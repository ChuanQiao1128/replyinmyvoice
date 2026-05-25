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
    .replace(/\bupgrading\b/g, "upgrade")
    .replace(/\bupgrade date was\b/g, "upgrade date")
    .replace(/\bwithout confirmation\b/g, "until confirm")
    .replace(/\buntil you confirm\b/g, "until confirm")
    .replace(/\bplease confirm whether\b/g, "ask whether")
    .replace(/\bconfirm whether\b/g, "ask whether")
    .replace(/\bplease let me know which\b/g, "ask which")
    .replace(/\blet me know which\b/g, "ask which")
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

    if (hasAtomicLockedFactToken(payload, facts)) {
      return {
        issue,
        severity: "hard",
        reason: "Locked fact is missing concrete atomic data.",
      };
    }

    if (isProtectedMissingPayload(normalizedPayload)) {
      return {
        issue,
        severity: "hard",
        reason: "Locked fact is a protected product, topic, or policy-boundary phrase.",
      };
    }

    return {
      issue,
      severity: "soft",
      reason: "Locked fact is semantic prose and is deferred to the LLM fact check.",
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

    if (hasAtomicLockedFactToken(payload, facts)) {
      return {
        issue,
        severity: "hard",
        reason: "Required extracted fact is missing concrete atomic data.",
      };
    }

    if (isProtectedMissingPayload(normalizedPayload)) {
      return {
        issue,
        severity: "hard",
        reason: "Required fact is a protected product, topic, or policy-boundary phrase.",
      };
    }

    return {
      issue,
      severity: "soft",
      reason: "Required extracted fact is semantic prose and is deferred to the LLM fact check.",
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

function isProtectedMissingPayload(normalizedPayload: string) {
  if (
    /\b(?:cannot|not|without|unless|only if|must|requires?|required|needs?)\b/.test(
      normalizedPayload,
    )
  ) {
    return true;
  }

  return [
    "science museum field trip",
    "cracked finchlite desk lamp",
    "replacement shade",
    "saturday pantry volunteer shift",
    "packing station two",
    "three-hour packing shift",
    "product analyst final interview",
    "cardiology scheduling request",
    "travel reimbursement",
    "cannot refund more than",
    "cannot add",
    "cannot place",
    "cannot promise",
    "not promising travel reimbursement",
    "cannot book the appointment",
    "hold the",
    "approval cycle",
    "advanced sso",
    "discount",
    "order form",
    "patient portal",
    "clinician",
    "interpret",
    "dr. chen",
    "marked received",
    "lockbox code",
    "rent credit",
    "access confirmation",
    "cleanup shift",
  ].some((phrase) => normalizedPayload.includes(phrase));
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

type LockedFactAtomKind = "money" | "date" | "time" | "count" | "identifier" | "name";

type LockedFactAtom = {
  kind: LockedFactAtomKind;
  value: string;
  start: number;
  end: number;
};

const numberWords =
  "one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve";
const countUnits =
  "active seats?|regular seats?|approved seats?|temporary users?|temporary contractors?|contractor accounts?|business days?|failed events?|seats?|users?|licenses?|accounts?|rows?|items?|tickets?|assignments?|days?|weeks?|months?|shipments?|units?|people|students?|teachers?|contractors?|providers?|interviews?|volunteers?|mugs?";

function canonicalAtomValue(value: string) {
  return value
    .toLowerCase()
    .replace(/[’]/g, "'")
    .replace(/,/g, "")
    .replace(/\b(a|p)\.m\.?\b/g, "$1m")
    .replace(/\b(nzd|usd|aud)\s*\$\s*/g, "$1 $")
    .replace(/\$\s+/g, "$")
    .replace(/\s+/g, " ")
    .trim();
}

function rangesOverlap(a: Pick<LockedFactAtom, "start" | "end">, start: number, end: number) {
  return a.start < end && start < a.end;
}

function pushAtom(
  atoms: LockedFactAtom[],
  kind: LockedFactAtomKind,
  value: string,
  start: number,
  end: number,
) {
  const canonicalValue = canonicalAtomValue(value);
  if (!canonicalValue) {
    return;
  }

  if (
    atoms.some(
      (atom) => atom.kind === kind && canonicalAtomValue(atom.value) === canonicalValue,
    )
  ) {
    return;
  }

  atoms.push({ kind, value: canonicalValue, start, end });
}

function pushRegexAtoms(
  atoms: LockedFactAtom[],
  fact: string,
  kind: LockedFactAtomKind,
  pattern: RegExp,
  skipOverlaps = false,
) {
  for (const match of fact.matchAll(pattern)) {
    const value = match[0];
    const start = match.index ?? 0;
    const end = start + value.length;

    if (skipOverlaps && atoms.some((atom) => rangesOverlap(atom, start, end))) {
      continue;
    }

    pushAtom(atoms, kind, value, start, end);
  }
}

function properNameCandidates(facts: ExtractedFacts) {
  return [
    facts.recipient_name,
    facts.sender_name_or_role,
    ...facts.people_mentioned,
  ]
    .map((name) => name.trim())
    .filter((name) => name.length > 1);
}

function isIdentifierLikeNameToken(value: string) {
  const token = value.trim().replace(/[,.]$/, "");
  if (!token || /^(?:Mr|Ms|Mrs|Dr)\.?$/i.test(token)) {
    return false;
  }

  return /^[A-Z]-$/i.test(token) ||
    /-\d/.test(token) ||
    /\d/.test(token) ||
    !/^[A-Za-z][A-Za-z']*(?:-[A-Za-z][A-Za-z']*)*$/.test(token);
}

function knownNamePattern(name: string) {
  const pattern = name
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .map(escapeRegExp)
    .join("\\s+");

  return new RegExp(`\\b${pattern}\\b`, "gi");
}

function pushKnownNameAtoms(
  atoms: LockedFactAtom[],
  fact: string,
  facts: ExtractedFacts,
) {
  for (const name of properNameCandidates(facts)) {
    if (name.split(/\s+/).some(isIdentifierLikeNameToken)) {
      continue;
    }

    for (const match of fact.matchAll(knownNamePattern(name))) {
      const start = match.index ?? 0;
      const end = start + match[0].length;

      if (
        atoms.some(
          (atom) => atom.kind === "identifier" && rangesOverlap(atom, start, end),
        )
      ) {
        continue;
      }

      pushAtom(atoms, "name", name, start, end);
    }
  }

  pushRegexAtoms(
    atoms,
    fact,
    "name",
    /\b[A-Z][A-Za-z'-]+(?:\s+[A-Z][A-Za-z'-]+)+\b/g,
    true,
  );
}

function lockedFactAtoms(fact: string, facts: ExtractedFacts) {
  const atoms: LockedFactAtom[] = [];

  pushRegexAtoms(
    atoms,
    fact,
    "money",
    /\b(?:nzd|usd|aud)\s*\$\s*\d[\d,]*(?:\.\d{2})?\b|\$\s*\d[\d,]*(?:\.\d{2})?(?:\s*(?:nzd|usd|aud))?\b|\b\d[\d,]*(?:\.\d{2})?\s*(?:nzd|usd|aud)\b/gi,
  );
  pushRegexAtoms(
    atoms,
    fact,
    "identifier",
    /#[A-Z]{1,8}-\d+[A-Z0-9-]*\b|\b[A-Z]{1,8}-\d+[A-Z0-9-]*\b|\b[A-Z]{1,4}\d{3,}\b/g,
  );
  pushRegexAtoms(
    atoms,
    fact,
    "time",
    /\b\d{1,2}(?::\d{2})?\s*(?:a\.m\.?|p\.m\.?|am|pm)\b/gi,
  );
  pushRegexAtoms(
    atoms,
    fact,
    "date",
    /\b(?:january|february|march|april|may|june|july|august|september|sept|october|november|december|jan|feb|mar|apr|jun|jul|aug|sep|oct|nov|dec)\.?\s+\d{1,2}(?:st|nd|rd|th)?\b|\b\d{1,2}\s+(?:january|february|march|april|may|june|july|august|september|sept|october|november|december|jan|feb|mar|apr|jun|jul|aug|sep|oct|nov|dec)\b|\b(?:monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b/gi,
  );
  pushRegexAtoms(
    atoms,
    fact,
    "count",
    new RegExp(
      `\\b\\d+(?:\\.\\d+)?(?:\\s*[- ]\\s*(?:${countUnits}))?\\b`,
      "gi",
    ),
    true,
  );
  pushRegexAtoms(
    atoms,
    fact,
    "count",
    new RegExp(
      `\\b(?:${numberWords})(?:\\s+(?:to|-)\\s+(?:${numberWords}))?(?:\\s+(?:${countUnits}))\\b`,
      "gi",
    ),
    true,
  );
  pushKnownNameAtoms(atoms, fact, facts);

  return atoms;
}

function hasAtomicLockedFactToken(fact: string, facts: ExtractedFacts) {
  return lockedFactAtoms(fact, facts).length > 0;
}

function flexibleAtomPattern(value: string) {
  return value
    .split(/\s+/)
    .map((part) => {
      if (/^[a-z]+s$/.test(part)) {
        return `${escapeRegExp(part.slice(0, -1))}s?`;
      }

      return escapeRegExp(part);
    })
    .join("[\\s-]+");
}

function atomIsPresent(normalizedText: string, atom: LockedFactAtom) {
  if (atom.kind === "money") {
    return normalizedText.includes(atom.value);
  }

  return new RegExp(
    `(?:^|[^a-z0-9$#])${flexibleAtomPattern(atom.value)}(?=$|[^a-z0-9])`,
    "i",
  ).test(normalizedText);
}

export function preservesMustNotChange(text: string, facts: ExtractedFacts) {
  const normalizedText = canonicalAtomValue(text);

  return facts.facts_that_must_not_change.filter((fact) => {
    const atoms = lockedFactAtoms(fact, facts);

    return atoms.length > 0 && !atoms.every((atom) => atomIsPresent(normalizedText, atom));
  });
}

function hasDanglingClosing(text: string) {
  return /(?:^|\n)\s*(best regards|best|regards|sincerely|thanks|thank you),?\s*$/i.test(
    text.trim(),
  );
}

export function detectMetaLanguage(text: string) {
  const patterns: Array<[RegExp, string]> = [
    [
      /\b(?:is|was|were)\s+(?:referenced|mentioned|provided|stated)\b/i,
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
    [/\bthe user\b/i, "meta_language:user_context_reference"],
  ];

  return patterns
    .filter(([pattern]) => pattern.test(text))
    .map(([, issue]) => issue);
}

const unsafeGreetingLabels = new Set([
  "account",
  "acceptable",
  "admin",
  "annual",
  "billing",
  "basic",
  "cancellation",
  "core",
  "customer",
  "duplicate",
  "eligibility",
  "explain",
  "express",
  "finance",
  "new",
  "offer",
  "option",
  "order",
  "parent",
  "partial",
  "per",
  "decision",
  "quiz",
  "remember",
  "reopening",
  "solo",
  "standard",
  "student",
  "support",
  "team",
  "updated",
]);

function firstGreetingName(text: string) {
  return text.match(/^\s*(?:hi|hello|dear)\s+([^,\n]{2,40}),/i)?.[1]?.trim() ?? "";
}

function hasUnsupportedGreeting(input: RewriteRequestInput, rewrittenText: string) {
  const greeting = firstGreetingName(rewrittenText);
  if (!greeting) {
    return false;
  }

  const normalizedGreeting = normalize(greeting).replace(/[^a-z0-9\s'-]+/g, "");
  const greetingWords = normalizedGreeting.split(/\s+/).filter(Boolean);
  if (
    greetingWords.length === 0 ||
    greetingWords.some((word) => unsafeGreetingLabels.has(word))
  ) {
    return true;
  }

  const normalizedSource = normalize(
    [
      input.messageToReplyTo,
      input.roughDraftReply,
      input.audience,
      input.purpose,
      input.whatHappened,
      input.factsToPreserve,
    ]
      .filter(Boolean)
      .join("\n"),
  );

  return !greetingWords.every((word) => normalizedSource.includes(word));
}

function normalizedSentenceKey(value: string) {
  return normalize(value)
    .replace(/^(?:hi|hello|dear)(?:\s+[^,\n]*)?,?\s+/, "")
    .replace(/^(?:the|a|an)\s+/, "")
    .replace(/[^a-z0-9:$.'\s-]+/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function hasRepeatedFactSentence(text: string) {
  const seen = new Set<string>();
  const sentences = text
    .split(/[.!?]+(?:\s+|$)/)
    .map(normalizedSentenceKey)
    .filter((sentence) => sentence.split(/\s+/).filter(Boolean).length >= 4);

  for (const sentence of sentences) {
    if (seen.has(sentence)) {
      return true;
    }
    seen.add(sentence);
  }

  return false;
}

function hasDetachedSentenceFragment(text: string) {
  if (/\b[ap]\.m\.\s+(?:Because|Or)\b/.test(text)) {
    return true;
  }

  const protectedText = text.replace(/\b([ap])\.m\./gi, "$1__time_period__");

  return protectedText
    .replace(/\n+/g, " ")
    .split(/(?<=[.!?])\s+/)
    .map((sentence) => sentence.trim())
    .filter(Boolean)
    .some((sentence) => {
      if (/^(?:because|or)\b/i.test(sentence)) {
        return true;
      }

      return /^if\b/i.test(sentence) &&
        !sentence.includes(",") &&
        wordCount(sentence) <= 10;
    });
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

  if (hasUnsupportedGreeting(input, rewrittenText)) {
    issues.push("structure:unsupported_greeting");
  }

  if (hasRepeatedFactSentence(rewrittenText)) {
    issues.push("structure:repeated_fact");
  }

  if (hasDetachedSentenceFragment(rewrittenText)) {
    issues.push("structure:sentence_fragment");
  }

  if (/\bthe user\b/i.test(rewrittenText)) {
    issues.push("meta_language:user_context_reference");
  }

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
