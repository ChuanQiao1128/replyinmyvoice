import type { RewriteRequestInput } from "./validation";

export type RequiredFactCategory =
  | "person"
  | "role"
  | "contact"
  | "date"
  | "deadline"
  | "amount"
  | "count"
  | "task"
  | "ordered_step"
  | "constraint"
  | "promise"
  | "signoff"
  | "quoted_phrase";

export type RequiredFact = {
  id: string;
  text: string;
  normalizedText: string;
  category: RequiredFactCategory;
  source: "message" | "draft" | "both";
  required: true;
};

export type UnsupportedFact = {
  text: string;
  normalizedText: string;
  category: RequiredFactCategory;
};

const properNameStopWords = new Set([
  "a",
  "after",
  "april",
  "australia",
  "at",
  "based",
  "best",
  "before",
  "both",
  "can",
  "chrome",
  "clarify",
  "customer",
  "currently",
  "csv",
  "direct",
  "even",
  "friday",
  "for",
  "from",
  "have",
  "hello",
  "hi",
  "however",
  "if",
  "i",
  "i'd",
  "i'll",
  "i'm",
  "i've",
  "in",
  "it",
  "june",
  "last",
  "glad",
  "long",
  "also",
  "avoid",
  "do",
  "english",
  "keep",
  "may",
  "no",
  "northstar",
  "monday",
  "just",
  "looking",
  "missing",
  "my",
  "nzd",
  "our",
  "please",
  "preserve",
  "product",
  "quick",
  "reply",
  "right",
  "role",
  "safari",
  "saas",
  "sincerely",
  "she",
  "since",
  "ssO".toLowerCase(),
  "that",
  "thanks",
  "thank",
  "the",
  "they",
  "then",
  "this",
  "ticket",
  "one",
  "two",
  "three",
  "four",
  "five",
  "six",
  "thursday",
  "to",
  "tuesday",
  "use",
  "warm",
  "wednesday",
  "we",
  "what",
  "voice",
  "while",
]);

const phraseFacts: Array<{
  category: RequiredFactCategory;
  pattern: RegExp;
  text: string;
}> = [
  { category: "task", pattern: /\breading response\b/i, text: "reading response" },
  { category: "task", pattern: /\bvocabulary practice\b/i, text: "vocabulary practice" },
  {
    category: "task",
    pattern: /\bshort reflection paragraph from Friday\b/i,
    text: "short reflection paragraph from Friday",
  },
  { category: "deadline", pattern: /\bend of this week\b/i, text: "end of this week" },
  { category: "deadline", pattern: /\bfirst week of June\b/i, text: "first week of June" },
  { category: "deadline", pattern: /\bnext month\b/i, text: "next month" },
  { category: "constraint", pattern: /\bpartial credit\b/i, text: "partial credit" },
  { category: "constraint", pattern: /\bno grade penalty\b/i, text: "no grade penalty" },
  { category: "constraint", pattern: /\bmanager approval\b/i, text: "manager approval" },
  { category: "constraint", pattern: /\baccount credit\b/i, text: "account credit" },
  { category: "constraint", pattern: /\bbase subscription\b/i, text: "base subscription" },
  { category: "constraint", pattern: /\bbase plan\b/i, text: "base plan" },
  { category: "constraint", pattern: /\bbase plan did not change\b/i, text: "base plan did not change" },
  { category: "constraint", pattern: /\bnot be recalculated\b/i, text: "not be recalculated" },
  { category: "constraint", pattern: /\bnot a duplicate charge\b/i, text: "not a duplicate charge" },
  { category: "constraint", pattern: /\binvoice screenshot\b/i, text: "invoice screenshot" },
  { category: "constraint", pattern: /\bnot push for a decision\b/i, text: "not push for a decision" },
  { category: "constraint", pattern: /\bnot promising\b/i, text: "not promising" },
  { category: "constraint", pattern: /\blogo color has not changed\b/i, text: "logo color has not changed" },
  { category: "constraint", pattern: /\bone missing exit ticket\b/i, text: "one missing exit ticket" },
  {
    category: "constraint",
    pattern: /\bcannot enter the quiz score\b/i,
    text: "cannot enter the quiz score",
  },
  { category: "constraint", pattern: /\bcannot approve\b/i, text: "cannot approve" },
  { category: "task", pattern: /\bCustomer Success Associate\b/i, text: "Customer Success Associate" },
  { category: "task", pattern: /\bsummarize action items\b/i, text: "summarize action items" },
  { category: "task", pattern: /\bhelp center articles?\b/i, text: "help center articles" },
  { category: "task", pattern: /\bpayment flow\b/i, text: "payment flow" },
  { category: "task", pattern: /\bpricing language\b/i, text: "pricing language" },
  { category: "count", pattern: /\b12 additional seats\b/i, text: "12 additional seats" },
  { category: "task", pattern: /\bsecond quote\b/i, text: "second quote" },
  { category: "task", pattern: /\bretry worker is paused\b/i, text: "retry worker is paused" },
  { category: "task", pattern: /\bonboarding checklist\b/i, text: "onboarding checklist" },
  { category: "task", pattern: /\bhelp article links\b/i, text: "help article links" },
  { category: "task", pattern: /\blast three failed events\b/i, text: "last three failed events" },
  { category: "deadline", pattern: /\b2pm launch check\b/i, text: "2pm launch check" },
  { category: "count", pattern: /\btwo asked\b/i, text: "two asked" },
  { category: "task", pattern: /\bAPI fix\b/i, text: "API fix" },
  { category: "task", pattern: /\bQA script\b/i, text: "QA script" },
  { category: "task", pattern: /\bCSV import\b/i, text: "CSV import" },
  { category: "task", pattern: /\bSSO setup\b/i, text: "SSO setup" },
  { category: "task", pattern: /\bcorrected report\b/i, text: "corrected report" },
  { category: "task", pattern: /\bPO number\b/i, text: "PO number" },
  { category: "role", pattern: /\bfinance manager\b/i, text: "finance manager" },
  { category: "task", pattern: /\bvendor API timeout\b/i, text: "vendor API timeout" },
  { category: "task", pattern: /\blegal approval\b/i, text: "legal approval" },
  { category: "quoted_phrase", pattern: /\bold pilot workspace\b/i, text: "old pilot workspace" },
  { category: "quoted_phrase", pattern: /\breporting feature\b/i, text: "reporting feature" },
  { category: "quoted_phrase", pattern: /\bteam templates\b/i, text: "team templates" },
  { category: "quoted_phrase", pattern: /\brenewal proposal\b/i, text: "renewal proposal" },
  { category: "quoted_phrase", pattern: /\btwo plan options\b/i, text: "two plan options" },
  { category: "quoted_phrase", pattern: /\bfinance thread\b/i, text: "finance thread" },
  { category: "quoted_phrase", pattern: /\btwo other vendors\b/i, text: "two other vendors" },
  {
    category: "constraint",
    pattern: /\b(?:resent the invite twice|invite was resent twice)\b/i,
    text: "resent the invite twice",
  },
  { category: "quoted_phrase", pattern: /\bsource file arrived late\b/i, text: "source file arrived late" },
  { category: "quoted_phrase", pattern: /\bcustom tags column\b/i, text: "custom tags column" },
  { category: "quoted_phrase", pattern: /\bbilling report folder\b/i, text: "billing report folder" },
  { category: "quoted_phrase", pattern: /\bpause the campaign\b/i, text: "pause the campaign" },
];

const orderedStepStopWords = new Set([
  "after",
  "that",
  "then",
  "with",
  "work",
  "short",
]);

function normalize(value: string) {
  return value
    .toLowerCase()
    .replace(/[’]/g, "'")
    .replace(/\bcan['’]?t\b/g, "cannot")
    .replace(/\bcannot say for sure\b/g, "not promising")
    .replace(/\bcannot guarantee\b/g, "not promising")
    .replace(/\bcan not guarantee\b/g, "not promising")
    .replace(/\bcannot promise\b/g, "not promising")
    .replace(/\bcan not promise\b/g, "not promising")
    .replace(/\bhave not pinned down the root cause\b/g, "root cause is not confirmed")
    .replace(/\bhaven't pinned down the root cause\b/g, "root cause is not confirmed")
    .replace(/\bmanager gives the go-ahead\b/g, "manager approves")
    .replace(/\bon hold\b/g, "paused")
    .replace(/\bnot to cut down\b/g, "not cutting down")
    .replace(/\b12 more seats\b/g, "12 additional seats")
    .replace(/\banother quote\b/g, "second quote")
    .replace(/\btwo requested\b/g, "two asked")
    .replace(/\blogo color remains the same\b/g, "logo color has not changed")
    .replace(/\bbase plan remains the same\b/g, "base plan did not change")
    .replace(
      /\b(before|by|after)\s+the\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|noon|\d{1,2}(?::\d{2})?\s*(?:am|pm))\b/g,
      "$1 $2",
    )
    .replace(/\s+/g, " ")
    .replace(/\s+([,.])/g, "$1")
    .trim();
}

function factId(category: RequiredFactCategory, text: string) {
  return `${category}:${normalize(text)}`;
}

function sourceText(input: RewriteRequestInput) {
  return {
    message: input.messageToReplyTo ?? "",
    draft: [
      input.roughDraftReply,
      input.whatHappened,
      input.factsToPreserve,
    ]
      .filter(Boolean)
      .join("\n"),
  };
}

function addFact(
  facts: Map<string, RequiredFact>,
  text: string,
  category: RequiredFactCategory,
  source: RequiredFact["source"],
) {
  const cleaned = text.replace(/\s+/g, " ").trim().replace(/[,.]$/, "");
  if (!cleaned) {
    return;
  }

  const normalizedText = normalize(cleaned);
  const id = factId(category, cleaned);
  const existing = facts.get(id);

  facts.set(id, {
    id,
    text: existing?.text ?? cleaned,
    normalizedText,
    category,
    source: existing && existing.source !== source ? "both" : source,
    required: true,
  });
}

function cleanPersonCandidate(value: string) {
  return value
    .trim()
    .replace(/[,.]$/, "")
    .replace(/[’]/g, "'")
    .replace(/'s$/i, "")
    .replace(/\s+/g, " ");
}

function shouldKeepPersonCandidate(value: string) {
  const firstWord = normalize(value.split(/\s+/)[0] ?? "");
  return Boolean(value) && !properNameStopWords.has(firstWord);
}

function extractFromText(
  facts: Map<string, RequiredFact>,
  text: string,
  source: RequiredFact["source"],
) {
  const normalizedText = text.replace(/\s+/g, " ");

  const signoff = normalizedText.match(
    /\b(best regards|regards|sincerely),?\s+((?:Mr|Ms|Mrs|Dr)\.?\s+[A-Z][A-Za-z.'-]+|[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){0,2})\b/i,
  );
  if (signoff) {
    addFact(facts, `${signoff[1]} ${signoff[2]}`, "signoff", source);
  }

  for (const match of normalizedText.matchAll(
    /\b(?:Hi|Hello|Dear)\s+([A-Z][A-Za-z.'-]+)\b/g,
  )) {
    const candidate = cleanPersonCandidate(match[1]);
    if (shouldKeepPersonCandidate(candidate)) {
      addFact(facts, candidate, "person", source);
    }
  }

  for (const phrase of phraseFacts) {
    if (phrase.pattern.test(text)) {
      addFact(facts, phrase.text, phrase.category, source);
    }
  }

  for (const match of normalizedText.matchAll(/\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b/g)) {
    addFact(facts, match[0], "contact", source);
  }

  for (const match of normalizedText.matchAll(/\b(?:NZD\s*)?\$\s?\d+(?:\.\d{2})?\b/gi)) {
    addFact(facts, match[0], "amount", source);
  }

  for (const match of normalizedText.matchAll(/\b\d+(?:\.\d+)?%\b/g)) {
    addFact(facts, match[0], "amount", source);
  }

  for (const match of normalizedText.matchAll(
    /\b(?:January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)\s+\d{1,2}\b/gi,
  )) {
    addFact(facts, match[0], "date", source);
  }

  for (const match of normalizedText.matchAll(
    /\b(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)\b/gi,
  )) {
    addFact(facts, match[0], "date", source);
  }

  for (const match of normalizedText.matchAll(/\b\d{1,2}(?::\d{2})?\s*(?:am|pm)\b/gi)) {
    addFact(facts, match[0], "deadline", source);
  }

  for (const match of normalizedText.matchAll(
    /\b\d+\s+(?:active\s+seats?|regular\s+seats?|approved\s+seats?|temporary\s+(?:users?|contractors?)|contractor accounts?|rows?|blockers?|failed\s+events?|providers?|interviews?|teachers?|seats?|users?)\b/gi,
  )) {
    addFact(facts, match[0], "count", source);
  }

  for (const match of normalizedText.matchAll(
    /\b(?:before|by|after)\s+(?:the\s+)?(?:end of this week|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday|noon|\d{1,2}(?::\d{2})?\s*(?:am|pm))\b/gi,
  )) {
    addFact(facts, match[0], "deadline", source);
  }

  for (const match of normalizedText.matchAll(
    /\b((?:(?:Mr|Ms|Mrs|Dr)\.?\s+)?[A-Z][A-Za-z'-]+(?:\s+[A-Z][A-Za-z'-]+){0,2})\b/g,
  )) {
    const candidate = cleanPersonCandidate(match[1]);
    if (shouldKeepPersonCandidate(candidate)) {
      addFact(facts, candidate, "person", source);
    }
  }

  for (const sentence of splitSentences(text)) {
    if (
      /\bfirst\b.*\b(?:then|after that)\b/i.test(sentence) ||
      /^(?:then|after that)\b/i.test(sentence)
    ) {
      addFact(facts, sentence, "ordered_step", source);
    }

    if (/\b[A-Z][A-Za-z.'-]+\s+owns\s+/i.test(sentence)) {
      const parts = sentence.split(/\s*,?\s+and\s+|\s*,\s*/).filter(Boolean);
      for (const part of parts) {
        if (/\bowns\b/i.test(part)) {
          addFact(facts, part, "ordered_step", source);
        }
      }
    }
  }
}

function splitSentences(value: string) {
  return value
    .split(/[.!?]\s+|\n+/)
    .map((part) => part.trim())
    .filter(Boolean);
}

export function extractRequiredFacts(input: RewriteRequestInput) {
  const facts = new Map<string, RequiredFact>();
  const source = sourceText(input);

  extractFromText(facts, source.message, "message");
  extractFromText(facts, source.draft, "draft");

  return Array.from(facts.values());
}

function factPresent(fact: RequiredFact, rewrittenText: string) {
  const rewritten = normalize(rewrittenText);
  const factText = fact.normalizedText;
  const withoutArticle = factText.replace(/^(the|a|an)\s+/, "");
  const rewrittenWithoutPunctuation = rewritten.replace(/[,.]/g, "");
  const factWithoutPunctuation = factText.replace(/[,.]/g, "");

  if (rewritten.includes(factText) || rewritten.includes(withoutArticle)) {
    return true;
  }

  if (
    fact.category === "deadline" &&
    /^(before|by|after)\s+/.test(factText)
  ) {
    const deadlineTarget = factText.replace(/^(before|by|after)\s+/, "");
    if (rewritten.includes(deadlineTarget)) {
      return true;
    }
  }

  if (rewrittenWithoutPunctuation.includes(factWithoutPunctuation)) {
    return true;
  }

  if (fact.category === "ordered_step") {
    return factText
      .split(/\s+/)
      .map((word) => word.replace(/[^\w'-]/g, ""))
      .filter((word) => word.length > 3 && !orderedStepStopWords.has(word))
      .every((word) => rewritten.includes(word));
  }

  if (fact.category === "signoff") {
    const signerTokens = factText
      .split(/\s+/)
      .map((word) => word.replace(/[^\w'.-]/g, ""))
      .filter(
        (word) =>
          word.length > 1 &&
          !["best", "regards", "sincerely"].includes(word),
      );

    return signerTokens.length > 0 &&
      signerTokens.every((word) => rewritten.includes(word));
  }

  return false;
}

export function missingRequiredFacts(
  input: RewriteRequestInput,
  rewrittenText: string,
) {
  return extractRequiredFacts(input).filter(
    (fact) => !factPresent(fact, rewrittenText),
  );
}

function extractUnsupportedComparables(value: string) {
  const input = {
    scenario: "General reply",
    messageToReplyTo: "",
    roughDraftReply: value,
    audience: "",
    purpose: "",
    whatHappened: "",
    factsToPreserve: "",
    tone: "warm",
    tonePreset: "Warm",
  } satisfies RewriteRequestInput;

  return extractRequiredFacts(input).filter((fact) =>
    ["person", "contact", "date", "deadline", "amount", "count"].includes(fact.category),
  );
}

export function detectUnsupportedFacts(
  input: RewriteRequestInput,
  rewrittenText: string,
): UnsupportedFact[] {
  const sourceComparables = extractUnsupportedComparables(
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
  const sourceFacts = new Set(sourceComparables.map((fact) => fact.id));
  const sourceText = sourceComparables.map((fact) => fact.normalizedText).join(" ");

  return extractUnsupportedComparables(rewrittenText)
    .filter((fact) => {
      if (sourceFacts.has(fact.id)) {
        return false;
      }

      if (
        fact.category === "deadline" &&
        /^(before|by|after)\s+/.test(fact.normalizedText)
      ) {
        const deadlineTarget = fact.normalizedText.replace(
          /^(before|by|after)\s+/,
          "",
        );
        return !sourceText.includes(deadlineTarget);
      }

      return true;
    })
    .map((fact) => ({
      text: fact.text,
      normalizedText: fact.normalizedText,
      category: fact.category,
    }));
}

export { normalize as normalizeFactText };
