import type { ScenarioOption } from "./rewrite-presets";
import type { RewriteRequestInput } from "./validation";

export const diagnosisTagOptions = [
  "stock_opening",
  "corporate_polish",
  "uniform_rhythm",
  "over_explained",
  "generic_transitions",
  "policy_memo_voice",
  "low_specificity",
  "too_balanced_structure",
  "over_safe_tone",
  "support_template_voice",
  "application_cliche",
] as const;

export type DiagnosisTag = (typeof diagnosisTagOptions)[number];

export type RewritePlan = {
  scenario: ScenarioOption;
  tags: DiagnosisTag[];
  summary: string;
  guardrails: string[];
  steps: string[];
  preserve: string[];
};

const scenarioGuardrails: Record<ScenarioOption, string[]> = {
  "Blank / custom": [
    "preserve the user's exact facts, names, numbers, dates, and requested next step",
    "do not add a recipient, relationship, timeline, promise, or sign-off that was not provided",
    "make the text sound like a practical note someone would actually send",
  ],
  "Email or message reply": [
    "preserve the relationship cues from the original thread",
    "do not add a recipient name unless it appears in the provided text",
    "answer the specific message directly before adding any extra explanation",
  ],
  "Customer support": [
    "preserve amounts, counts, dates, product names, policy limits, and next steps exactly",
    "answer the specific question before giving background",
    "avoid new promises, refunds, credits, changes, or timelines unless they are already provided",
  ],
  "Cover letter": [
    "preserve the user's actual experience and do not invent achievements",
    "replace broad self-praise with specific claims already present in the draft",
    "keep the note confident without sounding like a template application paragraph",
  ],
  "Work update": [
    "preserve ownership, deadlines, blockers, status, and requested action",
    "make the update easy to scan without turning it into a formal memo",
    "do not add certainty or commitments that were not provided",
  ],
};

const tagRepairs: Record<DiagnosisTag, string> = {
  stock_opening: "replace stock openings with a direct first line tied to the actual situation",
  corporate_polish: "swap formal service language for plain words and contractions where natural",
  uniform_rhythm: "vary sentence length and avoid making every sentence the same shape",
  over_explained: "cut repeated setup and keep only the explanation the recipient needs",
  generic_transitions: "remove generic transitions and connect facts with simple phrasing",
  policy_memo_voice: "turn policy-heavy language into a human but careful reply",
  low_specificity: "carry over the concrete names, dates, amounts, counts, and next step",
  too_balanced_structure: "avoid symmetrical list-like phrasing when a simple note is enough",
  over_safe_tone: "keep necessary caution but remove defensive hedging",
  support_template_voice: "answer the customer's real question instead of using support macros",
  application_cliche: "replace application cliches with specific motivation and evidence from the draft",
};

function includesAny(value: string, phrases: string[]) {
  return phrases.some((phrase) => value.includes(phrase));
}

function splitSentences(value: string) {
  return value
    .split(/[.!?]\s+|\n+/)
    .map((part) => part.trim())
    .filter(Boolean);
}

function unique<T>(items: T[]) {
  return Array.from(new Set(items));
}

export function getScenarioGuardrails(scenario: ScenarioOption) {
  return scenarioGuardrails[scenario] ?? scenarioGuardrails["Blank / custom"];
}

export function analyzeDraftForRewrite(input: RewriteRequestInput): DiagnosisTag[] {
  const draft = input.roughDraftReply.trim();
  const lower = draft.toLowerCase();
  const sentences = splitSentences(draft);
  const tags: DiagnosisTag[] = [];

  if (
    includesAny(lower, [
      "thank you for reaching out",
      "thanks for reaching out",
      "i understand your concern",
      "we understand your concern",
      "i acknowledge receipt",
    ])
  ) {
    tags.push("stock_opening");
  }

  if (
    includesAny(lower, [
      "regarding the matter",
      "at your earliest convenience",
      "please be advised",
      "proceed accordingly",
      "we will look into the matter",
      "provide an update as soon as possible",
    ])
  ) {
    tags.push("corporate_polish");
  }

  if (
    includesAny(lower, [
      "furthermore",
      "moreover",
      "in addition",
      "in conclusion",
      "as previously mentioned",
    ])
  ) {
    tags.push("generic_transitions");
  }

  const sentenceLengths = sentences.map((sentence) => sentence.split(/\s+/).length);
  if (
    sentenceLengths.length >= 4 &&
    Math.max(...sentenceLengths) - Math.min(...sentenceLengths) <= 5
  ) {
    tags.push("uniform_rhythm");
  }

  if (
    draft.length > 800 &&
    includesAny(lower, ["to help you explain", "for next steps", "in plain english"])
  ) {
    tags.push("over_explained");
  }

  if (includesAny(lower, ["according to policy", "course policy", "policy states"])) {
    tags.push("policy_memo_voice");
  }

  if (!/[0-9]|nzd|\$|may|june|july|monday|tuesday|wednesday|thursday|friday/i.test(draft)) {
    tags.push("low_specificity");
  }

  if (
    includesAny(lower, [
      "on the one hand",
      "on the other hand",
      "it is important to note",
      "there are several factors",
    ])
  ) {
    tags.push("too_balanced_structure");
  }

  if (includesAny(lower, ["may be", "might be", "could potentially", "not necessarily"])) {
    tags.push("over_safe_tone");
  }

  if (
    input.scenario === "Customer support" &&
    includesAny(lower, [
      "thank you for contacting us",
      "we apologize for any inconvenience",
      "our team is currently reviewing",
      "we appreciate your patience",
    ])
  ) {
    tags.push("support_template_voice");
  }

  if (
    input.scenario === "Cover letter" &&
    includesAny(lower, [
      "i am writing to express my interest",
      "passionate",
      "results-driven",
      "proven track record",
      "perfect fit",
      "dynamic team",
    ])
  ) {
    tags.push("application_cliche");
  }

  return unique(tags);
}

export function extractPreserveHints(input: RewriteRequestInput) {
  const source = [input.messageToReplyTo, input.roughDraftReply, input.factsToPreserve]
    .join(" ")
    .replace(/\s+/g, " ");
  const matches = source.match(
    /\b(?:NZD\s*)?\$\s?\d+(?:\.\d{2})?|\b\d+\s?(?:seats?|users?|days?|weeks?|months?|pm|am)\b|\b\d+\s*(?:am|pm)\b|\bfirst week of June\b|\bcourse policy\b|\btwo missing participation activities\b|\bone missing exit ticket\b|\btwo asked\b|\btwo other vendors\b|\bsource file arrived later\b|\bsource file arrived late\b|\b2pm launch check\b|\bpause the campaign\b|\bold pilot workspace\b|\bcustom tags column\b|\bbilling report folder\b|\breporting feature\b|\bteam templates\b|\bhelp center articles?\b|\bmonthly partner updates\b|\bweekly partner updates\b|\bpatient follow-up notes\b|\bpartner onboarding packet\b|\bMonday board packet\b|\bapplication timeline\b|\bscholarship forms\b|\bpricing table\b|\bsection three\b|\bsection five\b|\bpayment flow\b|\bonboarding checklist\b|\bhelp article links\b|\blast three failed events\b|\bfinance manager\b|\bbase plan\b|\b(?:old|new)\s+plan\s+(?:credit|charge)\b|\b(?:resent|sent)\s+the\s+invite\s+(?:twice|again)\b|\b(?:January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)\s+\d{1,2}\b|\b[A-Z][a-z]+(?:\s[A-Z][a-z]+)?\b/g,
  );

  return unique((matches ?? []).slice(0, 12));
}

export function createRewritePlan(
  input: RewriteRequestInput,
  tags: DiagnosisTag[] = analyzeDraftForRewrite(input),
): RewritePlan {
  const guardrails = getScenarioGuardrails(input.scenario);
  const preserve = extractPreserveHints(input);
  const steps = [
    "start from the recipient's actual situation, not a generic service opening",
    ...tags.map((tag) => tagRepairs[tag]),
    "answer the specific question or complete the specific writing job",
    "keep only facts that appear in the provided message, draft, or context",
    "measure the rewritten text and repair once if the AI-like signal stays high or the reply loses important facts",
  ];

  return {
    scenario: input.scenario,
    tags,
    summary: tags.length
      ? `Target ${tags.join(", ")} while preserving the user's facts.`
      : "Make the draft more natural while preserving the user's facts.",
    guardrails,
    steps: unique(steps),
    preserve,
  };
}
