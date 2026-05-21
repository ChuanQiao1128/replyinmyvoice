import type { RewriteRequestInput } from "./validation";
import type { ScenarioOption, TonePreset } from "./rewrite-presets";
import { tonePresetToTone } from "./rewrite-presets";

export type RewriteEmailEvalMode = "smoke" | "focused" | "full";

export type RewriteEmailEvalCase = {
  caseNumber: number;
  title: string;
  id: string;
  category: string;
  riskTags: string[];
  scenario: ScenarioOption;
  tonePreset: TonePreset;
  messageToReplyTo: string;
  roughDraftReply: string;
  audience: string;
  purpose: string;
  whatHappened: string;
  factsToPreserve: string;
  expectedRewriteChallenges: string;
};

export type FactsToPreserveExpectations = {
  expectedFacts: string[];
  forbiddenClaims: string[];
};

const REQUIRED_FIELDS = [
  "id",
  "category",
  "risk_tags",
  "message_to_reply_to",
  "rough_draft_reply",
  "audience",
  "purpose",
  "what_actually_happened",
  "facts_to_preserve",
  "expected_rewrite_challenges",
] as const;

function scenarioForCategory(category: string): ScenarioOption {
  if (
    category.includes("customer") ||
    category.includes("billing") ||
    category.includes("subscription") ||
    category.includes("account_access") ||
    category.includes("policy")
  ) {
    return "Customer support";
  }

  return "General reply";
}

function toneForCategory(category: string): TonePreset {
  if (
    category.includes("workplace") ||
    category.includes("procurement") ||
    category.includes("legal") ||
    category.includes("logistics") ||
    category.includes("professional_services")
  ) {
    return "Direct";
  }

  return "Warm";
}

function readField(
  fields: Map<string, string>,
  field: (typeof REQUIRED_FIELDS)[number],
  caseNumber: number,
) {
  const value = fields.get(field);
  if (!value) {
    throw new Error(`Case ${caseNumber} is missing ${field}.`);
  }

  return value;
}

export function parseRewriteEmailEvalCases(
  markdown: string,
): RewriteEmailEvalCase[] {
  const headerPattern = /^### Case (\d{3}) - (.+)$/gm;
  const headers = [...markdown.matchAll(headerPattern)];

  return headers.map((header, index) => {
    const caseNumber = Number(header[1]);
    const title = header[2].trim();
    const bodyStart = (header.index ?? 0) + header[0].length;
    const bodyEnd =
      index + 1 < headers.length ? headers[index + 1].index ?? markdown.length : markdown.length;
    const body = markdown.slice(bodyStart, bodyEnd);
    const fields = new Map<string, string>();

    for (const line of body.split(/\r?\n/)) {
      const match = line.match(/^- ([a-z_]+):\s*(.*)$/);
      if (match) {
        fields.set(match[1], match[2].trim());
      }
    }

    for (const field of REQUIRED_FIELDS) {
      readField(fields, field, caseNumber);
    }

    const category = readField(fields, "category", caseNumber);

    return {
      caseNumber,
      title,
      id: readField(fields, "id", caseNumber),
      category,
      riskTags: readField(fields, "risk_tags", caseNumber)
        .split(",")
        .map((tag) => tag.trim())
        .filter(Boolean),
      scenario: scenarioForCategory(category),
      tonePreset: toneForCategory(category),
      messageToReplyTo: readField(fields, "message_to_reply_to", caseNumber),
      roughDraftReply: readField(fields, "rough_draft_reply", caseNumber),
      audience: readField(fields, "audience", caseNumber),
      purpose: readField(fields, "purpose", caseNumber),
      whatHappened: readField(fields, "what_actually_happened", caseNumber),
      factsToPreserve: readField(fields, "facts_to_preserve", caseNumber),
      expectedRewriteChallenges: readField(
        fields,
        "expected_rewrite_challenges",
        caseNumber,
      ),
    };
  });
}

export function selectRewriteEmailEvalCases(
  cases: RewriteEmailEvalCase[],
  mode: RewriteEmailEvalMode,
) {
  const limit = mode === "smoke" ? 10 : mode === "focused" ? 40 : 100;

  return cases.slice(0, Math.min(limit, cases.length));
}

function isForbiddenClaimExpectation(value: string) {
  return /^\s*(?:do not|don't)\s+(?:offer|promise|imply|guess|mention|send revised quote|make me sound|state|say)\b/i.test(
    value,
  );
}

export function factsToPreserveExpectations(
  value: string,
): FactsToPreserveExpectations {
  const expectedFacts: string[] = [];
  const forbiddenClaims: string[] = [];

  for (const item of value.split(/(?<=\.)\s+/)) {
    const trimmed = item.trim();
    if (!trimmed) {
      continue;
    }

    if (isForbiddenClaimExpectation(trimmed)) {
      forbiddenClaims.push(trimmed);
    } else {
      expectedFacts.push(trimmed);
    }
  }

  return { expectedFacts, forbiddenClaims };
}

export function rewriteEmailEvalCaseToRequestInput(
  sample: RewriteEmailEvalCase,
): RewriteRequestInput {
  return {
    scenario: sample.scenario,
    messageToReplyTo: sample.messageToReplyTo,
    roughDraftReply: sample.roughDraftReply,
    audience: sample.audience,
    purpose: sample.purpose,
    whatHappened: sample.whatHappened,
    factsToPreserve: sample.factsToPreserve,
    tone: tonePresetToTone(sample.tonePreset),
    tonePreset: sample.tonePreset,
  };
}
