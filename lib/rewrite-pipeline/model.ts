import { z } from "zod";

import { optionalEnv } from "../env";
import {
  getOpenAiCompatibleApiKey,
  getOpenAiCompatibleChatCompletionsUrl,
  isDeepSeekCompatibleBaseUrl,
} from "../openai-compatible";
import type { RewriteTelemetryCollector } from "../observability/rewrite-telemetry";
import { estimateOpenAiCostUsd } from "../observability/rewrite-cost";
import type { RewriteRequestInput } from "../validation";
import type {
  CandidateKey,
  CandidateSet,
  ExtractedFacts,
  LlmFactCheckResult,
  ReviewResult,
  FactReconstructConfig,
  RewriteAttemptLedgerEntry,
  SentenceRiskReview,
  ScenarioClassification,
  StyleCard,
  RewriteStrategy,
} from "./types";
import type { HighRiskSentence } from "./targeted-repair";

function collectStringValues(value: unknown): string[] {
  if (value === null || value === undefined) {
    return [];
  }

  if (Array.isArray(value)) {
    return value.flatMap((item) => collectStringValues(item));
  }

  if (typeof value === "object") {
    return Object.values(value as Record<string, unknown>).flatMap((item) =>
      collectStringValues(item),
    );
  }

  if (
    typeof value === "string" ||
    typeof value === "number"
  ) {
    return [String(value)];
  }

  return [];
}

const stringArraySchema = z
  .preprocess(collectStringValues, z.array(z.string()).default([]))
  .transform((items) =>
    items
      .map((item) => item.trim())
      .filter((item) => item && !/^(not specified|unknown|n\/a|none)$/i.test(item)),
  );

const textSchema = z
  .preprocess((value) => collectStringValues(value).join("\n\n"), z.string())
  .transform((value) => value.trim())
  .refine((value) => value.length > 0, "Expected non-empty text.");

const optionalTextSchema = z
  .preprocess((value) => collectStringValues(value).join("\n\n"), z.string())
  .transform((value) => value.trim())
  .transform((value) =>
    /^(not specified|unknown|n\/a|none)$/i.test(value) ? "" : value,
  )
  .catch("");

function booleanSchema(defaultValue: boolean) {
  return z
    .preprocess((value) => {
      if (typeof value === "string") {
        const normalized = value.trim().toLowerCase();
        if (["true", "yes", "y", "1"].includes(normalized)) {
          return true;
        }
        if (["false", "no", "n", "0"].includes(normalized)) {
          return false;
        }
      }

      return value;
    }, z.boolean())
    .catch(defaultValue);
}

const extractedFactsSchema: z.ZodType<
  ExtractedFacts,
  z.ZodTypeDef,
  unknown
> = z.object({
  recipient_name: optionalTextSchema,
  sender_name_or_role: optionalTextSchema,
  people_mentioned: stringArraySchema,
  main_purpose: optionalTextSchema,
  key_facts: stringArraySchema,
  required_actions: stringArraySchema,
  deadlines: stringArraySchema,
  dates_times: stringArraySchema,
  positive_notes: stringArraySchema,
  concerns: stringArraySchema,
  policies_or_conditions: stringArraySchema,
  available_support: stringArraySchema,
  clarifications: stringArraySchema,
  facts_that_must_not_change: stringArraySchema,
  sensitive_points: stringArraySchema,
  original_tone: optionalTextSchema,
});

const scenarioSchema: z.ZodType<
  ScenarioClassification,
  z.ZodTypeDef,
  unknown
> = z.object({
  domain: z
    .enum([
      "education",
      "workplace",
      "customer_support",
      "sales",
      "personal",
      "general",
    ])
    .catch("general"),
  intent: z
    .enum([
      "explain",
      "remind",
      "decline",
      "apologize",
      "follow_up",
      "request",
      "summarize",
      "general_reply",
    ])
    .catch("general_reply"),
  format: z.enum(["email", "message", "note"]).catch("email"),
  relationship: optionalTextSchema,
  risk: z.enum(["low", "medium", "high"]).catch("low"),
  confidence: z.coerce.number().min(0).max(1).catch(0),
  style_card_id: optionalTextSchema,
  needs_action_steps: booleanSchema(true),
  needs_empathy: booleanSchema(false),
  needs_deadline_preservation: booleanSchema(false),
  notes: stringArraySchema,
});

const candidateSetSchema: z.ZodType<CandidateSet, z.ZodTypeDef, unknown> = z.object({
  candidate_a_concise: textSchema,
  candidate_b_warm: textSchema,
  candidate_c_natural: textSchema,
});

const candidateKeys = [
  "candidate_a_concise",
  "candidate_b_warm",
  "candidate_c_natural",
] as const;

const defaultCandidateScore = {
  factual_accuracy: 0,
  naturalness: 0,
  tone_appropriateness: 0,
  concision: 0,
  low_template_feel: 0,
  clarity_of_action_steps: 0,
  total: 0,
  issues: [],
};

const candidateScoreSchema = z.object({
  factual_accuracy: z.coerce.number().min(0).max(10).catch(0),
  naturalness: z.coerce.number().min(0).max(10).catch(0),
  tone_appropriateness: z.coerce.number().min(0).max(10).catch(0),
  concision: z.coerce.number().min(0).max(10).catch(0),
  low_template_feel: z.coerce.number().min(0).max(10).catch(0),
  clarity_of_action_steps: z.coerce.number().min(0).max(10).catch(0),
  total: z.coerce.number().min(0).max(60).catch(0),
  issues: stringArraySchema,
}).catch(defaultCandidateScore);

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function normalizeCandidateKey(value: unknown): CandidateKey | undefined {
  const text = collectStringValues(value).join(" ").toLowerCase();
  if (!text) {
    return undefined;
  }

  if (text.includes("candidate_a_concise") || text.includes("version_a")) {
    return "candidate_a_concise";
  }

  if (text.includes("candidate_b_warm") || text.includes("version_b")) {
    return "candidate_b_warm";
  }

  if (text.includes("candidate_c_natural") || text.includes("version_c")) {
    return "candidate_c_natural";
  }

  if (text.includes("concise") || /\ba\b/.test(text)) {
    return "candidate_a_concise";
  }

  if (text.includes("warm") || /\bb\b/.test(text)) {
    return "candidate_b_warm";
  }

  if (text.includes("natural") || /\bc\b/.test(text)) {
    return "candidate_c_natural";
  }

  return undefined;
}

function normalizeReviewPayload(value: unknown) {
  if (!isRecord(value)) {
    return value;
  }

  const scores = isRecord(value.scores)
    ? value.scores
    : {
        candidate_a_concise:
          value.candidate_a_concise ?? value.candidate_a ?? value.version_a,
        candidate_b_warm:
          value.candidate_b_warm ?? value.candidate_b ?? value.version_b,
        candidate_c_natural:
          value.candidate_c_natural ?? value.candidate_c ?? value.version_c,
      };
  const bestCandidate = normalizeCandidateKey(
    value.best_candidate_key ??
      value.best_candidate ??
      value.best_version ??
      value.selected_candidate ??
      value.selected ??
      value.winner,
  );

  return {
    ...value,
    scores,
    best_candidate_key: bestCandidate ?? "candidate_c_natural",
    required_edits:
      value.required_edits ?? value.requiredEdits ?? value.edits_needed ?? [],
    risk_notes: value.risk_notes ?? value.riskNotes ?? value.risk_notes ?? [],
  };
}

const reviewSchema: z.ZodType<ReviewResult, z.ZodTypeDef, unknown> =
  z.preprocess(
    normalizeReviewPayload,
    z.object({
      scores: z.object({
        candidate_a_concise: candidateScoreSchema,
        candidate_b_warm: candidateScoreSchema,
        candidate_c_natural: candidateScoreSchema,
      }),
      best_candidate_key: z.enum(candidateKeys),
      required_edits: stringArraySchema,
      risk_notes: stringArraySchema,
    }),
  );

const finalEmailSchema = z.object({
  final_email: textSchema,
});

const llmFactCheckSchema: z.ZodType<
  LlmFactCheckResult,
  z.ZodTypeDef,
  unknown
> = z.object({
  safe_to_send: booleanSchema(false),
  has_added_new_facts: booleanSchema(false),
  has_removed_important_facts: booleanSchema(false),
  has_changed_names: booleanSchema(false),
  has_changed_dates_or_deadlines: booleanSchema(false),
  has_changed_conditions_or_policies: booleanSchema(false),
  tone_problem: booleanSchema(false),
  issues: stringArraySchema,
  required_repairs: stringArraySchema,
});

function normalizeSentenceRiskPayload(value: unknown) {
  if (Array.isArray(value)) {
    return {
      sentence_diagnostics: value,
      overall_notes: [],
    };
  }

  if (!isRecord(value)) {
    return value;
  }

  const diagnostics =
    value.sentence_diagnostics ??
    value.sentenceDiagnostics ??
    value.diagnostics ??
    value.sentences ??
    [];

  return {
    ...value,
    sentence_diagnostics: diagnostics,
    overall_notes: value.overall_notes ?? value.overallNotes ?? value.notes ?? [],
  };
}

function normalizeSentenceRiskItem(value: unknown) {
  if (!isRecord(value)) {
    return value;
  }

  return {
    ...value,
    sentence:
      value.sentence ??
      value.text ??
      value.high_risk_sentence ??
      value.highRiskSentence,
    issue_tags:
      value.issue_tags ??
      value.issueTags ??
      value.tags ??
      value.reasons ??
      value.reason ??
      value.diagnosis ??
      [],
    repair_instruction:
      value.repair_instruction ??
      value.repairInstruction ??
      value.suggestion ??
      value.repair_suggestion ??
      value.instruction ??
      value.guidance ??
      "",
  };
}

const sentenceRiskReviewSchema: z.ZodType<
  SentenceRiskReview,
  z.ZodTypeDef,
  unknown
> = z.preprocess(
  normalizeSentenceRiskPayload,
  z.object({
    sentence_diagnostics: z.array(
      z.preprocess(
        normalizeSentenceRiskItem,
        z.object({
          sentence: textSchema,
          issue_tags: stringArraySchema,
          repair_instruction: textSchema,
        }),
      ),
    ),
    overall_notes: stringArraySchema,
  }),
);

function timeoutSignal(seconds: number) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), seconds * 1000);

  return {
    signal: controller.signal,
    clear: () => clearTimeout(timeout),
  };
}

function numberEnv(name: string, fallback: number) {
  const value = Number(optionalEnv(name, String(fallback)));
  return Number.isFinite(value) && value > 0 ? Math.trunc(value) : fallback;
}

function boundedNonNegativeIntegerEnv(name: string, fallback: number, max: number) {
  const value = Math.trunc(Number(optionalEnv(name, String(fallback))));

  if (!Number.isFinite(value) || value < 0) {
    return fallback;
  }

  return Math.min(value, max);
}

function maxTokensForRole(role: keyof FactReconstructConfig["models"]) {
  if (role === "cheap_structured") {
    return numberEnv("OPENAI_MAX_TOKENS_CHEAP_STRUCTURED", 1800);
  }

  if (role === "strong_escalation") {
    return numberEnv("OPENAI_MAX_TOKENS_STRONG_ESCALATION", 3200);
  }

  return numberEnv("OPENAI_MAX_TOKENS_MID_WRITER", 2800);
}

function deepSeekRequestOptions(role: keyof FactReconstructConfig["models"]) {
  if (!isDeepSeekCompatibleBaseUrl()) {
    return {};
  }

  if (
    role === "strong_escalation" &&
    optionalEnv("DEEPSEEK_ENABLE_STRONG_THINKING", "false") === "true"
  ) {
    return {
      thinking: { type: "enabled" },
      reasoning_effort: optionalEnv("DEEPSEEK_REASONING_EFFORT", "high"),
      max_tokens: maxTokensForRole(role),
    };
  }

  return {
    thinking: { type: "disabled" },
    max_tokens: maxTokensForRole(role),
  };
}

export class JsonCompletionQualityError extends Error {
  cause: unknown;

  constructor(cause: unknown) {
    super("Structured model output stayed unusable after retries.");
    this.name = "JsonCompletionQualityError";
    this.cause = cause;
  }
}

function escapedJsonControlCharacter(char: string) {
  switch (char) {
    case "\b":
      return "\\b";
    case "\t":
      return "\\t";
    case "\n":
      return "\\n";
    case "\f":
      return "\\f";
    case "\r":
      return "\\r";
    default:
      return `\\u${char.charCodeAt(0).toString(16).padStart(4, "0")}`;
  }
}

function escapeControlCharactersInJsonStrings(value: string) {
  let result = "";
  let inString = false;
  let escaped = false;

  for (const char of value) {
    if (!inString) {
      result += char;
      if (char === "\"") {
        inString = true;
      }
      continue;
    }

    if (char === "\"" && !escaped) {
      result += char;
      inString = false;
      continue;
    }

    if (char === "\\" && !escaped) {
      result += char;
      escaped = true;
      continue;
    }

    if (char.charCodeAt(0) <= 0x1f) {
      result += escapedJsonControlCharacter(char);
      escaped = false;
      continue;
    }

    result += char;
    escaped = false;
  }

  return result;
}

const retriableNetworkErrorCodes = new Set([
  "EAI_AGAIN",
  "ECONNRESET",
  "ECONNREFUSED",
  "ENETDOWN",
  "ENETUNREACH",
  "ENOTFOUND",
  "ETIMEDOUT",
  "UND_ERR_BODY_TIMEOUT",
  "UND_ERR_CONNECT_TIMEOUT",
  "UND_ERR_HEADERS_TIMEOUT",
  "UND_ERR_SOCKET",
]);

function nestedErrorCode(error: unknown): string | undefined {
  if (!error || typeof error !== "object") {
    return undefined;
  }

  const code = "code" in error ? error.code : undefined;
  if (typeof code === "string") {
    return code;
  }

  const cause = "cause" in error ? error.cause : undefined;
  return nestedErrorCode(cause);
}

function isRetriableNetworkError(error: unknown) {
  const code = nestedErrorCode(error);
  if (code && retriableNetworkErrorCodes.has(code)) {
    return true;
  }

  return error instanceof TypeError && /fetch failed/i.test(error.message);
}

async function createJsonCompletion<T>({
  config,
  model,
  role,
  schema,
  telemetry,
  temperature,
  system,
  user,
}: {
  config: FactReconstructConfig;
  model: string;
  role: keyof FactReconstructConfig["models"];
  schema: z.ZodType<T, z.ZodTypeDef, unknown>;
  telemetry?: RewriteTelemetryCollector;
  temperature: number;
  system: string;
  user: string;
}) {
  const timeoutSeconds = Number(optionalEnv("OPENAI_TIMEOUT_SEC", "35"));
  const retryCount = boundedNonNegativeIntegerEnv("OPENAI_RETRY_COUNT", 1, 2);
  const startedAt = Date.now();

  function latencyMs() {
    return Math.max(0, Date.now() - startedAt);
  }

  function estimateCost({
    inputTokens,
    outputTokens,
  }: {
    inputTokens: number;
    outputTokens: number;
  }) {
    const pricing = config.pricing[role];
    return estimateOpenAiCostUsd({
      inputTokens,
      outputTokens,
      inputPer1M: pricing.inputPer1M,
      outputPer1M: pricing.outputPer1M,
    });
  }

  function recordCall({
    errorCode,
    inputTokens = 0,
    outputTokens = 0,
    success,
  }: {
    errorCode?: string;
    inputTokens?: number;
    outputTokens?: number;
    success: boolean;
  }) {
    telemetry?.recordProviderCall({
      provider: "openai",
      role,
      model,
      inputTokens,
      outputTokens,
      estimatedCostUsd: estimateCost({ inputTokens, outputTokens }),
      latencyMs: latencyMs(),
      success,
      errorCode,
    });
  }

  function recordFailedCall(errorCode: string) {
    recordCall({ success: false, errorCode });
  }

  for (let attempt = 0; attempt <= retryCount; attempt += 1) {
    const timeout = timeoutSignal(
      Number.isFinite(timeoutSeconds) ? timeoutSeconds : 35,
    );
    try {
      const response = await fetch(getOpenAiCompatibleChatCompletionsUrl(), {
        method: "POST",
        headers: {
          Authorization: `Bearer ${getOpenAiCompatibleApiKey()}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          model,
          temperature,
          response_format: { type: "json_object" },
          ...deepSeekRequestOptions(role),
          messages: [
            { role: "system", content: system },
            { role: "user", content: user },
          ],
        }),
        signal: timeout.signal,
      });

      if (!response.ok) {
        const payload = (await response.json().catch(() => null)) as
          | { error?: { type?: string; code?: string; message?: string } }
          | null;
        const errorType =
          payload?.error?.type ?? payload?.error?.code ?? response.status;
        recordFailedCall(String(errorType));
        if (
          attempt < retryCount &&
          (response.status === 429 || response.status >= 500)
        ) {
          continue;
        }
        throw new Error(`OpenAI request failed: ${errorType}`);
      }

      const payload = (await response.json()) as {
        choices?: Array<{ message?: { content?: string | null } }>;
        usage?: {
          prompt_tokens?: number;
          completion_tokens?: number;
        };
      };
      const inputTokens = payload.usage?.prompt_tokens ?? 0;
      const outputTokens = payload.usage?.completion_tokens ?? 0;
      const rawContent = payload.choices?.at(0)?.message?.content;
      if (!rawContent) {
        recordCall({
          inputTokens,
          outputTokens,
          success: false,
          errorCode: "no_content",
        });
        throw new Error("OpenAI returned no content.");
      }

      try {
        const parsed = schema.parse(
          JSON.parse(escapeControlCharactersInJsonStrings(rawContent)),
        );
        recordCall({
          inputTokens,
          outputTokens,
          success: true,
        });
        return parsed;
      } catch (error) {
        if (error instanceof SyntaxError || error instanceof z.ZodError) {
          recordCall({
            inputTokens,
            outputTokens,
            success: false,
            errorCode: "bad_json",
          });
          if (attempt < retryCount) {
            continue;
          }
          throw new JsonCompletionQualityError(error);
        }
        throw error;
      }
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        recordFailedCall("timeout");
        if (attempt < retryCount) {
          continue;
        }
        throw new JsonCompletionQualityError(error);
      }
      if (isRetriableNetworkError(error)) {
        recordFailedCall("network_error");
        if (attempt < retryCount) {
          continue;
        }
        throw new JsonCompletionQualityError(error);
      }
      throw error;
    } finally {
      timeout.clear();
    }
  }

  throw new Error("OpenAI request failed after retry budget was exhausted.");
}

function inputText(input: RewriteRequestInput) {
  return [
    input.messageToReplyTo ? `Context/message:\n${input.messageToReplyTo}` : "",
    `Draft:\n${input.roughDraftReply}`,
    input.audience ? `Audience:\n${input.audience}` : "",
    input.purpose ? `Purpose:\n${input.purpose}` : "",
    input.whatHappened ? `What happened:\n${input.whatHappened}` : "",
    input.factsToPreserve ? `Facts to preserve:\n${input.factsToPreserve}` : "",
  ]
    .filter(Boolean)
    .join("\n\n");
}

function formatAttemptHistory(attemptHistory?: RewriteAttemptLedgerEntry[]) {
  if (!attemptHistory?.length) {
    return "";
  }

  return [
    "Prior failed attempts:",
    "Use these as negative evidence only. Do not treat failed candidates as a source of facts.",
    JSON.stringify(attemptHistory, null, 2),
  ].join("\n");
}

export async function extractFacts(
  input: RewriteRequestInput,
  config: FactReconstructConfig,
  telemetry?: RewriteTelemetryCollector,
) {
  return createJsonCompletion<ExtractedFacts>({
    config,
    model: config.models.cheap_structured,
    role: "cheap_structured",
    telemetry,
    temperature: 0.1,
    schema: extractedFactsSchema,
    system:
      "You are a factual information extraction assistant. Extract only facts from the user's draft and provided context. Do not rewrite, infer, or add unstated information. Return valid JSON only.",
    user: [
      "Schema keys: recipient_name, sender_name_or_role, people_mentioned, main_purpose, key_facts, required_actions, deadlines, dates_times, positive_notes, concerns, policies_or_conditions, available_support, clarifications, facts_that_must_not_change, sensitive_points, original_tone.",
      "Keep names, identifiers, counts, amounts, assignment names, dates, weekdays, times, time ranges, deadlines, offered options, policies, approval boundaries, clinical/admin disclaimers, conditions, support times, and negative constraints exact.",
      "Add every sentence with cannot, can't, do not, not, no, without, unless, only if, must, requires, before, after, or needs confirmation to facts_that_must_not_change.",
      "For short drafts, do not compress away operational details; every concrete date, time, option, ID, amount, count, condition, and next-step dependency is a locked fact.",
      "",
      inputText(input),
    ].join("\n"),
  });
}

export async function classifyScenario(
  facts: ExtractedFacts,
  config: FactReconstructConfig,
  telemetry?: RewriteTelemetryCollector,
) {
  return createJsonCompletion<ScenarioClassification>({
    config,
    model: config.models.cheap_structured,
    role: "cheap_structured",
    telemetry,
    temperature: 0.1,
    schema: scenarioSchema,
    system:
      "You are an email scenario classifier. Classify only from extracted facts. If confidence is low, choose general_professional_reply. Return valid JSON only.",
    user: [
      "Return domain, intent, format, relationship, risk, confidence, style_card_id, needs_action_steps, needs_empathy, needs_deadline_preservation, notes.",
      "Allowed style cards: teacher_parent_email_default, general_professional_reply, workplace_followup_default, customer_support_resolution, sales_followup_warm.",
      "If confidence < 0.55, use domain general, intent general_reply, style_card_id general_professional_reply.",
      "",
      JSON.stringify(facts),
    ].join("\n"),
  });
}

export async function generateCandidates({
  facts,
  scenario,
  styleCard,
  strategy,
  strategyGuidance,
  config,
  telemetry,
}: {
  facts: ExtractedFacts;
  scenario: ScenarioClassification;
  styleCard: StyleCard;
  strategy?: RewriteStrategy;
  strategyGuidance?: string[];
  config: FactReconstructConfig;
  telemetry?: RewriteTelemetryCollector;
}) {
  return createJsonCompletion<CandidateSet>({
    config,
    model: config.models.mid_writer,
    role: "mid_writer",
    telemetry,
    temperature: 0.7,
    schema: candidateSetSchema,
    system:
      "You are a professional email rewriting assistant. Rewrite from extracted facts and the style card only. Do not summarize the user's draft. Do not add facts. Return valid JSON only.",
    user: [
      "Write three candidate emails.",
      "candidate_a_concise: concise and direct.",
      "candidate_b_warm: warm and supportive.",
      "candidate_c_natural: natural everyday professional email.",
      "A simple realistic email is better than a polished template.",
      "Preserve every name, ID, amount, count, date, weekday, time, time range, deadline, offered option, assignment name, condition, and support time.",
      "Warm tone means kinder wording, not weaker boundaries.",
      "Do not add new dates, times, options, deadlines, commitments, discounts, refunds, approvals, clinical interpretations, policy exceptions, or next steps.",
      "Do not drop policies, approval cycles, clinical/admin disclaimers, conditions, clarifications, support availability, partial-credit rules, no-redo constraints, required confirmations, or negative constraints.",
      "If facts_that_must_not_change is present, every item is locked. Keep it in the candidate as exact wording or a direct, complete paraphrase.",
      "For drafts under 200 words, keep close to the original level of detail. Improve structure and tone, but do not compress away operational details.",
      "For long replies, group related ideas into useful paragraphs. Do not split every sentence into a separate paragraph.",
      "If using a numbered list, keep each number and item on the same line.",
      "For support and policy replies, separate what is known, what is conditional, and what the recipient should do next.",
      "Do not add subject lines, titles, headings, or unsupported openings.",
      "Do not intentionally add mistakes. Do not make it casual or sloppy.",
      strategy ? `Selected strategy: ${strategy}.` : "",
      strategyGuidance?.length
        ? `Strategy guidance:\n${strategyGuidance.map((item) => `- ${item}`).join("\n")}`
        : "",
      "",
      `Facts:\n${JSON.stringify(facts)}`,
      `Scenario:\n${JSON.stringify(scenario)}`,
      `Style card:\n${JSON.stringify(styleCard)}`,
    ].join("\n"),
  });
}

export async function reviewCandidates({
  facts,
  scenario,
  styleCard,
  candidates,
  config,
  telemetry,
}: {
  facts: ExtractedFacts;
  scenario: ScenarioClassification;
  styleCard: StyleCard;
  candidates: CandidateSet;
  config: FactReconstructConfig;
  telemetry?: RewriteTelemetryCollector;
}) {
  return createJsonCompletion<ReviewResult>({
    config,
    model: config.models.cheap_structured,
    role: "cheap_structured",
    telemetry,
    temperature: 0.1,
    schema: reviewSchema,
    system:
      "You are a strict email quality reviewer. Evaluate real writing quality only. Do not optimize for third-party scoring tools. Return valid JSON only.",
    user: [
      "Score each candidate 0-10 for factual_accuracy, naturalness, tone_appropriateness, concision, low_template_feel, clarity_of_action_steps.",
      "Penalize added dates, times, options, deadlines, commitments, discounts, refunds, approvals, clinical interpretations, policy exceptions, or next steps.",
      "Penalize missing IDs, names, amounts, counts, deadlines, approval cycles, clinical/admin disclaimers, negative constraints, and changed conditions.",
      "Penalize corporate language, generic empathy, repeated appreciation, vague motivational filler, and policy-statement tone.",
      "Penalize subject lines, titles, headings, dropped signoffs, dropped partial-credit rules, dropped support times, and dropped clarifications.",
      "Penalize broken numbered lists, detached list markers, every-sentence-new-paragraph formatting, forwarded-thread headers, and quote fragments.",
      "A slightly simpler email that sounds like a real person is better than a polished template.",
      "",
      `Facts:\n${JSON.stringify(facts)}`,
      `Scenario:\n${JSON.stringify(scenario)}`,
      `Style card:\n${JSON.stringify(styleCard)}`,
      `Candidates:\n${JSON.stringify(candidates)}`,
    ].join("\n"),
  });
}

export async function finalizeCandidate({
  facts,
  scenario,
  styleCard,
  selectedCandidate,
  requiredEdits,
  config,
  telemetry,
}: {
  facts: ExtractedFacts;
  scenario: ScenarioClassification;
  styleCard: StyleCard;
  selectedCandidate: string;
  requiredEdits: string[];
  config: FactReconstructConfig;
  telemetry?: RewriteTelemetryCollector;
}) {
  const result = await createJsonCompletion<{ final_email: string }>({
    config,
    model: config.models.mid_writer,
    role: "mid_writer",
    telemetry,
    temperature: 0.25,
    schema: finalEmailSchema,
    system:
      "You are a final email editor. Lightly edit the selected candidate. Do not add facts. Return valid JSON only with final_email.",
    user: [
      "Apply only necessary edits. Preserve all names, IDs, amounts, counts, assignment names, dates, weekdays, times, time ranges, deadlines, offered options, policies, approval boundaries, clinical/admin disclaimers, and support times.",
      "Preserve partial-credit rules, no-redo clarifications, required confirmations, negative constraints, available support times, and signoffs when they are present.",
      "Warm tone means kinder wording, not weaker boundaries. Do not summarize away facts.",
      "Keep it natural, clear, and professional. Avoid generic template-like language.",
      "Do not add subject lines, titles, or headings.",
      'Do not include internal analysis language such as "is referenced", "was mentioned", "Based on the provided context", "the source says", "extracted facts", or "reviewer notes".',
      "",
      `Facts:\n${JSON.stringify(facts)}`,
      `Scenario:\n${JSON.stringify(scenario)}`,
      `Style card:\n${JSON.stringify(styleCard)}`,
      `Selected candidate:\n${selectedCandidate}`,
      `Reviewer required edits:\n${JSON.stringify(requiredEdits)}`,
    ].join("\n"),
  });

  return result.final_email;
}

export async function llmFactCheck({
  facts,
  finalEmail,
  config,
  telemetry,
}: {
  facts: ExtractedFacts;
  finalEmail: string;
  config: FactReconstructConfig;
  telemetry?: RewriteTelemetryCollector;
}) {
  return createJsonCompletion<LlmFactCheckResult>({
    config,
    model: config.models.cheap_structured,
    role: "cheap_structured",
    telemetry,
    temperature: 0.1,
    schema: llmFactCheckSchema,
    system:
      "You compare a final email against extracted facts. Be strict about names, dates, deadlines, conditions, and policies. Return valid JSON only.",
    user: [
      "Check whether the final email is safe to send.",
      "Return safe_to_send false if facts were added, removed, softened, or changed.",
      "Be strict about unsupported dates, times, options, deadlines, commitments, discounts, refunds, approvals, clinical interpretations, policy exceptions, and next steps.",
      "",
      `Facts:\n${JSON.stringify(facts)}`,
      `Final email:\n${finalEmail}`,
    ].join("\n"),
  });
}

export async function diagnoseHighRiskSentences({
  facts,
  scenario,
  styleCard,
  highRiskSentences,
  config,
  telemetry,
}: {
  facts: ExtractedFacts;
  scenario: ScenarioClassification;
  styleCard: StyleCard;
  highRiskSentences: HighRiskSentence[];
  config: FactReconstructConfig;
  telemetry?: RewriteTelemetryCollector;
}) {
  return createJsonCompletion<SentenceRiskReview>({
    config,
    model: config.models.cheap_structured,
    role: "cheap_structured",
    telemetry,
    temperature: 0.1,
    schema: sentenceRiskReviewSchema,
    system:
      "You are an internal email quality reviewer. Diagnose why specific sentences feel generic or template-like. Do not optimize for third-party scoring tools. Return valid JSON only.",
    user: [
      "For each sentence, assign concise issue_tags and a repair_instruction.",
      "Allowed issue tag examples: generic_empathy, corporate_template, over_polished, vague_filler, repeated_appreciation, too_symmetric, low_specificity, policy_statement_voice.",
      "Do not suggest adding facts. Repairs must preserve the extracted facts.",
      "",
      `Facts:\n${JSON.stringify(facts)}`,
      `Scenario:\n${JSON.stringify(scenario)}`,
      `Style card:\n${JSON.stringify(styleCard)}`,
      `Sentences to diagnose:\n${JSON.stringify(highRiskSentences)}`,
    ].join("\n"),
  });
}

export async function repairHighRiskSentences({
  facts,
  scenario,
  styleCard,
  finalEmail,
  diagnostics,
  config,
  telemetry,
}: {
  facts: ExtractedFacts;
  scenario: ScenarioClassification;
  styleCard: StyleCard;
  finalEmail: string;
  diagnostics: SentenceRiskReview;
  config: FactReconstructConfig;
  telemetry?: RewriteTelemetryCollector;
}) {
  const result = await createJsonCompletion<{ final_email: string }>({
    config,
    model: config.models.mid_writer,
    role: "mid_writer",
    telemetry,
    temperature: 0.35,
    schema: finalEmailSchema,
    system:
      "You are a targeted sentence editor. Replace only the diagnosed weak sentences while preserving the rest of the email. Return valid JSON only with final_email.",
    user: [
      "Repair only the diagnosed sentences. Keep all other sentences and paragraph order as stable as possible.",
      "Do not add facts. Do not remove facts. Preserve all names, IDs, dates, weekdays, times, deadlines, amounts, counts, offered options, conditions, policies, approval boundaries, clinical/admin disclaimers, support times, and signoffs.",
      "Restore missing locked facts and remove unsupported additions; for scheduling, use only the options in the original draft.",
      "Make the repaired sentences simpler, more specific, and less template-like without becoming casual or sloppy.",
      "Do not add subject lines, titles, headings, or explanations.",
      'Do not include internal analysis language such as "is referenced", "was mentioned", "Based on the provided context", "the source says", "extracted facts", or "reviewer notes".',
      "",
      `Facts:\n${JSON.stringify(facts)}`,
      `Scenario:\n${JSON.stringify(scenario)}`,
      `Style card:\n${JSON.stringify(styleCard)}`,
      `Current final email:\n${finalEmail}`,
      `Sentence diagnostics:\n${JSON.stringify(diagnostics)}`,
    ].join("\n"),
  });

  return result.final_email;
}

export async function escalateCandidate({
  facts,
  scenario,
  styleCard,
  previousFinalEmail,
  reviewNotes,
  strategy,
  strategyGuidance,
  attemptHistory,
  config,
  telemetry,
}: {
  facts: ExtractedFacts;
  scenario: ScenarioClassification;
  styleCard: StyleCard;
  previousFinalEmail: string;
  reviewNotes: string[];
  strategy?: RewriteStrategy;
  strategyGuidance?: string[];
  attemptHistory?: RewriteAttemptLedgerEntry[];
  config: FactReconstructConfig;
  telemetry?: RewriteTelemetryCollector;
}) {
  const result = await createJsonCompletion<{ final_email: string }>({
    config,
    model: config.models.strong_escalation,
    role: "strong_escalation",
    telemetry,
    temperature: 0.45,
    schema: finalEmailSchema,
    system:
      "You are a senior email editor. Rewrite from extracted facts again. Do not add facts. Do not mention scores or third-party scoring tools. Return valid JSON only with final_email.",
    user: [
      "The previous rewrite did not meet our internal quality bar for natural professional writing.",
      "Make it sound like a normal professional email written by a real person in this scenario.",
      "Keep it specific and practical. Reduce generic template language.",
      "Preserve every factual detail, including IDs, amounts, counts, dates, weekdays, times, deadlines, names, offered options, conditions, approval boundaries, clinical/admin disclaimers, and support times.",
      "Restore any missing policy, approval-cycle, clinical/admin, partial-credit, clarification, support-time, negative-constraint, or signoff facts from the extracted facts.",
      "Remove unsupported additions. For scheduling cases, do not invent alternatives; use only the options in the original draft.",
      "Fix structural problems such as detached numbered-list markers, quote fragments, and one-sentence-per-paragraph output.",
      "For long support replies, use a compact send-ready structure instead of line-by-line paraphrasing.",
      "Do not add subject lines, titles, or headings.",
      "Do not make it casual or sloppy.",
      'Do not include internal analysis language such as "is referenced", "was mentioned", "Based on the provided context", "the source says", "extracted facts", or "reviewer notes".',
      strategy ? `Selected repair strategy: ${strategy}.` : "",
      strategyGuidance?.length
        ? `Strategy guidance:\n${strategyGuidance.map((item) => `- ${item}`).join("\n")}`
        : "",
      "",
      `Facts:\n${JSON.stringify(facts)}`,
      `Scenario:\n${JSON.stringify(scenario)}`,
      `Style card:\n${JSON.stringify(styleCard)}`,
      `Previous final email:\n${previousFinalEmail}`,
      `Reviewer notes:\n${JSON.stringify(reviewNotes)}`,
      formatAttemptHistory(attemptHistory),
    ].join("\n"),
  });

  return result.final_email;
}
