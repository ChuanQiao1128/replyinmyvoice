import {
  detectUnsupportedFacts,
  missingRequiredFacts,
} from "../fact-extraction";
import type { RewriteRequestInput } from "../validation";
import type {
  CandidateScore,
  ExtractedFacts,
  LlmFactCheckResult,
  StyleCard,
} from "./types";

export type DeterministicCheckResult = {
  safe: boolean;
  issues: string[];
  missingFacts: string[];
  unsupportedFacts: string[];
};

function normalize(value: string) {
  return value
    .toLowerCase()
    .replace(/[’]/g, "'")
    .replace(/\balready-submitted\b/g, "already submitted")
    .replace(/\bresent\b/g, "send them again")
    .replace(/\s+/g, " ")
    .trim();
}

function paragraphs(value: string) {
  return value
    .split(/\n{2,}/)
    .map((paragraph) => paragraph.trim())
    .filter(Boolean);
}

function wordCount(value: string) {
  return value.trim().split(/\s+/).filter(Boolean).length;
}

function sentenceCount(value: string) {
  return value
    .split(/[.!?]+(?:\s+|$)/)
    .map((sentence) => sentence.trim())
    .filter(Boolean).length;
}

function containsLimitedPhrase(text: string, styleCard: StyleCard) {
  const normalizedText = normalize(text);

  return styleCard.phrases_to_avoid_or_limit.filter((phrase) =>
    normalizedText.includes(normalize(phrase)),
  );
}

const genericLockedFactTokens = new Set([
  "specific",
  "there",
  "are",
  "clearly",
  "exactly",
  "current",
  "date",
  "provided",
  "stated",
  "start",
  "time",
]);

function preservesMustNotChange(text: string, facts: ExtractedFacts) {
  const normalizedText = normalize(text);

  return facts.facts_that_must_not_change.filter((fact) => {
    const normalizedFact = normalize(fact);
    if (!normalizedFact) {
      return false;
    }

    const tokens = normalizedFact
      .replace(/[^a-z0-9:$.'-]+/g, " ")
      .split(/\s+/)
      .map((token) => token.replace(/^[^a-z0-9]+|[^a-z0-9]+$/g, ""))
      .filter((token) => token.length > 2 && !genericLockedFactTokens.has(token));

    if (tokens.length > 5) {
      return false;
    }

    return tokens.some(Boolean) && !tokens.every((token) => normalizedText.includes(token));
  });
}

function hasDanglingClosing(text: string) {
  return /(?:^|\n)\s*(best regards|best|regards|sincerely|thanks|thank you),?\s*$/i.test(
    text.trim(),
  );
}

function detectMetaLanguage(text: string) {
  const patterns: Array<[RegExp, string]> = [
    [
      /\b(?:is|was|were)\s+(?:referenced|mentioned|provided|included|stated)\b/i,
      "meta_language:fact_reference",
    ],
    [
      /\bbased on (?:the )?(?:provided )?(?:context|information|details)\b/i,
      "meta_language:provided_context",
    ],
    [
      /\b(?:the )?(?:source|draft|original|input|facts?|context)\s+(?:says|states|mentions|indicates|notes)\b/i,
      "meta_language:source_reference",
    ],
    [/\bextracted facts?\b/i, "meta_language:extracted_facts"],
    [/\breviewer notes?\b/i, "meta_language:reviewer_notes"],
  ];

  return patterns
    .filter(([pattern]) => pattern.test(text))
    .map(([, issue]) => issue);
}

export function detectStructureIssues(
  input: RewriteRequestInput,
  rewrittenText: string,
) {
  const issues: string[] = [];
  const inputParagraphs = paragraphs(
    [input.messageToReplyTo, input.roughDraftReply].filter(Boolean).join("\n\n"),
  );
  const outputParagraphs = paragraphs(rewrittenText);
  const outputWords = wordCount(rewrittenText);
  const detachedNumberedItem =
    /(?:^|\n)\s*\d+[.)]\s*(?:\n|$)/.test(rewrittenText) ||
    /\b(?:options?|steps?|choices?)\s*:\s*\d+[.)]\s*(?:\n|$)/i.test(
      rewrittenText,
    );
  const detachedBulletItem = /(?:^|\n)\s*[-*]\s*(?:\n|$)/.test(rewrittenText);

  if (detachedNumberedItem) {
    issues.push("structure:broken_numbered_list");
  }

  if (detachedBulletItem) {
    issues.push("structure:broken_bullet_list");
  }

  const singleSentenceParagraphs = outputParagraphs.filter(
    (paragraph) => sentenceCount(paragraph) <= 1 && wordCount(paragraph) <= 45,
  ).length;
  if (
    outputWords >= 110 &&
    outputParagraphs.length >= 8 &&
    singleSentenceParagraphs / outputParagraphs.length >= 0.65
  ) {
    issues.push("structure:sentence_per_paragraph");
  }

  if (
    outputWords >= 140 &&
    inputParagraphs.length > 0 &&
    outputParagraphs.length >= Math.max(8, inputParagraphs.length * 1.8)
  ) {
    issues.push("structure:line_split_paraphrase");
  }

  if (/["“][^"”]*\n{2,}[^"”]*["”]/.test(rewrittenText)) {
    issues.push("structure:broken_quote_boundary");
  }

  if (
    /(?:^|\n)\s*(from:|sent:|to:|subject:|on .+ wrote:|[-]{2,}\s*forwarded message|[-]{2,}\s*original message)/i.test(
      rewrittenText,
    )
  ) {
    issues.push("structure:messy_thread_leak");
  }

  return issues;
}

export function deterministicCheck(
  input: RewriteRequestInput,
  facts: ExtractedFacts,
  rewrittenText: string,
  styleCard: StyleCard,
): DeterministicCheckResult {
  const missingFacts = missingRequiredFacts(input, rewrittenText).map(
    (fact) => fact.normalizedText,
  );
  const unsupportedFacts = detectUnsupportedFacts(input, rewrittenText).map(
    (fact) => fact.normalizedText,
  );
  const missingLockedFacts = preservesMustNotChange(rewrittenText, facts);
  const limitedPhrases = containsLimitedPhrase(rewrittenText, styleCard);
  const malformedIssues = hasDanglingClosing(rewrittenText)
    ? ["malformed:dangling_closing"]
    : [];
  const metaLanguageIssues = detectMetaLanguage(rewrittenText);
  const structureIssues = detectStructureIssues(input, rewrittenText);
  const issues = [
    ...missingFacts.map((fact) => `missing:${fact}`),
    ...unsupportedFacts.map((fact) => `unsupported:${fact}`),
    ...missingLockedFacts.map((fact) => `missing_locked:${fact}`),
    ...limitedPhrases.map((phrase) => `template_phrase:${phrase}`),
    ...malformedIssues,
    ...metaLanguageIssues,
    ...structureIssues,
  ];

  return {
    safe: issues.length === 0,
    issues,
    missingFacts: [...missingFacts, ...missingLockedFacts],
    unsupportedFacts,
  };
}

export function reviewerScorePasses(score: CandidateScore) {
  return (
    score.factual_accuracy >= 9 &&
    score.tone_appropriateness >= 8 &&
    score.low_template_feel >= 8
  );
}

export function llmFactCheckPasses(result: LlmFactCheckResult) {
  const hasConcreteIssue =
    result.has_added_new_facts ||
    result.has_removed_important_facts ||
    result.has_changed_names ||
    result.has_changed_dates_or_deadlines ||
    result.has_changed_conditions_or_policies ||
    result.tone_problem ||
    result.issues.length > 0 ||
    result.required_repairs.length > 0;

  return !hasConcreteIssue;
}
