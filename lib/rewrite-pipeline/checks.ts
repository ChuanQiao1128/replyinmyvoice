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

export type AdaptiveGateIssue = {
  issue: string;
  severity: "hard" | "soft";
  reason: string;
};

export type AdaptiveGateResult = {
  safe: boolean;
  issues: string[];
  blockingIssues: string[];
  softIssues: string[];
  adjudicatedIssues: AdaptiveGateIssue[];
  missingFacts: string[];
  unsupportedFacts: string[];
  deterministic: DeterministicCheckResult;
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

function issuePayload(issue: string) {
  const separator = issue.indexOf(":");
  return separator === -1 ? issue : issue.slice(separator + 1);
}

function escapeRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function isGenericSupportFooter(value: string) {
  const normalizedValue = normalize(value);
  return /\b(?:best regards\s+)?(?:customer support team|support team|customer service team)\b/i.test(
    normalizedValue,
  );
}

function containsMoney(value: string) {
  return /\b(?:nzd|usd|aud)?\s*\$\s*\d|\b\d+(?:\.\d+)?\s*(?:nzd|usd|aud)\b/i.test(
    value,
  );
}

function containsDateOrDeadline(value: string) {
  return /\b(?:monday|tuesday|wednesday|thursday|friday|saturday|sunday|january|february|march|april|may|june|july|august|september|october|november|december|today|tomorrow|yesterday|am|pm|a\.m\.|p\.m\.|\d{1,2}:\d{2}|business days?|days?|weeks?|months?)\b/i.test(
    value,
  );
}

function containsCount(value: string) {
  return /\b\d+\b/.test(value) ||
    /\b(?:one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\b/i.test(
      value,
    );
}

function containsPolicyOrPromise(value: string) {
  return /\b(?:refund|credit|charge|invoice|subscription|plan|seat|cancel|canceled|cancelled|transfer|enrollment|cohort|registration|availability|eligible|policy|partial credit|full credit|deadline|approve|approval|confirm|confirmed|not changed|no change|will not|must|required|lost|returned)\b/i.test(
    value,
  );
}

function containsConcreteName(
  value: string,
  facts: ExtractedFacts,
  input: RewriteRequestInput,
) {
  const normalizedValue = normalize(value);
  const names = [
    facts.recipient_name,
    ...facts.people_mentioned,
    ...Array.from(
      [input.messageToReplyTo, input.roughDraftReply]
        .join("\n")
        .matchAll(/\b(?:Hi|Hello|Dear)\s+([A-Z][A-Za-z.'-]+)\b/g),
      (match) => match[1],
    ),
  ]
    .map(normalize)
    .filter((name) => name.length > 1);

  return names.some((name) => normalizedValue === name || normalizedValue.includes(name));
}

function greetingNames(input: RewriteRequestInput) {
  return Array.from(
    [input.messageToReplyTo, input.roughDraftReply]
      .join("\n")
      .matchAll(/\b(?:Hi|Hello|Dear)\s+([A-Z][A-Za-z.'-]+)\b/g),
    (match) => normalize(match[1]),
  ).filter((name) => name.length > 1);
}

function isGreetingOnlyName(value: string, input: RewriteRequestInput) {
  const normalizedName = normalize(value);
  if (!normalizedName) {
    return false;
  }

  const normalizedSource = normalize(
    [input.messageToReplyTo, input.roughDraftReply].filter(Boolean).join("\n"),
  );
  const namePattern = escapeRegExp(normalizedName).replace(/\s+/g, "\\s+");
  const greetingPattern = new RegExp(
    `\\b(?:hi|hello|dear)\\s+${namePattern}\\b`,
    "g",
  );

  if (!greetingPattern.test(normalizedSource)) {
    return false;
  }

  const withoutGreeting = normalizedSource.replace(greetingPattern, " ");
  return !new RegExp(`\\b${namePattern}\\b`).test(withoutGreeting);
}

function isGreetingRecipientOnlyPayload(
  value: string,
  facts: ExtractedFacts,
  input: RewriteRequestInput,
) {
  if (
    containsMoney(value) ||
    containsDateOrDeadline(value) ||
    containsCount(value) ||
    containsPolicyOrPromise(value)
  ) {
    return false;
  }

  const normalizedValue = normalize(value);
  const names = [
    facts.recipient_name,
    ...greetingNames(input),
  ]
    .map(normalize)
    .filter((name) => name.length > 1);

  return names.some((name) => {
    if (!isGreetingOnlyName(name, input)) {
      return false;
    }

    if (normalizedValue === name) {
      return true;
    }

    const remaining = normalizedValue
      .replace(new RegExp(`\\b${escapeRegExp(name)}\\b`, "g"), " ")
      .replace(/\b(?:recipient|name|is|the|to|for|customer|client|user)\b/g, " ")
      .replace(/[^a-z0-9]+/g, " ")
      .trim();

    return remaining.length === 0;
  });
}

function classifyAdaptiveIssue({
  facts,
  input,
  issue,
}: {
  facts: ExtractedFacts;
  input: RewriteRequestInput;
  issue: string;
}): AdaptiveGateIssue {
  const payload = issuePayload(issue);
  const normalizedPayload = normalize(payload);

  if (isGenericSupportFooter(payload)) {
    return {
      issue,
      severity: "soft",
      reason: "Generic support-team footer is optional branding, not a critical fact.",
    };
  }

  if (issue.startsWith("missing_locked:")) {
    if (isGreetingRecipientOnlyPayload(payload, facts, input)) {
      return {
        issue,
        severity: "soft",
        reason: "Greeting-only recipient names are optional formatting, not critical business facts.",
      };
    }

    if (
      containsMoney(payload) ||
      containsDateOrDeadline(payload) ||
      containsCount(payload) ||
      containsPolicyOrPromise(payload) ||
      containsConcreteName(payload, facts, input)
    ) {
      return {
        issue,
        severity: "hard",
        reason: "Locked fact contains concrete business data.",
      };
    }

    return {
      issue,
      severity: "soft",
      reason: "Locked fact is phrasing or footer-level detail.",
    };
  }

  if (issue.startsWith("missing:")) {
    if (
      normalizedPayload === "sorry" ||
      normalizedPayload === "thank you" ||
      normalizedPayload === "thanks"
    ) {
      return {
        issue,
        severity: "soft",
        reason: "Polite phrase is not a critical fact.",
      };
    }

    if (isGreetingRecipientOnlyPayload(payload, facts, input)) {
      return {
        issue,
        severity: "soft",
        reason: "Greeting-only recipient names are optional formatting, not critical business facts.",
      };
    }

    return {
      issue,
      severity: "hard",
      reason: "Required extracted fact is missing.",
    };
  }

  if (issue.startsWith("unsupported:")) {
    if (
      normalizedPayload === "sorry" ||
      normalizedPayload === "thank you" ||
      normalizedPayload === "thanks"
    ) {
      return {
        issue,
        severity: "soft",
        reason: "Polite phrase is not an unsupported factual claim.",
      };
    }

    return {
      issue,
      severity: "hard",
      reason: "Output contains unsupported concrete information.",
    };
  }

  return {
    issue,
    severity: "hard",
    reason: "Structural, template, malformed, or meta-language issue.",
  };
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

export function adaptiveGateCheck(
  input: RewriteRequestInput,
  facts: ExtractedFacts,
  rewrittenText: string,
  styleCard: StyleCard,
): AdaptiveGateResult {
  const deterministic = deterministicCheck(input, facts, rewrittenText, styleCard);
  const adjudicatedIssues = deterministic.issues.map((issue) =>
    classifyAdaptiveIssue({ facts, input, issue }),
  );
  const blockingIssues = adjudicatedIssues
    .filter((issue) => issue.severity === "hard")
    .map((issue) => issue.issue);
  const softIssues = adjudicatedIssues
    .filter((issue) => issue.severity === "soft")
    .map((issue) => issue.issue);

  return {
    safe: blockingIssues.length === 0,
    issues: [...deterministic.issues],
    blockingIssues,
    softIssues,
    adjudicatedIssues,
    missingFacts: deterministic.missingFacts.filter((fact) =>
      blockingIssues.some((issue) => issue.endsWith(fact)),
    ),
    unsupportedFacts: deterministic.unsupportedFacts.filter((fact) =>
      blockingIssues.some((issue) => issue.endsWith(fact)),
    ),
    deterministic,
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
