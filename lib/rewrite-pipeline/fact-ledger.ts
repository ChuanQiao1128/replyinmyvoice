import {
  extractRequiredFacts,
  normalizeFactText,
  type RequiredFact,
} from "../fact-extraction";
import type { RewriteRequestInput } from "../validation";
import type { ExtractedFacts } from "./types";

export type ReviewedFact = {
  text: string;
  field: keyof ExtractedFacts;
  reason: string;
};

export type ReviewedFactLedger = {
  facts: ExtractedFacts;
  addedAnchors: RequiredFact[];
  rejectedFacts: ReviewedFact[];
};

const arrayFields = [
  "people_mentioned",
  "key_facts",
  "required_actions",
  "deadlines",
  "dates_times",
  "positive_notes",
  "concerns",
  "policies_or_conditions",
  "available_support",
  "clarifications",
  "facts_that_must_not_change",
  "sensitive_points",
] as const satisfies Array<keyof ExtractedFacts>;

const sentenceSplitPattern = /(?<=[.!?])\s+|\n+/;

const genericFactPatterns = [
  /\b(?:best regards\s+)?(?:customer support team|support team|customer service team)\b/i,
  /^(?:thank you|thanks|sorry|best regards|regards|sincerely)$/i,
];

const genericPersonValues = new Set([
  "account",
  "acceptable",
  "annual",
  "application",
  "basic",
  "billing",
  "cancellation",
  "current",
  "customer",
  "duplicate",
  "eligibility",
  "explain",
  "express",
  "finance",
  "invoice",
  "core",
  "new",
  "offer",
  "option",
  "order",
  "original",
  "partial",
  "per",
  "decision",
  "quiz",
  "remember",
  "renewal",
  "solo",
  "standard",
  "subscription",
  "team",
  "updated",
  "upgrade",
  "there",
  "this",
  "that",
  "it",
  "we",
  "you",
  "your",
  "our",
  "they",
  "them",
]);

const hardFactPattern =
  /\b(?:nzd|usd|aud|\$|\d|monday|tuesday|wednesday|thursday|friday|saturday|sunday|january|february|march|april|may|june|july|august|september|october|november|december|today|tomorrow|yesterday|am|pm|a\.m\.|p\.m\.|refund|credit|charge|invoice|subscription|plan|seat|cancel|canceled|cancelled|transfer|enrollment|cohort|registration|availability|eligible|policy|partial credit|full credit|deadline|approve|approval|confirm|confirmed|required|lost|returned|not|no|never|unless|only if|cannot|can't|will not|do not|does not|did not|has not|have not|is not|are not)\b/i;

const negationPattern =
  /\b(?:not|no|never|unless|cannot|can't|will not|do not|does not|did not|has not|have not|is not|are not|without)\b/i;

const negationSensitivePattern =
  /\b(?:lost|returned|refund|available|changed|required|approve|approval|confirmed|cancel|update|charge|credit|eligible)\b/i;

const tokenStopWords = new Set([
  "a",
  "an",
  "and",
  "are",
  "as",
  "at",
  "be",
  "been",
  "for",
  "from",
  "has",
  "have",
  "is",
  "it",
  "of",
  "on",
  "or",
  "that",
  "the",
  "this",
  "to",
  "with",
  "your",
]);

function sourceText(input: RewriteRequestInput) {
  return [
    input.messageToReplyTo,
    input.roughDraftReply,
    input.audience,
    input.purpose,
    input.whatHappened,
    input.factsToPreserve,
  ]
    .filter(Boolean)
    .join("\n");
}

function normalize(value: string) {
  return normalizeFactText(value);
}

function meaningfulTokens(value: string) {
  return normalize(value)
    .replace(/[^a-z0-9:$.'-]+/g, " ")
    .split(/\s+/)
    .map((token) => token.replace(/^[^a-z0-9]+|[^a-z0-9]+$/g, ""))
    .filter((token) => token.length > 2 && !tokenStopWords.has(token));
}

function isGenericFact(value: string) {
  return genericFactPatterns.some((pattern) => pattern.test(value.trim()));
}

function isForbiddenClaimInstruction(value: string) {
  return /^\s*(?:do not|don't)\s+(?:offer|promise|imply|guess|mention|send revised quote|make me sound|state|say)\b/i.test(
    value,
  );
}

function isGenericPerson(value: string) {
  return genericPersonValues.has(normalize(value));
}

function isHardFact(value: string) {
  return hardFactPattern.test(value);
}

function hasNegationConflict(fact: string, sentence: string) {
  if (!negationSensitivePattern.test(fact)) {
    return false;
  }

  return negationPattern.test(fact) !== negationPattern.test(sentence);
}

function hasEvidence(value: string, normalizedSource: string, sourceSentences: string[]) {
  const normalizedValue = normalize(value);
  const withoutArticle = normalizedValue.replace(/^(?:the|a|an)\s+/, "");

  if (!normalizedValue) {
    return false;
  }

  if (
    normalizedSource.includes(normalizedValue) ||
    normalizedSource.includes(withoutArticle)
  ) {
    return true;
  }

  const tokens = meaningfulTokens(value);
  if (tokens.length === 0) {
    return false;
  }

  return sourceSentences.some((sentence) => {
    const normalizedSentence = normalize(sentence);
    const coverage =
      tokens.filter((token) => normalizedSentence.includes(token)).length /
      tokens.length;

    if (coverage < 0.85) {
      return false;
    }

    return !hasNegationConflict(normalizedValue, normalizedSentence);
  });
}

function shouldKeepExtractedFact({
  field,
  normalizedSource,
  rejectedFacts,
  sourceSentences,
  text,
}: {
  field: keyof ExtractedFacts;
  normalizedSource: string;
  rejectedFacts: ReviewedFact[];
  sourceSentences: string[];
  text: string;
}) {
  const cleaned = text.trim();

  if (!cleaned || isGenericFact(cleaned) || isForbiddenClaimInstruction(cleaned)) {
    return false;
  }

  if (field === "people_mentioned" && isGenericPerson(cleaned)) {
    rejectedFacts.push({
      text: cleaned,
      field,
      reason: "Generic pronoun or placeholder is not a person.",
    });
    return false;
  }

  if (!isHardFact(cleaned)) {
    return true;
  }

  if (hasEvidence(cleaned, normalizedSource, sourceSentences)) {
    return true;
  }

  rejectedFacts.push({
    text: cleaned,
    field,
    reason: "No source evidence for hard fact.",
  });
  return false;
}

function unique(values: string[]) {
  const seen = new Set<string>();
  const result: string[] = [];

  for (const value of values) {
    const cleaned = value.replace(/\s+/g, " ").trim();
    const normalizedValue = normalize(cleaned);
    if (!cleaned || seen.has(normalizedValue)) {
      continue;
    }
    seen.add(normalizedValue);
    result.push(cleaned);
  }

  return result;
}

function addUnique(values: string[], value: string) {
  const normalizedValue = normalize(value);
  if (!normalizedValue || values.some((item) => normalize(item) === normalizedValue)) {
    return values;
  }

  return [...values, value];
}

function addAnchor(facts: ExtractedFacts, fact: RequiredFact) {
  facts.facts_that_must_not_change = addUnique(
    facts.facts_that_must_not_change,
    fact.text,
  );

  if (fact.category === "person") {
    facts.people_mentioned = addUnique(facts.people_mentioned, fact.text);
    return;
  }

  if (fact.category === "date") {
    facts.dates_times = addUnique(facts.dates_times, fact.text);
    return;
  }

  if (fact.category === "deadline") {
    facts.deadlines = addUnique(facts.deadlines, fact.text);
    return;
  }

  if (
    fact.category === "constraint" ||
    fact.category === "promise"
  ) {
    facts.policies_or_conditions = addUnique(
      facts.policies_or_conditions,
      fact.text,
    );
    return;
  }

  if (fact.category === "ordered_step") {
    facts.required_actions = addUnique(facts.required_actions, fact.text);
    return;
  }

  if (fact.category === "task" || fact.category === "quoted_phrase") {
    facts.key_facts = addUnique(facts.key_facts, fact.text);
  }
}

export function reviewExtractedFacts(
  input: RewriteRequestInput,
  extractedFacts: ExtractedFacts,
): ReviewedFactLedger {
  const source = sourceText(input);
  const normalizedSource = normalize(source);
  const sourceSentences = source
    .split(sentenceSplitPattern)
    .map((sentence) => sentence.trim())
    .filter(Boolean);
  const rejectedFacts: ReviewedFact[] = [];
  const facts: ExtractedFacts = {
    recipient_name: extractedFacts.recipient_name.trim(),
    sender_name_or_role: extractedFacts.sender_name_or_role.trim(),
    people_mentioned: [],
    main_purpose: extractedFacts.main_purpose.trim(),
    key_facts: [],
    required_actions: [],
    deadlines: [],
    dates_times: [],
    positive_notes: [],
    concerns: [],
    policies_or_conditions: [],
    available_support: [],
    clarifications: [],
    facts_that_must_not_change: [],
    sensitive_points: [],
    original_tone: extractedFacts.original_tone.trim(),
  };

  for (const field of arrayFields) {
    facts[field] = unique(
      extractedFacts[field].filter((item) =>
        shouldKeepExtractedFact({
          field,
          normalizedSource,
          rejectedFacts,
          sourceSentences,
          text: item,
        }),
      ),
    );
  }

  if (
    facts.recipient_name &&
    (isGenericPerson(facts.recipient_name) ||
      !hasEvidence(facts.recipient_name, normalizedSource, sourceSentences))
  ) {
    rejectedFacts.push({
      text: facts.recipient_name,
      field: "recipient_name",
      reason: "No source evidence for extracted recipient.",
    });
    facts.recipient_name = "";
  }

  const deterministicAnchors = extractRequiredFacts(input).filter(
    (fact) =>
      !isGenericFact(fact.text) &&
      !(fact.category === "person" && isGenericPerson(fact.text)),
  );

  for (const anchor of deterministicAnchors) {
    addAnchor(facts, anchor);
  }

  facts.people_mentioned = facts.people_mentioned.filter(
    (person) => !isGenericFact(person) && !isGenericPerson(person),
  );

  return {
    facts,
    addedAnchors: deterministicAnchors,
    rejectedFacts,
  };
}
