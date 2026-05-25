import { appendFile, mkdir, readFile, rename, writeFile } from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";

import { detectUnsupportedFacts } from "../lib/fact-extraction";
import {
  getOpenAiCompatibleApiKey,
  getOpenAiCompatibleChatCompletionsUrl,
} from "../lib/openai-compatible";
import { createRewritePlan } from "../lib/rewrite-diagnosis";
import {
  parseRewriteEmailEvalCases,
  rewriteEmailEvalCaseToRequestInput,
  selectRewriteEmailEvalCases,
  type RewriteEmailEvalCase,
} from "../lib/rewrite-eval-cases";
import type { RewriteResponsePayload } from "../lib/rewrite-types";
import { getFactReconstructConfig } from "../lib/rewrite-pipeline/config";
import {
  FactReconstructQualityError,
  rewriteWithFactReconstruct,
} from "../lib/rewrite-pipeline/pipeline";
import type { ScenarioOption, TonePreset } from "../lib/rewrite-presets";
import { tonePresetToTone } from "../lib/rewrite-presets";
import { rewriteRequestSchema, type RewriteRequestInput } from "../lib/validation";

type EvalCase = {
  id: string;
  scenario: ScenarioOption;
  tonePreset: TonePreset;
  messageToReplyTo: string;
  roughDraftReply: string;
  audience?: string;
  purpose?: string;
  whatHappened?: string;
  factsToPreserve?: string;
  expectedFacts: string[];
  forbiddenClaims?: string[];
  qualityTargets?: string;
  expectedRewriteChallenges?: string;
};

type EvalResultRow = EvalCase & {
  diagnosisTags: string[];
  rewritePlan: string;
  rewrittenText: string;
  wordCount: number;
  charCount: number;
  draft: number | null;
  firstCandidate: number | null;
  repairCandidate: number | null;
  rewrite: number | null;
  change: number | null;
  candidateSignals: NonNullable<
    RewriteResponsePayload["optimization"]["candidateSignals"]
  >;
  rejectedReasons: string[];
  factsPreserved: boolean;
  missingFacts: string[];
  unsupportedFactsIntroduced: string[];
  forbiddenClaimViolations: string[];
  qualityDraftScore: number | null;
  qualityRewriteScore: number | null;
  qualityDelta: number | null;
  qualityNotes: string[];
  qualityRegression: boolean;
  qualityFailure: boolean;
  qualityFailureReason: string | null;
  customerUsablePass: boolean;
  strictSignalPass: boolean;
};

type SemanticJudgeResult = {
  facts: { passed: boolean; missing: string[] };
  forbiddenViolations: string[];
  quality: {
    draftScore: number | null;
    rewriteScore: number | null;
    delta: number | null;
    notes: string[];
  };
};

function draftOnlyCase(
  id: string,
  tonePreset: TonePreset,
  roughDraftReply: string,
  expectedFacts: string[],
): EvalCase {
  return {
    id,
    scenario: "General reply",
    tonePreset,
    messageToReplyTo: "",
    roughDraftReply,
    expectedFacts,
    forbiddenClaims: [],
  };
}

async function loadEnvLocal() {
  const envPath = path.join(process.cwd(), ".env.local");
  let content = "";

  try {
    content = await readFile(envPath, "utf8");
  } catch {
    return;
  }

  for (const line of content.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) {
      continue;
    }
    const match = trimmed.match(/^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/);
    if (!match) {
      continue;
    }
    const [, key, rawValue] = match;
    if (process.env[key]) {
      continue;
    }
    const value = rawValue
      .replace(/^['"]|['"]$/g, "")
      .replace(/\\n/g, "\n");
    process.env[key] = value;
  }
}

function points(value: number | null) {
  return value === null ? "unavailable" : `${value}%`;
}

function normalize(value: string) {
  return value
    .toLowerCase()
    .replace(/[’]/g, "'")
    .replace(/\bcan['’]?t\b/g, "cannot")
    .replace(/\bwon['’]?t\b/g, "will not")
    .replace(/\bhasn['’]?t\b/g, "has not")
    .replace(/\bhaven['’]?t\b/g, "have not")
    .replace(/\sisn['’]?t\b/g, "is not")
    .replace(/\bcannot say for sure\b/g, "not promising")
    .replace(/\bcannot guarantee\b/g, "not promising")
    .replace(/\bcan not guarantee\b/g, "not promising")
    .replace(/\bcannot promise\b/g, "not promising")
    .replace(/\bcan not promise\b/g, "not promising")
    .replace(/\bcannot be used for\b/g, "cannot cover")
    .replace(/\bcan not be used for\b/g, "cannot cover")
    .replace(/\bsubmitted\b/g, "submit")
    .replace(/\bturn in\b/g, "submit")
    .replace(/\buploading\b/g, "upload")
    .replace(/\bupgrading\b/g, "upgrade")
    .replace(/\bupgrade date was\b/g, "upgrade date")
    .replace(/\brenews?\s+on\b/g, "renewal date")
    .replace(/(\$\s*\d[\d,]*(?:\.\d{2})?)\/month\b/g, "$1 per month")
    .replace(/\bwill apply\b/g, "applies")
    .replace(
      /\bapplies?\s+(?:an?\s+)?official transcript\s+hold when (?:a\s+)?balance (?:goes|is) over\b/g,
      "official transcript hold applies to balances over",
    )
    .replace(
      /\bapplies?\s+(?:an?\s+)?hold when (?:a\s+)?balance (?:goes|is) over\b/g,
      "hold applies to balances over",
    )
    .replace(/\bsent by the ([a-z]+ \d{1,2}) deadline\b/g, "requested by $1")
    .replace(/\bsent by ([a-z]+ \d{1,2}) deadline\b/g, "requested by $1")
    .replace(/\brequires payment or approved correction\b/g, "payment or approved correction")
    .replace(
      /\bpay(?:ment)?\s+(?:the\s+)?(\$\s*\d[\d,]*(?:\.\d{2})?)\s+or have the charge corrected through an approved process\b/g,
      "payment or approved correction $1",
    )
    .replace(/\bplease let me know whether\b/g, "ask whether")
    .replace(/\blet me know whether\b/g, "ask whether")
    .replace(/\bplease let me know which\b/g, "ask which")
    .replace(/\blet me know which\b/g, "ask which")
    .replace(/\bconfirm whether\b/g, "ask whether")
    .replace(/\buntil you confirm\b/g, "until confirm")
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
    .replace(/\bprice is\b/g, "")
    .replace(/\brenewal date\b/g, "renews")
    .replace(/\bcan be sent\b/g, "can send")
    .replace(/\bwithout confirmation\b/g, "until confirm")
    .replace(/\bconfirmation\b/g, "confirm")
    .replace(/\bplease confirm whether\b/g, "ask whether")
    .replace(/\s+/g, " ")
    .trim();
}

const factTokenStopWords = new Set([
  "a",
  "an",
  "and",
  "are",
  "after",
  "at",
  "be",
  "by",
  "can",
  "do",
  "does",
  "did",
  "for",
  "explain",
  "from",
  "has",
  "have",
  "if",
  "in",
  "need",
  "is",
  "it",
  "make",
  "me",
  "not",
  "of",
  "on",
  "only",
  "or",
  "people",
  "person",
  "plus",
  "price",
  "region",
  "should",
  "student",
  "that",
  "they",
  "then",
  "still",
  "the",
  "to",
  "will",
  "was",
  "were",
  "with",
  "options",
  "date",
]);

function factTokens(value: string) {
  return normalize(value)
    .replace(/[^a-z0-9#$:.@'-]+/g, " ")
    .split(/\s+/)
    .map((token) => token.replace(/^[^a-z0-9#$@]+|[^a-z0-9]+$/g, ""))
    .filter((token) => token.length > 1 && !factTokenStopWords.has(token));
}

const monthNames = [
  "january",
  "february",
  "march",
  "april",
  "may",
  "june",
  "july",
  "august",
  "september",
  "october",
  "november",
  "december",
  "jan",
  "feb",
  "mar",
  "apr",
  "jun",
  "jul",
  "aug",
  "sep",
  "sept",
  "oct",
  "nov",
  "dec",
] as const;

const monthNameSet = new Set<string>(monthNames);
const weekdayNames = [
  "monday",
  "tuesday",
  "wednesday",
  "thursday",
  "friday",
  "saturday",
  "sunday",
] as const;
const weekdayNameSet = new Set<string>(weekdayNames);
const monthPattern = monthNames.join("|");
const weekdayPattern = weekdayNames.join("|");

type MoneyAnchor = { amount: string; currency: string | null };
type FactAnchor =
  | { kind: "money"; amount: string; currency: string | null }
  | { kind: "date" | "weekday" | "time" | "identifier" | "email" | "acronym" | "number" | "percentage" | "proper"; value: string };

type TextAnchorIndex = {
  money: MoneyAnchor[];
  dates: Set<string>;
  weekdays: Set<string>;
  times: Set<string>;
  identifiers: Set<string>;
  emails: Set<string>;
  acronyms: Set<string>;
  numbers: Set<string>;
  percentages: Set<string>;
};

const numberWordValues = new Map<string, string>([
  ["zero", "0"],
  ["one", "1"],
  ["two", "2"],
  ["three", "3"],
  ["four", "4"],
  ["five", "5"],
  ["six", "6"],
  ["seven", "7"],
  ["eight", "8"],
  ["nine", "9"],
  ["ten", "10"],
  ["eleven", "11"],
  ["twelve", "12"],
  ["thirteen", "13"],
  ["fourteen", "14"],
  ["fifteen", "15"],
  ["sixteen", "16"],
  ["seventeen", "17"],
  ["eighteen", "18"],
  ["nineteen", "19"],
  ["twenty", "20"],
  ["thirty", "30"],
  ["forty", "40"],
  ["fifty", "50"],
  ["sixty", "60"],
  ["seventy", "70"],
  ["eighty", "80"],
  ["ninety", "90"],
]);

const semanticProseStopWords = new Set([
  ...factTokenStopWords,
  "am",
  "pm",
  "a.m",
  "p.m",
  "dear",
  "hi",
  "hello",
  "our",
  "your",
  "private",
  "order",
  "ticket",
  "invoice",
]);
semanticProseStopWords.delete("options");

const properAnchorStopWords = new Set([
  ...factTokenStopWords,
  ...monthNames,
  ...weekdayNames,
  "annual",
  "billing",
  "corrected",
  "late",
  "offer",
  "order",
  "private",
]);

const semanticTokenGroups = [
  ["ask", "asked", "request", "requested", "note"],
  ["available", "either", "meet", "offer", "option", "options"],
  ["confirm", "confirmation", "confirmed"],
  ["delay", "delayed", "back"],
  ["escalate", "escalation"],
  ["fail", "failure", "failed"],
  ["form", "permission", "slip"],
  ["received", "sent", "send", "ship"],
  ["renew", "renewal", "renewed", "renews"],
  ["submit", "submitted", "turn", "turned", "upload", "uploaded"],
];

const semanticTokenCanonicals = new Map<string, string>();
for (const [canonical, ...aliases] of semanticTokenGroups) {
  for (const alias of [canonical, ...aliases]) {
    semanticTokenCanonicals.set(alias, canonical);
  }
}

function escapeRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function dedupeAnchors(anchors: FactAnchor[]) {
  const seen = new Set<string>();

  return anchors.filter((anchor) => {
    const key =
      anchor.kind === "money"
        ? `${anchor.kind}:${anchor.currency ?? ""}:${anchor.amount}`
        : `${anchor.kind}:${anchor.value}`;
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

function normalizeNumeric(value: string) {
  const compact = value.replace(/,/g, "");
  const [whole, decimal] = compact.split(".");
  const normalizedWhole = whole.replace(/^0+(?=\d)/, "") || "0";

  return decimal ? `${normalizedWhole}.${decimal}` : normalizedWhole;
}

function extractMoneyAnchors(value: string): MoneyAnchor[] {
  const anchors: MoneyAnchor[] = [];
  const add = (currency: string | undefined, amount: string | undefined) => {
    if (!amount) {
      return;
    }
    anchors.push({
      amount: normalizeNumeric(amount),
      currency: currency?.toLowerCase() ?? null,
    });
  };

  for (const match of value.matchAll(/\b(nzd|usd|aud)\s*\$\s*(\d[\d,]*(?:\.\d{2})?)/gi)) {
    add(match[1], match[2]);
  }
  for (const match of value.matchAll(/\$\s*(\d[\d,]*(?:\.\d{2})?)\s*(?:\b(nzd|usd|aud)\b)?/gi)) {
    add(match[2], match[1]);
  }
  for (const match of value.matchAll(/\b(\d[\d,]*(?:\.\d{2})?)\s*(nzd|usd|aud)\b/gi)) {
    add(match[2], match[1]);
  }

  const seen = new Set<string>();
  return anchors.filter((anchor) => {
    const key = `${anchor.currency ?? ""}:${anchor.amount}`;
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

function extractPercentageAnchors(value: string) {
  const anchors = new Set<string>();

  for (const match of value.matchAll(/\b(\d+(?:\.\d+)?)\s*(?:%|percent)\b/gi)) {
    anchors.add(normalizeNumeric(match[1]));
  }

  return anchors;
}

function extractDateAnchors(value: string) {
  const anchors = new Set<string>();
  const monthDay = new RegExp(
    `\\b(${monthPattern})\\s+(\\d{1,2})(?:st|nd|rd|th)?\\b`,
    "gi",
  );
  const dayMonth = new RegExp(
    `\\b(\\d{1,2})(?:st|nd|rd|th)?\\s+(${monthPattern})\\b`,
    "gi",
  );

  for (const match of value.matchAll(monthDay)) {
    anchors.add(`${match[1].toLowerCase()}:${Number(match[2])}`);
  }
  for (const match of value.matchAll(dayMonth)) {
    anchors.add(`${match[2].toLowerCase()}:${Number(match[1])}`);
  }

  return anchors;
}

function extractWeekdayAnchors(value: string) {
  const anchors = new Set<string>();
  const weekday = new RegExp(`\\b(${weekdayPattern})\\b`, "gi");

  for (const match of value.matchAll(weekday)) {
    anchors.add(match[1].toLowerCase());
  }

  return anchors;
}

function extractTimeAnchors(value: string) {
  const anchors = new Set<string>();

  for (const match of value.matchAll(/\b(\d{1,2})(?::(\d{2}))?\s*(a\.?m\.?|p\.?m\.?|am|pm)\b/gi)) {
    const hour = String(Number(match[1]));
    const minute = match[2] ?? "00";
    const meridiem = match[3].toLowerCase().startsWith("a") ? "am" : "pm";
    anchors.add(`${hour}:${minute.padStart(2, "0")} ${meridiem}`);
  }

  return anchors;
}

function extractIdentifierAnchors(value: string) {
  const anchors = new Set<string>();
  const addMatches = (pattern: RegExp) => {
    for (const match of value.matchAll(pattern)) {
      anchors.add(match[0].toLowerCase().replace(/[.,;:]$/, ""));
    }
  };

  addMatches(/#[A-Za-z0-9-]*\d[A-Za-z0-9-]*/g);
  addMatches(/\b[A-Z]{1,8}-[A-Z0-9-]*\d[A-Z0-9-]*\b/gi);
  addMatches(/\b[A-Z]+\d[A-Z0-9-]*\b/gi);

  return anchors;
}

function extractEmailAnchors(value: string) {
  const anchors = new Set<string>();

  for (const match of value.matchAll(/\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b/g)) {
    anchors.add(match[0].toLowerCase());
  }

  return anchors;
}

function extractAcronymAnchors(value: string) {
  const anchors = new Set<string>();

  for (const match of value.matchAll(/\b[A-Z]{2,}(?:-[A-Z0-9]+)*\b/g)) {
    const acronym = match[0].toLowerCase();
    if (!["am", "pm", "nzd", "usd", "aud"].includes(acronym)) {
      anchors.add(acronym);
    }
  }

  return anchors;
}

function scrubHardAnchors(value: string) {
  return value
    .replace(/\b(?:nzd|usd|aud)\s*\$\s*\d[\d,]*(?:\.\d{2})?/gi, " ")
    .replace(/\$\s*\d[\d,]*(?:\.\d{2})?\s*(?:\b(?:nzd|usd|aud)\b)?/gi, " ")
    .replace(/\b\d[\d,]*(?:\.\d{2})?\s*(?:nzd|usd|aud)\b/gi, " ")
    .replace(/\b\d+(?:\.\d+)?\s*(?:%|percent)\b/gi, " ")
    .replace(new RegExp(`\\b(?:${weekdayPattern}),?\\s+`, "gi"), " ")
    .replace(new RegExp(`\\b(?:${monthPattern})\\s+\\d{1,2}(?:st|nd|rd|th)?\\b`, "gi"), " ")
    .replace(new RegExp(`\\b\\d{1,2}(?:st|nd|rd|th)?\\s+(?:${monthPattern})\\b`, "gi"), " ")
    .replace(/\b\d{1,2}(?::\d{2})?\s*(?:a\.?m\.?|p\.?m\.?|am|pm)\b/gi, " ")
    .replace(/#[A-Za-z0-9-]*\d[A-Za-z0-9-]*/g, " ")
    .replace(/\b[A-Z]{1,8}-[A-Z0-9-]*\d[A-Z0-9-]*\b/gi, " ")
    .replace(/\b[A-Z]+\d[A-Z0-9-]*\b/gi, " ");
}

function extractStandaloneNumberAnchors(value: string) {
  const anchors = new Set<string>();
  const scrubbed = scrubHardAnchors(value);

  for (const match of scrubbed.matchAll(/\b\d[\d,]*(?:\.\d+)?\b/g)) {
    anchors.add(normalizeNumeric(match[0]));
  }
  for (const match of scrubbed.matchAll(/\b(?:zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety)\b/gi)) {
    const valueForWord = numberWordValues.get(match[0].toLowerCase());
    if (valueForWord) {
      anchors.add(valueForWord);
    }
  }

  return anchors;
}

function firstWordIndex(value: string) {
  const match = value.match(/\b[A-Za-z][A-Za-z.'-]*\b/);

  return match?.index ?? -1;
}

function extractProperNameAnchors(rawFact: string) {
  const anchors = new Set<string>();
  const firstIndex = firstWordIndex(rawFact);

  for (const match of rawFact.matchAll(/\b[A-Z][A-Za-z.'-]*\b/g)) {
    const rawToken = match[0].replace(/[.'-]+$/g, "");
    const token = rawToken.toLowerCase();
    const isFirstWord = match.index === firstIndex;
    if (
      token.length <= 1 ||
      monthNameSet.has(token) ||
      weekdayNameSet.has(token) ||
      properAnchorStopWords.has(token) ||
      /^[a-z]{2,}$/.test(token) && rawToken === rawToken.toUpperCase()
    ) {
      continue;
    }

    const afterToken = rawFact.slice(match.index + match[0].length);
    const beforeToken = rawFact.slice(0, match.index);
    const hasAdjacentCapital =
      /^\s+[A-Z][A-Za-z.'-]*/.test(afterToken) ||
      /[A-Z][A-Za-z.'-]*\s+$/.test(beforeToken);
    if (!isFirstWord || hasAdjacentCapital) {
      anchors.add(token);
    }
  }

  return anchors;
}

function extractFactAnchors(rawFact: string, normalizedFact: string) {
  const anchors: FactAnchor[] = [];

  for (const money of extractMoneyAnchors(normalizedFact)) {
    anchors.push({ kind: "money", ...money });
  }
  for (const value of extractPercentageAnchors(normalizedFact)) {
    anchors.push({ kind: "percentage", value });
  }
  for (const value of extractDateAnchors(normalizedFact)) {
    anchors.push({ kind: "date", value });
  }
  for (const value of extractWeekdayAnchors(normalizedFact)) {
    anchors.push({ kind: "weekday", value });
  }
  for (const value of extractTimeAnchors(normalizedFact)) {
    anchors.push({ kind: "time", value });
  }
  for (const value of extractIdentifierAnchors(rawFact)) {
    anchors.push({ kind: "identifier", value });
  }
  for (const value of extractEmailAnchors(normalizedFact)) {
    anchors.push({ kind: "email", value });
  }
  for (const value of extractAcronymAnchors(rawFact)) {
    anchors.push({ kind: "acronym", value });
  }
  for (const value of extractStandaloneNumberAnchors(normalizedFact)) {
    anchors.push({ kind: "number", value });
  }
  for (const value of extractProperNameAnchors(rawFact)) {
    anchors.push({ kind: "proper", value });
  }

  return dedupeAnchors(anchors);
}

function buildTextAnchorIndex(normalizedText: string): TextAnchorIndex {
  return {
    money: extractMoneyAnchors(normalizedText),
    dates: extractDateAnchors(normalizedText),
    weekdays: extractWeekdayAnchors(normalizedText),
    times: extractTimeAnchors(normalizedText),
    identifiers: extractIdentifierAnchors(normalizedText),
    emails: extractEmailAnchors(normalizedText),
    acronyms: extractAcronymAnchors(normalizedText.toUpperCase()),
    numbers: extractStandaloneNumberAnchors(normalizedText),
    percentages: extractPercentageAnchors(normalizedText),
  };
}

function hasWord(normalizedText: string, word: string) {
  return new RegExp(`\\b${escapeRegExp(word)}\\b`).test(normalizedText);
}

function anchorPresent(
  normalizedText: string,
  index: TextAnchorIndex,
  anchor: FactAnchor,
) {
  if (anchor.kind === "money") {
    return index.money.some(
      (candidate) =>
        candidate.amount === anchor.amount &&
        (!anchor.currency || candidate.currency === anchor.currency),
    );
  }
  if (anchor.kind === "date") {
    return index.dates.has(anchor.value);
  }
  if (anchor.kind === "weekday") {
    return index.weekdays.has(anchor.value);
  }
  if (anchor.kind === "time") {
    return index.times.has(anchor.value);
  }
  if (anchor.kind === "identifier") {
    return index.identifiers.has(anchor.value);
  }
  if (anchor.kind === "email") {
    return index.emails.has(anchor.value);
  }
  if (anchor.kind === "acronym") {
    return index.acronyms.has(anchor.value) || hasWord(normalizedText, anchor.value);
  }
  if (anchor.kind === "number") {
    return index.numbers.has(anchor.value);
  }
  if (anchor.kind === "percentage") {
    return index.percentages.has(anchor.value);
  }

  return hasWord(normalizedText, anchor.value);
}

function dedupeDoubleConsonant(value: string) {
  return /([bcdfghjklmnpqrstvwxyz])\1$/.test(value)
    ? value.slice(0, -1)
    : value;
}

function stemVariants(token: string) {
  const variants = new Set([token]);
  const add = (value: string) => {
    if (value.length > 1) {
      variants.add(value);
      variants.add(dedupeDoubleConsonant(value));
    }
  };

  if (token.endsWith("ation") && token.length > 7) {
    add(token.slice(0, -5));
  }
  if (token.endsWith("ment") && token.length > 6) {
    add(token.slice(0, -4));
  }
  if (token.endsWith("ing") && token.length > 5) {
    const base = token.slice(0, -3);
    add(base);
    add(`${base}e`);
  }
  if (token.endsWith("ied") && token.length > 5) {
    add(`${token.slice(0, -3)}y`);
  }
  if (token.endsWith("ed") && token.length > 4) {
    const base = token.slice(0, -2);
    add(base);
    add(`${base}e`);
  }
  if (token.endsWith("ies") && token.length > 4) {
    add(`${token.slice(0, -3)}y`);
  }
  if (token.endsWith("es") && token.length > 4) {
    add(token.slice(0, -2));
  }
  if (token.endsWith("s") && token.length > 3) {
    add(token.slice(0, -1));
  }
  if (token.endsWith("d") && token.length > 4) {
    const base = token.slice(0, -1);
    add(base);
    add(`${base}e`);
  }
  if (token.endsWith("al") && token.length > 5) {
    add(token.slice(0, -2));
  }
  if (token.endsWith("ly") && token.length > 5) {
    add(token.slice(0, -2));
  }
  if (token.endsWith("ure") && token.length > 5) {
    add(token.slice(0, -3));
  }

  return variants;
}

function semanticKeysForToken(token: string) {
  const keys = new Set<string>();

  for (const variant of stemVariants(token)) {
    keys.add(variant);
    const canonical = semanticTokenCanonicals.get(variant);
    if (canonical) {
      keys.add(canonical);
    }
  }

  const numberValue = numberWordValues.get(token);
  if (numberValue) {
    keys.add(numberValue);
  }

  return keys;
}

function proseTokens(value: string) {
  return scrubHardAnchors(value)
    .toLowerCase()
    .replace(/[^a-z0-9'-]+/g, " ")
    .split(/[\s-]+/)
    .map((token) => token.replace(/^'+|'+$/g, ""))
    .filter(
      (token) =>
        token.length > 1 &&
        !semanticProseStopWords.has(token) &&
        !monthNameSet.has(token) &&
        !weekdayNameSet.has(token),
    );
}

function semanticTextKeySet(normalizedText: string) {
  const keys = new Set<string>();

  for (const token of normalizedText
    .replace(/[^a-z0-9'-]+/g, " ")
    .split(/[\s-]+/)
    .map((part) => part.replace(/^'+|'+$/g, ""))
    .filter(Boolean)) {
    for (const key of semanticKeysForToken(token)) {
      keys.add(key);
    }
  }

  return keys;
}

function proseCoveragePass(normalizedText: string, normalizedFact: string) {
  const tokens = Array.from(new Set(proseTokens(normalizedFact)));
  if (!tokens.length) {
    return true;
  }

  const textKeys = semanticTextKeySet(normalizedText);
  const matched = tokens.filter((token) =>
    Array.from(semanticKeysForToken(token)).some((key) => textKeys.has(key)),
  ).length;
  const required = tokens.length <= 2 ? tokens.length : Math.ceil(tokens.length * 0.6);

  return matched >= required;
}

function includesFact(normalizedText: string, fact: string) {
  const normalizedFact = normalize(fact);
  if (normalizedText.includes(normalizedFact)) {
    return true;
  }

  const anchors = extractFactAnchors(fact, normalizedFact);
  const textAnchorIndex = buildTextAnchorIndex(normalizedText);

  if (
    anchors.some((anchor) => !anchorPresent(normalizedText, textAnchorIndex, anchor))
  ) {
    return false;
  }

  return proseCoveragePass(normalizedText, normalizedFact);
}

function factCheck(text: string, facts: string[]) {
  const normalized = normalize(text);
  const missing = facts.filter((fact) => !includesFact(normalized, fact));

  return {
    passed: missing.length === 0,
    missing,
  };
}

type EvalUnsupportedFact = {
  text: string;
  normalizedText: string;
};

type EvalTemporalClaim = {
  text: string;
  normalizedText: string;
};

const evalTemporalTimePattern =
  String.raw`(?:noon|\d{1,2}(?::\d{2})?\s*(?:a\.?m\.?|p\.?m\.?|am|pm))`;
const evalTemporalDateRefPattern = String.raw`(?:today|tomorrow|tonight|${weekdayPattern}|(?:${monthPattern})\s+\d{1,2})`;

function normalizeEvalTemporalDateRef(value: string) {
  return normalize(value.replace(/,/g, " "));
}

function normalizeEvalTemporalTime(value: string) {
  const normalized = normalize(value).replace(/\./g, "");
  if (normalized === "noon") {
    return "noon";
  }

  const match = normalized.match(/^(\d{1,2})(?::(\d{2}))?\s*([ap])m$/);
  if (!match) {
    return normalized;
  }

  return `${Number(match[1])}:${(match[2] ?? "00").padStart(2, "0")} ${match[3]}m`;
}

function addEvalTemporalClaim(
  claims: Map<string, EvalTemporalClaim>,
  text: string,
  normalizedText: string,
) {
  const normalized = normalize(normalizedText);
  if (!claims.has(normalized)) {
    claims.set(normalized, {
      text: text.replace(/\s+/g, " ").trim(),
      normalizedText: normalized,
    });
  }
}

function extractEvalTemporalClaims(value: string) {
  const claims = new Map<string, EvalTemporalClaim>();
  const dateThenTime = new RegExp(
    `\\b(${evalTemporalDateRefPattern})\\b\\s*(?:,\\s*)?\\s+(at|by|before|after)\\s+(${evalTemporalTimePattern})\\b`,
    "gi",
  );
  const boundaryDateTime = new RegExp(
    `\\b(by|before|after)\\s+(${evalTemporalDateRefPattern})\\b(?:\\s+at)?\\s+(${evalTemporalTimePattern})\\b`,
    "gi",
  );
  const dateRange = new RegExp(
    `\\b(${evalTemporalDateRefPattern})\\b\\s*(?:,\\s*)?\\s+between\\s+(${evalTemporalTimePattern})\\s+and\\s+(${evalTemporalTimePattern})\\b`,
    "gi",
  );
  const timeOnDate = new RegExp(
    `\\b(${evalTemporalTimePattern})\\s+on\\s+(${evalTemporalDateRefPattern})\\b`,
    "gi",
  );
  const optionTimesOnDate = new RegExp(
    `\\b(${evalTemporalTimePattern})\\s+or\\s+(${evalTemporalTimePattern})\\s+on\\s+(${evalTemporalDateRefPattern})\\b`,
    "gi",
  );
  const bareBoundaryTime = new RegExp(
    `\\b(by|before|after)\\s+(${evalTemporalTimePattern})\\b`,
    "gi",
  );

  for (const match of value.matchAll(dateThenTime)) {
    const date = normalizeEvalTemporalDateRef(match[1]);
    const prep = normalize(match[2]);
    const time = normalizeEvalTemporalTime(match[3]);
    addEvalTemporalClaim(claims, match[0], `${date} ${prep} ${time}`);
  }

  for (const match of value.matchAll(boundaryDateTime)) {
    const prep = normalize(match[1]);
    const date = normalizeEvalTemporalDateRef(match[2]);
    const time = normalizeEvalTemporalTime(match[3]);
    addEvalTemporalClaim(claims, match[0], `${prep} ${date} at ${time}`);
  }

  for (const match of value.matchAll(dateRange)) {
    const date = normalizeEvalTemporalDateRef(match[1]);
    const start = normalizeEvalTemporalTime(match[2]);
    const end = normalizeEvalTemporalTime(match[3]);
    addEvalTemporalClaim(claims, match[0], `${date} between ${start} and ${end}`);
  }

  for (const match of value.matchAll(timeOnDate)) {
    const time = normalizeEvalTemporalTime(match[1]);
    const date = normalizeEvalTemporalDateRef(match[2]);
    addEvalTemporalClaim(claims, match[0], `${date} at ${time}`);
  }

  for (const match of value.matchAll(optionTimesOnDate)) {
    const first = normalizeEvalTemporalTime(match[1]);
    const second = normalizeEvalTemporalTime(match[2]);
    const date = normalizeEvalTemporalDateRef(match[3]);
    addEvalTemporalClaim(claims, `${match[3]} at ${match[1]}`, `${date} at ${first}`);
    addEvalTemporalClaim(claims, `${match[3]} at ${match[2]}`, `${date} at ${second}`);
  }

  for (const match of value.matchAll(bareBoundaryTime)) {
    const prep = normalize(match[1]);
    const time = normalizeEvalTemporalTime(match[2]);
    addEvalTemporalClaim(claims, match[0], `${prep} ${time}`);
  }

  return Array.from(claims.values());
}

function detectEvalUnsupportedFacts(
  input: RewriteRequestInput,
  rewrittenText: string,
): EvalUnsupportedFact[] {
  const rawSource = [
    input.messageToReplyTo,
    input.roughDraftReply,
    input.audience,
    input.purpose,
    input.whatHappened,
    input.factsToPreserve,
  ]
    .filter(Boolean)
    .join("\n");
  const sourceTemporalClaims = new Set(
    extractEvalTemporalClaims(rawSource).map((claim) => claim.normalizedText),
  );
  const temporalUnsupported = extractEvalTemporalClaims(rewrittenText)
    .filter((claim) => !sourceTemporalClaims.has(claim.normalizedText))
    .map((claim): EvalUnsupportedFact => ({
      text: claim.text,
      normalizedText: claim.normalizedText,
    }));
  const deterministicUnsupported = detectUnsupportedFacts(input, rewrittenText).map(
    (fact) => ({
      text: fact.text,
      normalizedText: fact.normalizedText,
    }),
  );
  const seen = new Set<string>();

  return [...deterministicUnsupported, ...temporalUnsupported].filter((fact) => {
    if (seen.has(fact.normalizedText)) {
      return false;
    }
    seen.add(fact.normalizedText);
    return true;
  });
}

function mergeSemanticJudgeWithDeterministicFactCheck(
  rewrittenText: string,
  _mustKeep: string[],
  semanticResult: SemanticJudgeResult,
): SemanticJudgeResult {
  if (!semanticResult.facts.missing.length) {
    return semanticResult;
  }

  const deterministic = factCheck(rewrittenText, semanticResult.facts.missing);
  const deterministicMissing = new Set(deterministic.missing);
  const missing = semanticResult.facts.missing.filter((fact) =>
    deterministicMissing.has(fact),
  );

  return {
    ...semanticResult,
    facts: {
      passed: missing.length === 0,
      missing,
    },
  };
}

function emptyQualityJudgeResult() {
  return {
    draftScore: null,
    rewriteScore: null,
    delta: null,
    notes: [],
  };
}

export const __evalScenarioTestUtils = {
  customerUsablePassFromGates,
  factCheck,
  factTokens,
  forbiddenClaimViolations,
  detectEvalUnsupportedFacts,
  includesFact,
  isQualityRegression,
  mergeSemanticJudgeWithDeterministicFactCheck,
  normalize,
  semanticFactJudge,
};

function forbiddenClaimViolations(text: string, claims: string[]) {
  const normalizedText = normalize(text);

  return claims.filter((claim) => {
    const normalizedClaim = normalize(claim);

    if (/\brefund\b/.test(normalizedClaim)) {
      return /\b(?:can|could|will|we'll|offer|process|approve|approved|available|issue)\b.{0,30}\brefund\b|\brefund\b.{0,30}\b(?:available|approved|processed|issued|offer)\b/.test(
        normalizedText,
      ) && !/\brefund\b.{0,24}\bnot approved\b|\brefund has not been approved\b/.test(
        normalizedText,
      );
    }

    if (/\bpromise|guarantee\b/.test(normalizedClaim)) {
      return /\b(?:promise|guarantee|guaranteed|definitely|absolutely|will)\b.{0,50}\b(?:completion|access|approval|approved|eligible)\b/.test(
        normalizedText,
      ) && !/\b(?:cannot|can't|not)\b.{0,20}\b(?:promise|guarantee)\b/.test(
        normalizedText,
      );
    }

    if (/\bwrong|careless|blame\b/.test(normalizedClaim)) {
      return /\b(?:wrong|careless|should have|failed to|blame)\b/.test(
        normalizedText,
      );
    }

    return false;
  });
}

const DEFAULT_SEMANTIC_JUDGE_MAX_ATTEMPTS = 3;
const DEFAULT_SEMANTIC_JUDGE_RETRY_DELAY_MS = 750;
const QUALITY_REGRESSION_DROP_THRESHOLD = 2;
const QUALITY_REGRESSION_MIN_SCORE = 6;

class NonRetryableSemanticJudgeError extends Error {}

function parsePositiveIntegerEnv(
  name: string,
  fallback: number,
  options: { allowZero?: boolean } = {},
) {
  const value = process.env[name];
  if (value === undefined) {
    return fallback;
  }

  const parsed = Number(value);
  const minimum = options.allowZero ? 0 : 1;
  return Number.isInteger(parsed) && parsed >= minimum ? parsed : fallback;
}

function sleep(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function isRetryableSemanticJudgeStatus(status: number) {
  return status === 408 || status === 409 || status === 429 || status >= 500;
}

async function fetchSemanticJudgeResponse(prompt: string) {
  const maxAttempts = parsePositiveIntegerEnv(
    "EVAL_SEMANTIC_JUDGE_MAX_ATTEMPTS",
    DEFAULT_SEMANTIC_JUDGE_MAX_ATTEMPTS,
  );
  const baseDelayMs = parsePositiveIntegerEnv(
    "EVAL_SEMANTIC_JUDGE_RETRY_DELAY_MS",
    DEFAULT_SEMANTIC_JUDGE_RETRY_DELAY_MS,
    { allowZero: true },
  );
  let lastError: unknown = null;

  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    try {
      const response = await fetch(getOpenAiCompatibleChatCompletionsUrl(), {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${getOpenAiCompatibleApiKey()}`,
        },
        body: JSON.stringify({
          model:
            process.env.OPENAI_MODEL_CHEAP_STRUCTURED ??
            process.env.OPENAI_MODEL ??
            "deepseek-chat",
          messages: [{ role: "user", content: prompt }],
          temperature: 0,
          response_format: { type: "json_object" },
        }),
      });

      if (response.ok) {
        return response;
      }

      const text = await response.text();
      const error = new Error(`semanticFactJudge API error ${response.status}: ${text}`);
      if (
        attempt === maxAttempts ||
        !isRetryableSemanticJudgeStatus(response.status)
      ) {
        throw new NonRetryableSemanticJudgeError(error.message);
      }
      lastError = error;
    } catch (error) {
      if (error instanceof NonRetryableSemanticJudgeError) {
        throw error;
      }
      if (attempt === maxAttempts) {
        throw error;
      }
      lastError = error;
    }

    if (baseDelayMs > 0) {
      await sleep(baseDelayMs * attempt);
    }
  }

  throw lastError instanceof Error ? lastError : new Error(String(lastError));
}

function isQualityRegression(
  draftScore: number | null,
  rewriteScore: number | null,
) {
  if (rewriteScore === null) {
    return false;
  }

  if (rewriteScore < QUALITY_REGRESSION_MIN_SCORE) {
    return true;
  }

  return (
    draftScore !== null &&
    rewriteScore <= draftScore - QUALITY_REGRESSION_DROP_THRESHOLD
  );
}

function customerUsablePassFromGates({
  hasRewrittenText,
  factsPassed,
  unsupportedFactsCount,
  forbiddenViolationsCount,
  qualityFailure,
  signalNotWorse,
  qualityRegression,
}: {
  hasRewrittenText: boolean;
  factsPassed: boolean;
  unsupportedFactsCount: number;
  forbiddenViolationsCount: number;
  qualityRegression: boolean;
  qualityFailure: boolean;
  signalNotWorse: boolean;
  qualityRegression: boolean;
}) {
  return (
    hasRewrittenText &&
    factsPassed &&
    unsupportedFactsCount === 0 &&
    forbiddenViolationsCount === 0 &&
    !qualityFailure &&
    signalNotWorse &&
    !qualityRegression
  );
}

async function semanticFactJudge(
  inputDraft: string,
  rewrittenText: string,
  mustKeep: string[],
  mustNotClaim: string[],
  qualityTargets?: string,
): Promise<SemanticJudgeResult> {
  if (mustKeep.length === 0 && mustNotClaim.length === 0) {
    return {
      facts: { passed: true, missing: [] },
      forbiddenViolations: [],
      quality: emptyQualityJudgeResult(),
    };
  }

  const mustKeepLines =
    mustKeep.map((fact, index) => `${index + 1}. ${fact}`).join("\n") ||
    "(none)";
  const mustNotClaimLines =
    mustNotClaim.map((claim, index) => `${index + 1}. ${claim}`).join("\n") ||
    "(none)";

  const prompt = [
    "You are a strict single-input draft rewrite judge. Evaluate whether the rewritten text improves the user's original draft without changing facts or boundaries.",
    "",
    "ORIGINAL INPUT DRAFT:",
    inputDraft,
    "",
    "REWRITTEN TEXT:",
    rewrittenText,
    "",
    "MUST-KEEP FACTS - answer PASS if the fact is semantically expressed. Exact wording is not required; a clear paraphrase is fine. Answer FAIL if the fact is missing or contradicted:",
    mustKeepLines,
    "",
    "MUST-NOT-CLAIM assertions - answer FAIL if the rewrite makes or implies the claim. Answer PASS if the claim is absent:",
    mustNotClaimLines,
    "",
    "QUALITY TARGETS - score the original input draft and the rewritten text from 1 to 10 for the stated targets. Reward clarity, warmth, structure, conciseness, actionability, human voice, and necessary formatting. Penalize overpromising, excessive apology, template-like voice, and weaker boundaries.",
    qualityTargets?.trim() || "(general quality improvement)",
    "",
    "Respond in valid JSON only, no markdown fences:",
    `{"mustKeep":[{"fact":"...","verdict":"PASS","reason":"..."}],"mustNotClaim":[{"claim":"...","verdict":"PASS","reason":"..."}],"quality":{"draftScore":1,"rewriteScore":1,"notes":["..."]}}`,
  ].join("\n");

  const response = await fetchSemanticJudgeResponse(prompt);

  const data = (await response.json()) as {
    choices?: Array<{ message?: { content?: string } }>;
  };
  const content = data.choices?.[0]?.message?.content ?? "{}";
  const parsed = JSON.parse(content) as {
    mustKeep?: Array<{ fact?: string; verdict?: string }>;
    mustNotClaim?: Array<{ claim?: string; verdict?: string }>;
    quality?: {
      draftScore?: number;
      rewriteScore?: number;
      notes?: unknown;
    };
  };

  const missing = (parsed.mustKeep ?? [])
    .filter((item) => item.verdict === "FAIL")
    .map((item) => item.fact)
    .filter((fact): fact is string => Boolean(fact));
  const forbiddenViolations = (parsed.mustNotClaim ?? [])
    .filter((item) => item.verdict === "FAIL")
    .map((item) => item.claim)
    .filter((claim): claim is string => Boolean(claim));
  const draftScore =
    typeof parsed.quality?.draftScore === "number" &&
    Number.isFinite(parsed.quality.draftScore)
      ? Math.max(1, Math.min(10, parsed.quality.draftScore))
      : null;
  const rewriteScore =
    typeof parsed.quality?.rewriteScore === "number" &&
    Number.isFinite(parsed.quality.rewriteScore)
      ? Math.max(1, Math.min(10, parsed.quality.rewriteScore))
      : null;
  const qualityNotes = Array.isArray(parsed.quality?.notes)
    ? parsed.quality.notes
        .filter((note): note is string => typeof note === "string")
        .slice(0, 5)
    : [];

  return {
    facts: { passed: missing.length === 0, missing },
    forbiddenViolations,
    quality: {
      draftScore,
      rewriteScore,
      delta:
        draftScore === null || rewriteScore === null
          ? null
          : Number((rewriteScore - draftScore).toFixed(1)),
      notes: qualityNotes,
    },
  };
}

function wordCount(value: string) {
  return value.trim().split(/\s+/).filter(Boolean).length;
}

function signalPass(
  draft: number | null,
  rewrite: number | null,
  naturalnessThreshold: number,
) {
  if (draft === null || rewrite === null) {
    return false;
  }

  if (draft <= naturalnessThreshold) {
    return rewrite <= draft;
  }

  return rewrite <= naturalnessThreshold;
}

type RewriteQualityFailure = FactReconstructQualityError;

const draftOnlyCases: EvalCase[] = [
  draftOnlyCase(
    "draft-only-01-teacher-jordan",
    "Warm",
    "Hi Monica,\n\nJordan is missing the reading response, vocabulary practice, and the short reflection paragraph from Friday. He should start with the reading response and vocabulary practice because those can be done quickly. Then he can work on the reflection paragraph. If he turns everything in by the end of this week, I can still accept it for partial credit.\n\nBest regards,\nMs. Carter",
    ["Monica", "Jordan", "reading response", "vocabulary practice", "Friday", "end of this week", "partial credit", "Ms. Carter"],
  ),
  draftOnlyCase(
    "draft-only-01b-teacher-jordan-long-polished",
    "Warm",
    [
      "Hi Monica,",
      "Thank you for reaching out and sharing your concerns about Jordan's grade. I understand how stressful this situation can feel, and I appreciate you checking in so we can work together to help him catch up.",
      "Right now, Jordan is missing three assignments from the past two weeks: the reading response, the vocabulary practice, and the short reflection paragraph from last Friday. He continues to participate in class discussions, and I want to recognize that his verbal contributions have been thoughtful. However, the missing written work is beginning to have a significant impact on his overall grade.",
      "I recommend that Jordan begin with the reading response and vocabulary practice, since those should be the quickest to complete. After that, he can work on the short reflection paragraph. If he submits all three assignments by this Friday at 5 p.m., I will still accept them for partial credit.",
      "I also want to clarify that he does not need to redo any work he has already completed. This only applies to the three missing assignments listed above. If he has any questions about the instructions, he is welcome to come see me during lunch on Tuesday or Thursday, and I would be happy to walk him through what is expected.",
      "Thank you again for your support and for helping Jordan take responsibility for catching up. I believe he can get back on track if he completes the missing work this week and builds a more consistent routine moving forward.",
      "Best regards,",
      "Ms. Carter",
    ].join("\n\n"),
    [
      "Monica",
      "Jordan",
      "three assignments",
      "past two weeks",
      "reading response",
      "vocabulary practice",
      "short reflection paragraph",
      "last Friday",
      "class discussions",
      "this Friday at 5 p.m.",
      "partial credit",
      "does not need to redo",
      "Tuesday",
      "Thursday",
      "Ms. Carter",
    ],
  ),
  draftOnlyCase(
    "draft-only-02-teacher-kai",
    "Direct",
    "Hi Lena, Kai's grade changed because two participation activities and one exit ticket are still missing. The course policy says late work may not receive full credit, but he can submit the exit ticket before class tomorrow and I will review it. I am not promising a grade change yet.",
    ["Lena", "Kai", "two participation activities", "one exit ticket", "course policy", "before class tomorrow", "not promising"],
  ),
  draftOnlyCase(
    "draft-only-03-teacher-amelia",
    "Warm",
    "Hi Mr. Ortiz, Amelia did complete the lab notes, but I have not received the reflection paragraph from Tuesday. She can bring it to lunch study hall on Thursday. If she submits it then, I can mark it late but complete.",
    ["Mr. Ortiz", "Amelia", "lab notes", "reflection paragraph", "Tuesday", "Thursday", "late but complete"],
  ),
  draftOnlyCase(
    "draft-only-04-teacher-ravi",
    "Direct",
    "Hi Priya, Ravi was absent for the quiz on Monday and still needs to schedule the make-up. The available times are Wednesday at 8:15am or Friday after school. I cannot enter the quiz score until he completes it.",
    ["Priya", "Ravi", "quiz", "Monday", "Wednesday at 8:15am", "Friday after school", "cannot enter the quiz score"],
  ),
  draftOnlyCase(
    "draft-only-05-teacher-sam",
    "Warm",
    "Hi Dana, Sam's project is strong, but the bibliography is missing three sources. If he adds the sources by Friday, I can grade the final version instead of the draft version. The presentation date is still May 22.",
    ["Dana", "Sam", "bibliography", "three sources", "Friday", "final version", "May 22"],
  ),
  draftOnlyCase(
    "draft-only-06-teacher-noah",
    "Direct",
    "Hi Ms. Nguyen, Noah turned in the worksheet but did not attach the graph. Please ask him to upload the graph in Canvas by 6pm tonight. I will not need a new worksheet, only the graph file.",
    ["Ms. Nguyen", "Noah", "worksheet", "graph", "Canvas", "6pm tonight", "only the graph file"],
  ),
  draftOnlyCase(
    "draft-only-07-teacher-maya",
    "Warm",
    "Hi Alex, Maya can still join the science fair group, but the permission slip is due Thursday morning. The group is meeting in Room 18 after school. She should bring the signed slip and her project idea.",
    ["Alex", "Maya", "science fair group", "permission slip", "Thursday morning", "Room 18", "project idea"],
  ),
  draftOnlyCase(
    "draft-only-08-teacher-omar",
    "Direct",
    "Hi Jordan, Omar has improved his reading log, but the March 14 response is still missing. He can turn in that response for half credit. The quiz retake is separate and needs to be scheduled with me.",
    ["Jordan", "Omar", "reading log", "March 14", "half credit", "quiz retake", "scheduled with me"],
  ),
  draftOnlyCase(
    "draft-only-09-support-tax",
    "Warm",
    "Hi Sarah, the tax line increased because the billing address changed to Australia. The base subscription is unchanged. The new tax amount starts on June 1, and the May invoice will not be recalculated.",
    ["Sarah", "tax line", "Australia", "base subscription", "June 1", "May invoice", "not be recalculated"],
  ),
  draftOnlyCase(
    "draft-only-10-support-refund",
    "Direct",
    "Hi Eli, the refund window ended May 10. We can offer account credit, but manager approval is required before I can apply it. I cannot promise the credit today.",
    ["Eli", "refund window", "May 10", "account credit", "manager approval", "cannot promise"],
  ),
  draftOnlyCase(
    "draft-only-11-support-export",
    "Warm",
    "Hi Mina, the April CSV export is missing the custom tags column for the Northeast region. The underlying campaign data is still safe. We are checking the export job and will send a corrected file before Monday at 10am if the check confirms the issue.",
    ["Mina", "April CSV export", "custom tags column", "Northeast region", "data is still safe", "Monday at 10am"],
  ),
  draftOnlyCase(
    "draft-only-12-support-seat-count",
    "Direct",
    "Hi Priya, the usage report shows 18 active seats, but the renewal only approved 15 regular seats. The extra NZD $126 appears tied to three temporary contractors who were active during May. The base plan did not change.",
    ["Priya", "18 active seats", "15 regular seats", "NZD $126", "three temporary contractors", "May", "base plan did not change"],
  ),
  draftOnlyCase(
    "draft-only-13-support-login",
    "Warm",
    "Hi Mina, keep signing in with mina@northstar.example. If you still land in the old pilot workspace, this is probably a workspace association issue, not a new invite issue. We already resent the invite twice, so support should check the account link next.",
    ["Mina", "mina@northstar.example", "old pilot workspace", "workspace association issue", "resent the invite twice", "account link"],
  ),
  draftOnlyCase(
    "draft-only-14-support-plan-change",
    "Direct",
    "Hi Arun, the Starter plan credit and the Team plan charge are shown as separate lines because the plan changed on May 3. That usually means proration, not a duplicate charge. I still need the invoice screenshot before I can confirm the final amount.",
    ["Arun", "Starter plan credit", "Team plan charge", "May 3", "proration", "not a duplicate charge", "invoice screenshot"],
  ),
  draftOnlyCase(
    "draft-only-15-support-notifications",
    "Warm",
    "Hi Claire, ticket #4821 is still open for duplicate notifications. The delivery logs are being reviewed, but the root cause is not confirmed yet. I will send the next status note before noon so your team can decide whether to pause the campaign.",
    ["Claire", "ticket #4821", "duplicate notifications", "delivery logs", "root cause is not confirmed", "before noon", "pause the campaign"],
  ),
  draftOnlyCase(
    "draft-only-16-support-import",
    "Direct",
    "Hi Omar, the import failed because row 14 has an invalid date format. Rows 1 through 13 were not saved. Please fix row 14 and upload the file again. Do not delete the existing project.",
    ["Omar", "row 14", "invalid date format", "Rows 1 through 13", "not saved", "upload the file again", "Do not delete"],
  ),
  draftOnlyCase(
    "draft-only-16b-implementation-schedule-regression",
    "Warm",
    [
      "Hi Morgan,",
      "Thank you for sending over the revised onboarding timeline and the notes from yesterday's implementation call. I reviewed the update with our operations lead this morning, and I want to clarify a few points before we confirm the schedule with the broader team.",
      "The overall timeline still looks workable, but the training session originally planned for Tuesday, 4 June will need to move. Our payroll team has a quarter-end review that afternoon, and three of the supervisors who need the training will not be available. The best replacement time on our side is Thursday, 6 June at 10:30 a.m. If that does not work for your team, the backup option is Friday, 7 June after 2 p.m.",
      "Please do not change the go-live date yet. We are still aiming for Monday, 17 June, as long as the user-permission issue is resolved by the end of next week. The main blocker is that the warehouse supervisors can see the dashboard, but they cannot approve shift changes. We also noticed that the export file is missing the approved by column, which our finance team needs for the weekly reconciliation report.",
      "For now, please keep the project scope the same. We are not adding the SMS reminder feature in this phase, and we are not ready to approve the additional NZD $480 setup fee for that feature. I do want the team to document it as a possible phase-two item, because our regional managers may ask about it later.",
      "Could you send us an updated implementation note that includes the proposed training time change, the current permission issue for warehouse supervisors, the missing approved by column in the export, a clear note that SMS reminders are not part of this phase, and confirmation that the go-live date is still Monday, 17 June unless the permission issue is not resolved?",
      "Please keep the tone calm and practical. I do not want the message to sound like we are blaming your team. I just want the internal stakeholders to understand what changed, what is still blocked, and what we are not approving yet.",
      "Best,",
      "Avery",
    ].join("\n\n"),
    [
      "Morgan",
      "Tuesday, 4 June",
      "Thursday, 6 June at 10:30 a.m.",
      "Friday, 7 June after 2 p.m.",
      "Monday, 17 June",
      "user-permission issue",
      "warehouse supervisors",
      "cannot approve shift changes",
      "approved by column",
      "weekly reconciliation report",
      "SMS reminders are not part of this phase",
      "NZD $480",
      "phase-two item",
      "regional managers",
      "Avery",
    ],
  ),
  draftOnlyCase(
    "draft-only-17-sales-renewal",
    "Warm",
    "Hi Jordan, thanks for looking at the renewal proposal. I will send a shorter summary of the two plan options for your finance thread. I know your team is also comparing two other vendors, and the earliest decision point is the first week of June.",
    ["Jordan", "renewal proposal", "two plan options", "finance thread", "two other vendors", "first week of June"],
  ),
  draftOnlyCase(
    "draft-only-18-sales-demo",
    "Direct",
    "Hi Leah, we can move the demo to Thursday at 3pm. I will keep the agenda focused on reporting, team templates, and the approval workflow. I will not include pricing unless you ask for it.",
    ["Leah", "Thursday at 3pm", "reporting", "team templates", "approval workflow", "will not include pricing"],
  ),
  draftOnlyCase(
    "draft-only-19-sales-proposal",
    "Warm",
    "Hi Mateo, I attached the revised proposal with the implementation timeline from our May 12 call. Section three has the pricing language, and section five has the rollout notes. Please send comments by Friday if your legal team wants changes.",
    ["Mateo", "May 12", "section three", "pricing language", "section five", "rollout notes", "Friday", "legal team"],
  ),
  draftOnlyCase(
    "draft-only-20-sales-checkin",
    "Direct",
    "Hi Nora, I will not push for a decision this week. Your team is still comparing Vendor A and Vendor B, and finance asked for the security questionnaire first. I can send the questionnaire today and follow up next Tuesday.",
    ["Nora", "not push for a decision", "Vendor A", "Vendor B", "security questionnaire", "today", "next Tuesday"],
  ),
  draftOnlyCase(
    "draft-only-21-sales-expansion",
    "Warm",
    "Hi Devon, the expansion quote includes 12 additional seats starting July 1. It does not include the analytics add-on yet. If you want analytics included, I can send a second quote after your manager approves it.",
    ["Devon", "12 additional seats", "July 1", "does not include the analytics add-on", "second quote", "manager approves"],
  ),
  draftOnlyCase(
    "draft-only-22-client-design",
    "Direct",
    "Hi Ava, the homepage mockup is ready, but the mobile version still needs one spacing check. I can send desktop today and mobile by Wednesday morning. The logo color has not changed.",
    ["Ava", "homepage mockup", "mobile version", "spacing check", "desktop today", "Wednesday morning", "logo color has not changed"],
  ),
  draftOnlyCase(
    "draft-only-23-client-invoice",
    "Warm",
    "Hi Ben, invoice #317 was sent on April 30, but the PO number was missing. I have attached a corrected copy with PO-8842. The amount is still NZD $2,450 and the due date is May 20.",
    ["Ben", "invoice #317", "April 30", "PO-8842", "NZD $2,450", "May 20"],
  ),
  draftOnlyCase(
    "draft-only-24-client-delay",
    "Direct",
    "Hi Grace, the source file arrived late, so the report will not be ready by noon. I can send the clean version by 4pm Friday after one quality check. The dashboard numbers are unchanged.",
    ["Grace", "source file arrived late", "not be ready by noon", "4pm Friday", "one quality check", "dashboard numbers are unchanged"],
  ),
  draftOnlyCase(
    "draft-only-25-work-launch",
    "Warm",
    "Quick update: the payment flow passed the smoke test, the onboarding checklist is done, and the help article links are live. I am still reviewing the last three failed webhook events before the 2pm launch check.",
    ["payment flow", "smoke test", "onboarding checklist", "help article links", "last three failed webhook events", "2pm launch check"],
  ),
  draftOnlyCase(
    "draft-only-26-work-blockers",
    "Direct",
    "Nina owns the API fix, Omar owns the QA script, and both are due before the Friday demo. The only blocker is the vendor API timeout. I will post another update after the 11am retry.",
    ["Nina", "API fix", "Omar", "QA script", "Friday demo", "vendor API timeout", "11am retry"],
  ),
  draftOnlyCase(
    "draft-only-27-work-research",
    "Warm",
    "We finished six teacher interviews this week. Four teachers said the onboarding copy felt too technical, and two asked for a sample response before signing up. I recommend updating the first screen before Wednesday's test.",
    ["six teacher interviews", "four teachers", "two asked", "sample response", "first screen", "Wednesday"],
  ),
  draftOnlyCase(
    "draft-only-28-work-screenshots",
    "Direct",
    "The revised screenshots are delayed because the updated design source file arrived late this morning. They still need one quality check, especially the pricing table in section three and the partner logo on the final slide. Target is 4pm Friday.",
    ["revised screenshots", "source file arrived late", "one quality check", "pricing table", "section three", "partner logo", "final slide", "4pm Friday"],
  ),
  draftOnlyCase(
    "draft-only-29-work-board",
    "Warm",
    "The board packet is ready except for the finance chart. The chart needs the April revenue number and the May forecast. I can send the final PDF by 9am tomorrow if finance confirms the numbers today.",
    ["board packet", "finance chart", "April revenue number", "May forecast", "9am tomorrow", "finance confirms"],
  ),
  draftOnlyCase(
    "draft-only-30-work-handoff",
    "Direct",
    "The handoff is complete for authentication and billing. Search is not included in this release. I added the rollback notes to the release doc and tagged Maya for the support FAQ review.",
    ["authentication", "billing", "Search is not included", "rollback notes", "release doc", "Maya", "support FAQ review"],
  ),
  draftOnlyCase(
    "draft-only-31-work-meeting",
    "Warm",
    "I need to move our 1:1 from Tuesday to Wednesday because the client workshop now overlaps. I can do Wednesday at 10am or 2pm. The hiring plan feedback is ready, so this is only a scheduling change.",
    ["1:1", "Tuesday", "Wednesday", "client workshop", "10am", "2pm", "hiring plan feedback"],
  ),
  draftOnlyCase(
    "draft-only-32-work-incident",
    "Direct",
    "The incident summary is ready. Impact was limited to 14 checkout attempts between 8:05am and 8:22am. No successful payments were duplicated. The retry worker is paused until Alex reviews the queue logs.",
    ["incident summary", "14 checkout attempts", "8:05am", "8:22am", "No successful payments were duplicated", "retry worker is paused", "Alex"],
  ),
  draftOnlyCase(
    "draft-only-33-general-workshop",
    "Warm",
    "Families, the Saturday workshop is moving to Room 204 because of library maintenance. The start time is still 6:30pm. We will still cover scholarship forms, supporting documents, and the application timeline.",
    ["Saturday", "Room 204", "library maintenance", "6:30pm", "scholarship forms", "supporting documents", "application timeline"],
  ),
  draftOnlyCase(
    "draft-only-34-general-volunteer",
    "Direct",
    "Hi team, the volunteer roster has 32 people confirmed for Saturday. We still need two check-in leads and one person for the supply table. Please reply by Thursday noon if you can take one of those roles.",
    ["32 people", "Saturday", "two check-in leads", "one person for the supply table", "Thursday noon"],
  ),
  draftOnlyCase(
    "draft-only-35-cover-program",
    "Warm",
    "I am applying for the Program Manager role. In my current job, I coordinate 32 volunteers, prepare monthly partner updates, manage weekend workshop schedules, and track attendance numbers for grant reports. I care about education access but do not want the letter to sound generic.",
    ["Program Manager", "32 volunteers", "monthly partner updates", "weekend workshop schedules", "attendance numbers", "grant reports", "education access"],
  ),
  draftOnlyCase(
    "draft-only-36-cover-support",
    "Direct",
    "I am applying for the Support Specialist role. I answer customer questions by email and chat, summarize recurring issues for the product team, and update help center articles when the same question keeps coming up. Please do not make me sound senior.",
    ["Support Specialist", "email and chat", "recurring issues", "product team", "help center articles", "do not make me sound senior"],
  ),
  draftOnlyCase(
    "draft-only-37-general-policy",
    "Warm",
    "Participants who already submitted questions do not need to send them again. The agenda is unchanged, and the room change only affects this Saturday's workshop. Printed scholarship drafts are still welcome.",
    ["already submitted questions", "do not need to send them again", "agenda is unchanged", "Saturday's workshop", "Printed scholarship drafts"],
  ),
  draftOnlyCase(
    "draft-only-38-general-apology",
    "Direct",
    "Hi Taylor, I missed your message on Monday and should have replied sooner. I can review the contract notes by Thursday afternoon. I cannot approve the final language until legal confirms clause 7.",
    ["Taylor", "Monday", "Thursday afternoon", "cannot approve", "legal", "clause 7"],
  ),
  draftOnlyCase(
    "draft-only-39-general-neighbor",
    "Warm",
    "Hi Chris, thanks for checking about the noise. The work crew is scheduled from 9am to 3pm on Friday only. They are replacing the back fence panels, not cutting down the tree.",
    ["Chris", "9am to 3pm", "Friday only", "back fence panels", "not cutting down the tree"],
  ),
  draftOnlyCase(
    "draft-only-40-general-event",
    "Direct",
    "The event reminder should say doors open at 5:30pm, the panel starts at 6pm, and parking is in Lot C. Do not mention catering because food is not confirmed yet.",
    ["doors open at 5:30pm", "panel starts at 6pm", "Lot C", "Do not mention catering", "food is not confirmed"],
  ),
];

const cases: EvalCase[] = [
  ...draftOnlyCases,
  {
    id: "blank-01-partner-update",
    scenario: "Blank / custom",
    tonePreset: "Direct",
    messageToReplyTo: "",
    roughDraftReply:
      "I am writing to provide an update regarding the partner onboarding packet. The revised document has now been completed and is available for your review. Please note that section three contains the updated pricing language, and section five includes the implementation timeline that was discussed during the call on May 12. Kindly review the attached document and provide any feedback at your earliest convenience so that we may proceed accordingly.",
    expectedFacts: ["section three", "section five", "May 12"],
  },
  {
    id: "blank-02-community-note",
    scenario: "Blank / custom",
    tonePreset: "Warm",
    messageToReplyTo: "",
    roughDraftReply:
      "This message is to inform families that the Thursday workshop will be moved to Room 204 due to maintenance in the library. The start time remains 6:30pm, and the session will still cover scholarship forms, supporting documents, and the application timeline. We apologize for any inconvenience this change may cause and appreciate your understanding regarding the matter.",
    expectedFacts: ["Thursday", "Room 204", "6:30pm"],
  },
  {
    id: "blank-03-internal-note",
    scenario: "Blank / custom",
    tonePreset: "Direct",
    messageToReplyTo: "",
    roughDraftReply:
      "The purpose of this note is to summarize the current status of the vendor review. Vendor A has provided the revised security questionnaire, Vendor B is still waiting on legal approval, and Vendor C has requested an extension until Friday. Based on the current timeline, I recommend that we do not make a final decision until all three responses are available for comparison.",
    expectedFacts: ["Vendor A", "Vendor B", "Vendor C", "Friday"],
  },
  {
    id: "reply-01-teacher-extension",
    scenario: "Email or message reply",
    tonePreset: "Warm",
    messageToReplyTo:
      "Hi Professor, I missed the reflection deadline because of a family issue this week. I know the policy says late work may not be accepted, but is there any way I can still submit it before class tomorrow?",
    roughDraftReply:
      "Dear Student, I acknowledge receipt of your email regarding the missed reflection deadline. Late submissions are generally subject to the course policy and may not be accepted. However, I understand that you have indicated a family issue. I will review the situation and respond accordingly. Please be advised that submitting before class tomorrow does not guarantee that it will be accepted.",
    expectedFacts: ["family issue", "before class tomorrow", "course policy"],
  },
  {
    id: "reply-02-sales-followup",
    scenario: "Email or message reply",
    tonePreset: "Warm",
    messageToReplyTo:
      "Thanks for sending the proposal. We like the reporting feature, but the team is comparing two other vendors and probably will not decide until next month.",
    roughDraftReply:
      "Hello Jordan, I am following up on our previous communication regarding the proposal. Please advise whether you would like to proceed with the proposal as discussed. We are happy to provide any additional information that may assist your decision-making process as you evaluate your options.",
    expectedFacts: ["Jordan", "reporting feature", "next month"],
  },
  {
    id: "reply-03-parent-question",
    scenario: "Email or message reply",
    tonePreset: "Direct",
    messageToReplyTo:
      "Hi, I saw Kai's grade dropped this week. He said he turned in everything except one exit ticket. Can you explain what happened?",
    roughDraftReply:
      "Thank you for reaching out regarding Kai's grade. I understand your concern. The grade change is due to two missing participation activities and one missing exit ticket. I can provide additional details if needed and would be happy to discuss this matter further at your earliest convenience.",
    expectedFacts: ["Kai", "two missing participation activities", "one missing exit ticket"],
  },
  {
    id: "support-01-priya-billing",
    scenario: "Customer support",
    tonePreset: "Warm",
    messageToReplyTo:
      "Hi Reply In My Voice team, our usage report shows 18 active seats for May, but we only approved 15 regular seats. The invoice preview is NZD $126 higher than last month. We had three temporary contractors during the first week of May and they were supposed to be removed after the client handover on May 8. Can you confirm what changed and what we should do before the invoice is finalized? Thanks, Priya",
    roughDraftReply:
      "Hi Priya, Thank you for contacting us regarding the usage report and invoice preview in your account. We understand that there appears to be a discrepancy between the number of active seats shown in the dashboard and the number of seats approved during your renewal. The most likely explanation is that the three temporary contractors were counted as active seats during May. Even if a user only joins for part of the month, prorated charges may apply. Please check whether the contractors are still active and send us their names if you would like assistance.",
    expectedFacts: ["Priya", "18 active seats", "15 regular seats", "NZD $126", "May 8"],
  },
  {
    id: "support-02-export-error",
    scenario: "Customer support",
    tonePreset: "Direct",
    messageToReplyTo:
      "The CSV export from the dashboard is missing the custom tags column. We need the export for our Monday board packet, and the team cannot reconcile April without that column.",
    roughDraftReply:
      "Thank you for reaching out. We apologize for any inconvenience caused by the missing custom tags column in your CSV export. Our team is currently investigating the matter and will provide an update as soon as possible. In the meantime, please be advised that you may try exporting the report again from the dashboard settings page.",
    expectedFacts: ["custom tags column", "Monday board packet", "April"],
  },
  {
    id: "support-03-login-access",
    scenario: "Customer support",
    tonePreset: "Direct",
    messageToReplyTo:
      "Mina was added to our workspace yesterday, but she still sees the old team after logging in with mina@northstar.example. We already resent the invite twice.",
    roughDraftReply:
      "Hello, thank you for contacting support. We understand the issue regarding Mina's account access and apologize for the inconvenience. It may be related to the user's previous workspace association. Please ensure that the invitation has been accepted correctly and that the user is logging in with the appropriate email address.",
    expectedFacts: ["Mina", "mina@northstar.example", "resent the invite twice"],
  },
  {
    id: "cover-01-operations-role",
    scenario: "Cover letter",
    tonePreset: "Direct",
    messageToReplyTo:
      "Job post: Operations Coordinator at a nonprofit education program. The role mentions partner communication, weekly reporting, scheduling, and keeping shared documents organized.",
    roughDraftReply:
      "I am writing to express my interest in the Operations Coordinator position. I am a passionate and results-driven professional with a proven track record of managing communication, coordinating schedules, and supporting team success. In my last role, I prepared weekly partner updates, kept shared folders organized, and helped schedule meetings for a team of eight. I believe I would be a perfect fit for your dynamic team.",
    expectedFacts: ["weekly partner updates", "shared folders", "team of eight"],
  },
  {
    id: "cover-02-customer-success",
    scenario: "Cover letter",
    tonePreset: "Warm",
    messageToReplyTo:
      "Role: Customer Success Associate at a B2B SaaS company. They want someone who can answer customer questions clearly and write helpful follow-up notes.",
    roughDraftReply:
      "I am excited to apply for the Customer Success Associate role. I have always been passionate about helping customers and delivering excellent service. In my current position, I respond to customer questions, summarize action items after calls, and update our help center articles when a pattern appears. I am confident that my communication skills and positive attitude would make me a strong addition to the team.",
    expectedFacts: ["summarize action items", "help center articles", "Customer Success Associate"],
  },
  {
    id: "cover-03-admin-assistant",
    scenario: "Cover letter",
    tonePreset: "Direct",
    messageToReplyTo:
      "Opening: Administrative Assistant for a clinic. The listing emphasizes calendar coordination, patient follow-up notes, and careful handling of private information.",
    roughDraftReply:
      "Please accept my application for the Administrative Assistant position. I am highly organized and detail oriented, and I have extensive experience supporting administrative workflows. At my previous clinic, I managed calendars for three providers, prepared patient follow-up notes, and handled private information carefully according to office procedures. I would welcome the opportunity to bring my skills to your organization.",
    expectedFacts: ["three providers", "patient follow-up notes", "private information"],
  },
  {
    id: "work-01-design-delay",
    scenario: "Work update",
    tonePreset: "Direct",
    messageToReplyTo:
      "Can you send the revised screenshots today? I need to include them in the partner update.",
    roughDraftReply:
      "Unfortunately, the revised screenshots are not available at this time due to a delay in receiving the updated design file. The source file arrived late this morning and still requires one quality check before it can be shared externally. I expect to send the screenshots by 4pm Friday if there are no further issues.",
    expectedFacts: ["source file arrived late", "one quality check", "4pm Friday"],
  },
  {
    id: "work-02-launch-risk",
    scenario: "Work update",
    tonePreset: "Direct",
    messageToReplyTo:
      "Can you give me the current status before the 2pm launch check?",
    roughDraftReply:
      "The current launch status is that the payment flow has passed the latest smoke test, but the webhook retry log still needs review. I am checking the last three failed events and will post the result before the 2pm launch check. At this time, I do not recommend changing the launch decision until that review is complete.",
    expectedFacts: ["payment flow", "last three failed events", "2pm launch check"],
  },
  {
    id: "work-03-research-summary",
    scenario: "Work update",
    tonePreset: "Warm",
    messageToReplyTo:
      "Did we get enough feedback from the teacher interviews to update the onboarding copy?",
    roughDraftReply:
      "I am writing to inform you that the teacher interview notes are now ready for review. We completed six interviews this week. Four teachers mentioned that the onboarding copy felt too technical, and two asked for a sample response before signing up. I recommend updating the first screen and adding one short example before the next test on Wednesday.",
    expectedFacts: ["six interviews", "four teachers", "two asked", "Wednesday"],
  },
  {
    id: "support-04-priya-billing-long-regression",
    scenario: "Customer support",
    tonePreset: "Warm",
    messageToReplyTo:
      "Hi Reply In My Voice team,\n\nI am reaching out because the usage report and the invoice preview in our account do not seem to match what we expected for this month. I checked the dashboard this morning, and it shows 18 active seats for May, but our team only approved 15 regular seats when we renewed. I also noticed that the invoice preview is NZD $126 higher than last month, and I need to explain this to our finance manager before the invoice is finalized.\n\nThe confusing part is that we had three temporary contractors join the workspace for a short project during the first week of May. They were supposed to be removed after the client handover on May 8. My understanding was that temporary users would not affect the monthly billing if they were only added for a few days, but maybe I misunderstood how the billing works.\n\nCan you please confirm what changed, whether those three temporary users are included in the current invoice preview, and whether the base plan itself has changed? I do not want to tell finance the wrong thing. If this is just a prorated charge, please explain it in plain English so I can forward the explanation internally.\n\nAlso, if the three temporary users are still active, please let me know what we need to do to remove them before the invoice is finalized. I am not asking you to change anything yet; I just need a clear explanation and next step.\n\nThanks,\nPriya",
    roughDraftReply:
      "Hi Priya,\n\nThanks for reaching out. I can understand why this looks confusing, especially with the temporary contractors added for only a short project.\n\nFrom what you described, the most likely reason the May invoice preview is higher is that the three temporary contractors were counted as active seats during May. Even if a user only joins for part of the month, they can still create a prorated seat charge for the days they had access. In plain English: you are not necessarily being charged for three full extra seats for the whole month, but the system may be charging for the portion of May when those users were active.\n\nThe base plan itself does not appear to be the issue based on the information here. The difference seems more likely related to the active seat count increasing from 15 to 18 during the billing period.\n\nTo help you explain this internally, you could summarize it to finance like this:\n\n“The May invoice preview is higher because three temporary contractor accounts were active in the workspace during the month. The additional NZD $126 appears to be a prorated usage charge for those temporary seats, rather than a change to our base plan.”\n\nFor next steps, please check whether the three contractor accounts are still showing as active in the workspace. If they are, you can remove or deactivate them before the invoice is finalized so they do not continue affecting future billing. We will not make any changes on your behalf unless you ask us to.\n\nPlease send us the names or email addresses of the three contractor users if you would like us to help confirm whether they are still active.\n\nThanks,\nReply In My Voice Team",
    expectedFacts: ["Priya", "18 active seats", "15 regular seats", "NZD $126", "May 8", "finance manager", "base plan"],
  },
  {
    id: "support-05-data-export-long",
    scenario: "Customer support",
    tonePreset: "Direct",
    messageToReplyTo:
      "Hello, our monthly data export is missing the custom tags column for April and May. We use those tags to reconcile campaigns before the board packet goes out on Monday. I tried exporting from the dashboard twice, once with all filters cleared and once with only the Northeast region selected. Both files had the same issue. Can you tell us whether this is a known export problem, whether the original data is still safe, and what we should do if we need a corrected file before Monday at 10am?",
    roughDraftReply:
      "Thank you for contacting us regarding the missing custom tags column in your April and May CSV exports. We understand the importance of this information for your campaign reconciliation and Monday board packet. Our team is currently investigating the export behavior to determine whether the issue is related to the dashboard filters or the CSV generation process. Please be assured that there is no indication that the underlying campaign data has been deleted or changed. In the meantime, you may attempt to generate the report again using the standard dashboard export workflow. We will provide an update as soon as more information is available.",
    expectedFacts: ["custom tags column", "April", "May", "Monday", "10am", "Northeast region"],
  },
  {
    id: "support-06-login-workspace-long",
    scenario: "Customer support",
    tonePreset: "Direct",
    messageToReplyTo:
      "Mina accepted the invitation for the Northstar workspace yesterday, but when she logs in with mina@northstar.example she still lands in the old pilot workspace. We resent the invite twice and also asked her to try a private browser window. She still sees the old team and cannot access the billing report folder. Can you tell us if this is because her email is linked to the previous workspace, and what exact step we should take next?",
    roughDraftReply:
      "Hello, thank you for contacting support. We understand the issue regarding Mina's account access and apologize for the inconvenience caused by the workspace mismatch. Based on the information provided, this may be related to the user's previous workspace association or an incomplete invitation acceptance flow. Please ensure that Mina is logging in with the correct email address, mina@northstar.example, and that she has accepted the most recent invitation. If the problem persists, our team can review the account association and provide additional guidance regarding the next steps.",
    expectedFacts: ["Mina", "mina@northstar.example", "resent the invite twice", "old pilot workspace", "billing report folder"],
  },
  {
    id: "support-07-plan-change-long",
    scenario: "Customer support",
    tonePreset: "Warm",
    messageToReplyTo:
      "We switched from the Starter plan to the Team plan on May 3 because our manager wanted shared templates. The checkout page said the change would be prorated, but the invoice preview now shows the full Team plan amount plus a separate adjustment line. I need to know whether this means we are being charged twice or whether it is just the old plan credit and new plan charge shown separately. Please explain it in a way I can paste into our internal finance thread.",
    roughDraftReply:
      "Thank you for reaching out regarding the invoice preview after your recent plan change from Starter to Team. We understand that billing adjustments can be confusing when prorated charges and credits appear as separate invoice lines. The invoice preview may show both the credit for unused time on the Starter plan and the charge for the Team plan for the remaining portion of the billing period. This does not necessarily mean that you are being charged twice. Rather, it may reflect the standard prorated adjustment process. Please review the line items carefully, and contact us if you would like additional assistance.",
    expectedFacts: ["Starter plan", "Team plan", "May 3", "shared templates", "old plan credit", "new plan charge"],
  },
  {
    id: "support-08-delayed-response-long",
    scenario: "Customer support",
    tonePreset: "Direct",
    messageToReplyTo:
      "We opened ticket #4821 on Friday about duplicate notifications being sent to client contacts. The issue is still happening this morning. We understand engineering may need time, but our account team needs to know whether we should pause the campaign before noon. Can you give us a plain answer on what is confirmed, what is still being checked, and when we will hear the next update?",
    roughDraftReply:
      "Thank you for following up on ticket #4821 regarding the duplicate notifications being sent to client contacts. We apologize for the continued inconvenience. Our engineering team is actively investigating the matter and working to identify the root cause. At this time, we are still reviewing the relevant delivery logs and notification events. We understand that your account team needs guidance before noon regarding whether to pause the campaign. We will provide an update as soon as possible once additional information is available.",
    expectedFacts: ["ticket #4821", "Friday", "duplicate notifications", "before noon", "pause the campaign"],
  },
  {
    id: "support-09-course-transfer-refund-long-regression",
    scenario: "Customer support",
    tonePreset: "Direct",
    messageToReplyTo:
      "Hi team, my work schedule changed unexpectedly and I can no longer attend the online course session I registered for. I am currently enrolled in the June weekend cohort, which starts on Saturday, 6 June. I contacted you before the course start date, but I am not sure whether I am still in time to move to another cohort or request a refund. I saw that the next available cohort may start on Saturday, 20 July. Can you explain whether I can transfer, whether a refund is possible, and what you need from me before making any changes? Please do not cancel my current seat unless I confirm.",
    roughDraftReply:
      "Hi Daniel,\n\nThank you for contacting us and for explaining your situation in detail. We understand that your work schedule has changed unexpectedly, and that this has made it difficult for you to attend the online course session you originally registered for.\n\nAfter reviewing the information you provided, it appears that your current enrollment is for the June weekend cohort, which is scheduled to begin on Saturday, 6 June. Since you notified us before the course start date, you may still be eligible to move your enrollment to a later cohort, depending on seat availability.\n\nAt this stage, the next available cohort is scheduled to begin on Saturday, 20 July. If that date works better for you, we can help move your registration to that session. Please note that your course access, learning materials, and live session links would be updated once the transfer is confirmed.\n\nRegarding your question about a refund, our standard policy is that refund requests must be submitted at least seven days before the course begins. Since your message was sent before the course start date but close to the deadline, we would need to review the exact registration timestamp before confirming whether a full refund is available. If a full refund is not available, we may still be able to offer a course credit or transfer your enrollment to a future session.\n\nIn plain terms, you currently have two possible options:\n\n1. You can transfer your enrollment to a later cohort, subject to availability.\n2. You can request a refund review, and our team will confirm whether the timing meets the refund policy.\n\nBefore we make any changes, please reply and confirm whether you would prefer to move your enrollment to the July cohort or proceed with a refund review. We will not update your registration or cancel your current seat unless you clearly confirm which option you would like to choose.\n\nBest regards,\nCustomer Support Team",
    expectedFacts: [
      "Daniel",
      "June weekend cohort",
      "Saturday, 6 June",
      "Saturday, 20 July",
      "seat availability",
      "seven days before the course begins",
      "registration timestamp",
      "course credit",
      "will not update your registration or cancel your current seat unless you clearly confirm",
    ],
  },
  {
    id: "cover-04-long-program-manager",
    scenario: "Cover letter",
    tonePreset: "Direct",
    messageToReplyTo:
      "Role: Program Manager at a community education nonprofit. The listing asks for experience coordinating volunteers, writing partner updates, managing workshop schedules, and tracking outcomes for grant reports.",
    roughDraftReply:
      "I am writing to express my strong interest in the Program Manager position. I am a highly motivated and results-oriented professional with extensive experience supporting community programs and coordinating stakeholders. In my current role, I coordinate a volunteer roster of 32 people, prepare monthly partner updates, manage the schedule for weekend workshops, and track attendance and completion numbers for grant reports. I am passionate about education access and believe my background makes me an excellent fit for your organization. I would welcome the opportunity to contribute my skills to your team and support the continued success of your programs.",
    expectedFacts: ["32 people", "monthly partner updates", "weekend workshops", "grant reports", "education access"],
  },
  {
    id: "cover-05-long-support-specialist",
    scenario: "Cover letter",
    tonePreset: "Warm",
    messageToReplyTo:
      "Role: Support Specialist for a small SaaS company. They want someone who can answer customer questions, document recurring issues, and work with product on help center gaps.",
    roughDraftReply:
      "I am excited to submit my application for the Support Specialist role. I have always been passionate about helping customers succeed and providing excellent service. In my previous position, I answered customer questions through email and chat, summarized recurring issues for our product team, and updated help center articles when we noticed the same question coming up repeatedly. I enjoy making complicated product details easier for customers to understand. I believe I would be a valuable addition to your team and would appreciate the opportunity to discuss my qualifications further.",
    expectedFacts: ["email and chat", "recurring issues", "product team", "help center articles"],
  },
  {
    id: "work-04-long-launch-readiness",
    scenario: "Work update",
    tonePreset: "Direct",
    messageToReplyTo:
      "Can you summarize launch readiness before the 2pm check? I need to know what is green, what is still being reviewed, and whether there is any reason to delay.",
    roughDraftReply:
      "The current launch readiness status is as follows. The payment flow passed the latest smoke test, the onboarding checklist has been reviewed, and the help article links have been updated in the footer. The remaining item is the webhook retry log, where I am reviewing the last three failed events from this morning. I expect to post the result before the 2pm launch check. At this time, I do not recommend delaying the launch unless the retry log shows a repeated payment event failure.",
    expectedFacts: ["payment flow", "onboarding checklist", "help article links", "last three failed events", "2pm launch check"],
  },
  {
    id: "work-05-long-design-delay",
    scenario: "Work update",
    tonePreset: "Direct",
    messageToReplyTo:
      "Are the revised screenshots ready for the partner deck? The deck is going out Friday afternoon and I need time to review them.",
    roughDraftReply:
      "Unfortunately, the revised screenshots are not yet available because the updated design source file arrived later than expected this morning. The screen captures still need one quality check before they can be shared externally, especially because the pricing table changed in section three and the partner logo appears on the final slide. I expect to send the screenshots by 4pm Friday if there are no further issues. I will let you know sooner if the quality check reveals anything that changes that timing.",
    expectedFacts: ["source file arrived later", "one quality check", "pricing table", "section three", "4pm Friday"],
  },
  {
    id: "blank-04-long-policy-note",
    scenario: "Blank / custom",
    tonePreset: "Warm",
    messageToReplyTo: "",
    roughDraftReply:
      "This message is to inform participants that the Saturday workshop location has changed due to maintenance in the library. The session will now take place in Room 204, and the start time remains 6:30pm. The agenda is unchanged and will still include scholarship forms, supporting documents, and the application timeline. Participants who already submitted questions do not need to send them again. We apologize for any inconvenience this change may cause and appreciate your understanding regarding the matter.",
    expectedFacts: ["Saturday", "Room 204", "6:30pm", "scholarship forms", "application timeline"],
  },
  {
    id: "reply-04-long-sales-renewal",
    scenario: "Email or message reply",
    tonePreset: "Warm",
    messageToReplyTo:
      "Thanks for the renewal proposal. We like the reporting feature and the new team templates, but finance asked us to compare two other vendors before we commit. The earliest we can decide is the first week of June. If you have a shorter summary of the two plan options, send it over and I will include it in our internal thread.",
    roughDraftReply:
      "Hello Jordan, I am following up regarding the renewal proposal and your ongoing evaluation process. We appreciate your interest in the reporting feature and the new team templates. We understand that your finance team is comparing multiple vendors before making a final decision. Please let us know whether you would like to proceed with one of the plan options, and we would be happy to provide any additional information that may assist your decision-making process during the first week of June.",
    expectedFacts: ["Jordan", "reporting feature", "team templates", "two other vendors", "first week of June"],
  },
];

const longEvaluationExtensions: Record<
  string,
  { messageToReplyTo: string; roughDraftReply: string }
> = {
  "support-05-data-export-long": {
    messageToReplyTo:
      "\n\nFor context, this export is part of our monthly reporting workflow. The operations team imports the CSV into a spreadsheet, checks each campaign against its custom tag, and then sends the reconciled numbers to finance and the board assistant. Last month the same export included the tag column, so the team is worried that either a product change removed the field or the report is failing silently. We do not need anyone to make an account change yet. We just need a clear answer on whether the data is intact and what workaround is safest before the Monday deadline.",
    roughDraftReply:
      "\n\nWe recognize that your team is relying on this export for an internal reporting deadline and that the missing column creates extra manual work. The key point to communicate is that the missing CSV column does not automatically mean the underlying tag data is gone. It may be a report-generation issue or a configuration issue in the export path. A useful reply should separate what is confirmed, what is still being checked, and what the customer can do before Monday at 10am without implying that engineering has already found the cause.",
  },
  "support-06-login-workspace-long": {
    messageToReplyTo:
      "\n\nThis is time sensitive because Mina needs the billing report folder for a meeting with the finance lead tomorrow morning. The team already tried the obvious steps: she accepted the newest invitation, used the same email address, opened a private browser window, and waited for the account page to refresh. We do not want to delete the old pilot workspace because it still has archived notes. We need to know whether the issue is likely an account association problem and whether support can check the workspace link from our side.",
    roughDraftReply:
      "\n\nThe reply needs to acknowledge the steps already taken without repeating them as if they are new instructions. It should make clear that Mina should keep using mina@northstar.example and that the previous workspace association may be the reason she is landing in the old pilot workspace. It should also preserve the fact that the invite was resent twice and that the missing destination is the billing report folder. Do not promise that support will unlink the account immediately unless the user asks for that change.",
  },
  "support-07-plan-change-long": {
    messageToReplyTo:
      "\n\nThe finance team is asking for a plain-English explanation because they need to approve the monthly close. The account owner is not disputing the upgrade; they approved the Team plan because of shared templates. The concern is only the invoice preview layout. If the preview is showing a credit for unused Starter time and a new prorated Team charge, they can explain that internally. If it is a duplicate charge, they need to pause before the invoice finalizes. Please keep the explanation careful and avoid saying the invoice is definitely correct without checking.",
    roughDraftReply:
      "\n\nA good answer should explain the likely invoice structure without sounding defensive. It should say that separate credit and charge lines can appear during a mid-cycle plan change, and that this usually reflects proration rather than double billing. It should preserve May 3, Starter, Team, shared templates, old plan credit, and new plan charge. It should invite the customer to send a screenshot or invoice line items if they want the team to confirm the preview, but it should not promise an adjustment or refund.",
  },
  "support-08-delayed-response-long": {
    messageToReplyTo:
      "\n\nThe client contacts are senior buyers, so duplicate notifications are creating visible confusion. The account team has not paused the campaign yet because they are waiting for guidance from support. They do not need a detailed engineering root-cause report in this reply, but they do need a usable status update: what is known, what is still unknown, and when they should expect the next message. They specifically asked for the answer before noon because a campaign decision depends on it.",
    roughDraftReply:
      "\n\nThe reply should avoid a vague 'we are investigating' message because the customer already knows that. It should keep ticket #4821, Friday, duplicate notifications, delivery logs, noon, and the pause decision. It should say what is confirmed in plain language: duplicate notifications are still being reviewed and logs are being checked. It should also be honest about what is not confirmed yet. If no firm resolution time is available, the next update time should be stated cautiously and not invented.",
  },
  "cover-04-long-program-manager": {
    messageToReplyTo:
      "\n\nThe applicant wants the letter to feel grounded, not like a generic nonprofit application. They have not managed a whole department, but they have coordinated volunteers, handled partner communications, managed workshop calendars, and tracked numbers used in grant reporting. The tone should be professional but still personal. Do not invent leadership titles, budget ownership, fundraising wins, or program outcomes that were not included in the draft.\n\nThe posting also says the program manager will work with school partners, community volunteers, and a small operations team. The applicant has been the person who notices when a workshop schedule is drifting, follows up with volunteers before the weekend, and turns attendance notes into the numbers a grants manager can actually use. They want the letter to show that practical coordination style without sounding like a broad claim about changing education systems.",
    roughDraftReply:
      "\n\nThe current draft sounds polished but generic. It should be rewritten to put the concrete experience first: 32 volunteers, monthly partner updates, weekend workshop schedule, attendance and completion numbers, and grant reports. The application can mention education access because it is in the original draft, but it should not overstate passion or claim a perfect fit. The final version should feel like a person explaining why this specific role matches work they have actually done.\n\nIt should also keep the application focused on the employer's needs. The reader should quickly understand that the applicant can keep details organized across people, dates, documents, and reporting deadlines. Avoid opening with a grand statement. A more believable version might begin with the applicant's actual coordination work and then connect that work to the nonprofit's program manager role.",
  },
  "cover-05-long-support-specialist": {
    messageToReplyTo:
      "\n\nThe applicant is early in their support career and wants the letter to sound confident without pretending to be senior. They have direct experience answering email and chat, noticing recurring customer issues, summarizing patterns for the product team, and updating help center articles. The company is small, so the letter should show practical judgment and clear communication rather than broad claims about world-class service.\n\nThey also want to avoid sounding like they copied a career-advice template. The hiring manager probably cares less about big personality claims and more about whether the applicant can write clear answers, spot repeated confusion, and help the product team understand where customers are getting stuck. The letter should preserve the help center work because that is one of the strongest details in the draft.",
    roughDraftReply:
      "\n\nThe rewrite should remove broad phrases like passionate about helping customers and valuable addition to your team. It should keep the actual work: email and chat support, recurring issues, product team summaries, and help center article updates. It should also keep the idea that the applicant likes making complicated product details easier to understand. Do not add metrics, customer satisfaction scores, management experience, or technical certifications.\n\nThe final letter can be warm, but it should still feel like a real applicant wrote it. It should not over-explain every support task. The strongest version will probably have two or three compact paragraphs: why the role fits, what the applicant has done, and why that experience would help a small SaaS support team.",
  },
  "work-04-long-launch-readiness": {
    messageToReplyTo:
      "\n\nThe launch check has several people attending: product, support, engineering, and billing. They need a short update that can be pasted into the launch channel before the meeting. The key is to separate green items from the one remaining review item. The sender should not sound alarmed, but also should not hide the fact that the webhook retry log still needs review. If the last three failed events show a repeated payment event issue, the launch decision may need to change.\n\nThe message should also avoid sounding like a formal incident report because this is still a readiness update, not a postmortem. Product needs to know whether the user flow is ready, support needs to know whether help links are in place, engineering needs to know what log review remains, and billing needs to know whether payment events are safe enough for launch. Keep the 2pm timing visible.",
    roughDraftReply:
      "\n\nThe rewrite should keep the payment flow, onboarding checklist, help article links, webhook retry log, last three failed events, and 2pm launch check. It should not add new test results. It should make the update easier to scan, but it should not turn into a formal status report with too many headings. The final message should be clear enough that a manager can see what is ready and what is still being checked.\n\nIf the text uses bullets, keep them short. If it uses paragraphs, make each paragraph do a different job: ready items, pending review, and launch recommendation. Do not make the launch sound approved if the retry log still has to be checked.",
  },
  "work-05-long-design-delay": {
    messageToReplyTo:
      "\n\nThe partner deck is being sent externally, so the screenshots need to be accurate. The manager is mainly worried about timing and whether the partner logo and pricing table are correct. They do not need a long apology. They need to know why the screenshots are not ready yet, what check remains, and when they should expect the files if nothing else changes.\n\nThere is also a relationship issue here: the manager needs enough information to trust the delay, but not a defensive explanation. The response should make it clear that the late source file caused the delay, that the pricing table in section three and the partner logo on the final slide are the details being checked, and that 4pm Friday is still the current target if the check does not find another problem.",
    roughDraftReply:
      "\n\nThe rewrite should keep the updated design source file arriving late, one quality check, pricing table, section three, partner logo, final slide, and 4pm Friday. It should not promise delivery earlier than 4pm. It should also preserve the note that timing could change if the quality check reveals an issue. The message should sound like a practical work update rather than a formal excuse.\n\nA good version can be direct: what happened, what remains, and when the recipient will get the screenshots. Avoid phrases like unfortunately, at this time, and if there are no further issues if they make the message sound like a customer service macro.",
  },
  "blank-04-long-policy-note": {
    messageToReplyTo: "",
    roughDraftReply:
      "\n\nAdditional details for the note: families received the original workshop reminder on Tuesday, so this update should focus only on the room change and not repeat the full registration instructions. The library maintenance is temporary and does not affect the rest of the program schedule. Participants may still bring printed scholarship drafts if they want feedback during the session. The tone should be warm and clear, but the note should not sound like a legal notice or a school district memo.\n\nThe note will be sent by text and email, so it needs to be easy to understand without extra context. It should keep Saturday, Room 204, 6:30pm, scholarship forms, supporting documents, application timeline, and the fact that already-submitted questions do not need to be resent. Do not add a new deadline, a new contact person, or a different reason for the room change. The best version should sound like a coordinator giving a practical update to families.",
  },
  "reply-04-long-sales-renewal": {
    messageToReplyTo:
      "\n\nThe buyer has been responsive but cautious. They are not ready for a hard close, and pushing too aggressively could hurt the renewal conversation. They specifically asked for a shorter summary of the two plan options, so the reply should offer that summary and respect the first-week-of-June timing. It should not imply that they already chose a vendor or that the deal is urgent. Keep the reporting feature, team templates, finance review, two other vendors, and first week of June.\n\nThis should sound like a real sales follow-up from someone who listened. The sender can say they will send a tighter summary, but should not ask for a decision immediately. The reply should make it easy for Jordan to forward the summary internally, and it should leave the door open for questions while the team compares vendors.",
    roughDraftReply:
      "\n\nThe rewrite should sound like a helpful sales follow-up, not a pressure email. It should acknowledge that they like the reporting feature and team templates, offer to send a shorter plan-options summary, and leave room for the finance comparison. Avoid phrases like decision-making process, proceed with the proposal, and happy to provide any additional information if they make the note feel generic. The best version should sound like one person responding to Jordan's actual message.\n\nPreserve Jordan's name if it appears in the draft, but do not invent a last name or company. Do not claim that the plan options are attached unless the draft says so. A good reply should be short enough to send in a thread but specific enough that it clearly answers the buyer's request.",
  },
};

const allEvalCases = cases.map((sample) => {
  const extension = longEvaluationExtensions[sample.id];
  if (!extension) {
    return sample;
  }

  return {
    ...sample,
    messageToReplyTo: `${sample.messageToReplyTo}${extension.messageToReplyTo}`,
    roughDraftReply: `${sample.roughDraftReply}${extension.roughDraftReply}`,
  };
});

type EvalMode = "smoke" | "focused" | "full";

type EvalCliOptions = {
  mode: EvalMode;
  corpusPath: string | null;
  outputPath: string;
  progressPath: string;
  limit: number;
  resume: boolean;
  timeBudgetMs: number;
};

type CompletedCorpusCase = {
  id: string;
  scenario: ScenarioOption;
  tonePreset: TonePreset;
  draftAiLikePercent: number | null;
  rewriteAiLikePercent: number | null;
  changePoints: number | null;
  factsPreserved: boolean;
  unsupportedFactsCount: number;
  forbiddenViolationsCount: number;
  qualityFailure: boolean;
  customerUsablePass: boolean;
  strictSignalPass: boolean;
  completedAt: string;
};

type CorpusProgress = {
  corpusPath: string;
  outputPath: string;
  startedAt: string;
  lastUpdatedAt: string;
  strategyVersion: string;
  naturalnessThreshold: number;
  completedCases: CompletedCorpusCase[];
};

type LegacyEvalProgress = {
  mode: EvalMode;
  outputPath: string;
  startedAt: string;
  lastUpdatedAt: string;
  strategyVersion: string;
  naturalnessThreshold: number;
  completedRows: EvalResultRow[];
};

function cliValue(args: string[], name: string) {
  const equalsValue = args
    .find((arg) => arg.startsWith(`--${name}=`))
    ?.slice(name.length + 3);
  if (equalsValue !== undefined) {
    return equalsValue;
  }

  const flagIndex = args.indexOf(`--${name}`);
  const nextValue = flagIndex >= 0 ? args[flagIndex + 1] : undefined;
  return nextValue && !nextValue.startsWith("--") ? nextValue : undefined;
}

function parseNonNegativeInteger(value: string, flagName: string) {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 0) {
    throw new Error(`--${flagName} must be a non-negative integer`);
  }

  return parsed;
}

function parseEvalMode(
  args: string[] = process.argv.slice(2),
  env: NodeJS.ProcessEnv = process.env,
): EvalMode {
  const modeArg =
    cliValue(args, "mode") ??
    env.EVAL_MODE ??
    "focused";

  if (modeArg === "smoke" || modeArg === "focused" || modeArg === "full") {
    return modeArg;
  }

  return "focused";
}

function parseEvalCliOptions(
  args: string[] = process.argv.slice(2),
  env: NodeJS.ProcessEnv = process.env,
): EvalCliOptions {
  const limitArg = cliValue(args, "limit");
  const timeBudgetArg = cliValue(args, "time-budget-ms");

  return {
    mode: parseEvalMode(args, env),
    corpusPath: cliValue(args, "corpus") ?? null,
    outputPath:
      cliValue(args, "output") ??
      path.join("docs", "scenario-evaluation-results.md"),
    progressPath:
      cliValue(args, "progress") ??
      path.join("plans", "learning-baseline-progress.json"),
    limit:
      limitArg === undefined
        ? Number.POSITIVE_INFINITY
        : parseNonNegativeInteger(limitArg, "limit"),
    resume: args.includes("--resume"),
    timeBudgetMs:
      timeBudgetArg === undefined
        ? 540000
        : parseNonNegativeInteger(timeBudgetArg, "time-budget-ms"),
  };
}

const learningBaselineScenarioMap = new Map<string, ScenarioOption>([
  ["Blank", "Blank / custom"],
  ["Blank / custom", "Blank / custom"],
  ["Email", "Email or message reply"],
  ["Email or message reply", "Email or message reply"],
  ["Customer support", "Customer support"],
  ["Cover letter", "Cover letter"],
  ["Work update", "Work update"],
]);

function parseMarkdownTableRow(line: string) {
  const trimmed = line.trim();
  if (!trimmed.startsWith("|") || !trimmed.endsWith("|")) {
    return null;
  }

  return trimmed
    .slice(1, -1)
    .split("|")
    .map((cell) => cell.trim());
}

function isMarkdownSeparatorRow(cells: string[]) {
  return cells.every((cell) => /^:?-{3,}:?$/.test(cell));
}

export function parseLearningBaselineCorpus(markdown: string): EvalCase[] {
  const cases: EvalCase[] = [];

  for (const line of markdown.split(/\r?\n/)) {
    const cells = parseMarkdownTableRow(line);
    if (!cells || cells[0] === "ID" || isMarkdownSeparatorRow(cells)) {
      continue;
    }

    const rowId = cells[0] || "unknown row";
    if (cells.length !== 7) {
      throw new Error(
        `Learning baseline corpus row ${rowId} must have 7 columns; found ${cells.length}`,
      );
    }

    const [, , scenarioText, toneText, draftText, factsText] = cells;
    const scenario = learningBaselineScenarioMap.get(scenarioText);
    if (!scenario) {
      throw new Error(
        `Learning baseline corpus row ${rowId} has unknown scenario: ${scenarioText}`,
      );
    }

    if (toneText !== "Warm" && toneText !== "Direct") {
      throw new Error(
        `Learning baseline corpus row ${rowId} has unknown tone: ${toneText}`,
      );
    }

    cases.push({
      id: rowId,
      scenario,
      tonePreset: toneText,
      messageToReplyTo: "",
      roughDraftReply: draftText,
      expectedFacts: factsText
        .split(";")
        .map((fact) => fact.trim())
        .filter(Boolean),
      forbiddenClaims: [],
    });
  }

  return cases;
}

function selectEvalCases(samples: EvalCase[], mode: EvalMode) {
  if (mode === "smoke") {
    const smokeIds = [
      "draft-only-01b-teacher-jordan-long-polished",
      "draft-only-16b-implementation-schedule-regression",
      "draft-only-12-support-seat-count",
      "reply-03-parent-question",
      "support-04-priya-billing-long-regression",
      "support-05-data-export-long",
      "support-07-plan-change-long",
      "support-08-delayed-response-long",
      "support-09-course-transfer-refund-long-regression",
      "work-04-long-launch-readiness",
      "blank-04-long-policy-note",
    ];
    const byId = new Map(samples.map((sample) => [sample.id, sample]));

    return smokeIds
      .map((id) => byId.get(id))
      .filter((sample): sample is EvalCase => Boolean(sample));
  }

  const targetCount = mode === "full" ? Math.min(60, samples.length) : 30;
  const selected = new Map<string, EvalCase>();
  const add = (items: EvalCase[], limit: number) => {
    for (const item of items) {
      if (selected.size >= targetCount) {
        return;
      }
      if (limit <= 0) {
        return;
      }
      if (!selected.has(item.id)) {
        selected.set(item.id, item);
        limit -= 1;
      }
    }
  };

  add(
    samples.filter((item) => item.messageToReplyTo.trim().length === 0),
    mode === "full" ? 20 : 14,
  );
  add(
    samples.filter(
      (item) =>
        item.messageToReplyTo.trim().length > 0 &&
        /teacher|parent|student/i.test(item.id),
    ),
    mode === "full" ? 8 : 4,
  );
  add(
    samples.filter((item) => /^work-/.test(item.id)),
    mode === "full" ? 8 : 4,
  );
  add(
    samples.filter(
      (item) =>
        item.scenario === "Customer support" || /support|sales/i.test(item.id),
    ),
    mode === "full" ? 16 : 8,
  );
  add(
    samples.filter((item) => /cover|policy|quote|list|blank/i.test(item.id)),
    mode === "full" ? 8 : 4,
  );

  for (const item of samples) {
    if (selected.size >= targetCount) {
      break;
    }
    selected.set(item.id, item);
  }

  return [...selected.values()].slice(0, targetCount);
}

function emailEvalCaseToEvalCase(sample: RewriteEmailEvalCase): EvalCase {
  const input = rewriteEmailEvalCaseToRequestInput(sample);

  return {
    id: sample.id,
    scenario: input.scenario,
    tonePreset: input.tonePreset,
    messageToReplyTo: input.messageToReplyTo,
    roughDraftReply: input.roughDraftReply,
    audience: input.audience,
    purpose: input.purpose,
    whatHappened: input.whatHappened,
    factsToPreserve: input.factsToPreserve,
    expectedFacts: sample.mustKeep,
    forbiddenClaims: sample.mustNotClaim,
    qualityTargets: sample.rewriteQualityTargets,
    expectedRewriteChallenges: sample.expectedRewriteChallenges,
  };
}

function shouldUseEmailMarkdownCorpus() {
  return (
    process.env.EVAL_CORPUS === "email-100" ||
    Boolean(process.env.EVAL_CASES_PATH)
  );
}

async function loadSelectedEvalCases(mode: EvalMode) {
  if (!shouldUseEmailMarkdownCorpus()) {
    return selectEvalCases(allEvalCases, mode);
  }

  const corpusPath =
    process.env.EVAL_CASES_PATH ??
    path.join(process.cwd(), "docs", "rewrite-email-eval-cases-100.md");
  const markdown = await readFile(corpusPath, "utf8");
  const emailCases = parseRewriteEmailEvalCases(markdown);

  return selectRewriteEmailEvalCases(emailCases, mode).map(emailEvalCaseToEvalCase);
}

async function evaluateSample(
  sample: EvalCase,
  rewriteConfig: ReturnType<typeof getFactReconstructConfig>,
): Promise<EvalResultRow> {
  const input = rewriteRequestSchema.parse({
    scenario: sample.scenario,
    messageToReplyTo: sample.messageToReplyTo,
    roughDraftReply: sample.roughDraftReply,
    audience: sample.audience,
    purpose: sample.purpose,
    whatHappened: sample.whatHappened,
    factsToPreserve: sample.factsToPreserve,
    tone: tonePresetToTone(sample.tonePreset),
    tonePreset: sample.tonePreset,
  });
  const plan = createRewritePlan(input);
  let result: RewriteResponsePayload | null = null;
  let qualityFailure: RewriteQualityFailure | null = null;

  try {
    result = await rewriteWithFactReconstruct(input);
  } catch (error) {
    if (error instanceof FactReconstructQualityError) {
      qualityFailure = error;
    } else {
      throw error;
    }
  }

  const rewrittenText = result?.rewrittenText ?? "";
  const { facts, forbiddenViolations, quality } = rewrittenText
    ? mergeSemanticJudgeWithDeterministicFactCheck(
        rewrittenText,
        sample.expectedFacts,
        await semanticFactJudge(
          sample.roughDraftReply,
          rewrittenText,
          sample.expectedFacts,
          sample.forbiddenClaims ?? [],
          sample.qualityTargets,
        ),
      )
    : {
        facts: { passed: false, missing: sample.expectedFacts },
        forbiddenViolations: [],
        quality: emptyQualityJudgeResult(),
      };
  const unsupportedFacts = rewrittenText
    ? detectEvalUnsupportedFacts(input, rewrittenText).map((fact) => fact.text)
    : [];
  const naturalness = result?.naturalness ?? qualityFailure?.naturalness;
  const draft = naturalness?.draftAiLikePercent ?? null;
  const rewrite = naturalness?.rewriteAiLikePercent ?? null;
  const change = naturalness?.changePoints ?? null;
  const candidateSignals =
    result?.optimization.candidateSignals ?? qualityFailure?.candidateSignals ?? [];
  const firstCandidate = candidateSignals.find((item) => item.stage === "initial");
  const repairCandidate = candidateSignals.find((item) => item.stage === "repair");
  const rejectedReasons = candidateSignals
    .filter((item) => item.rejected)
    .map((item) => `${item.stage}: ${item.reason}`);
  const signalPassed = signalPass(
    draft,
    rewrite,
    rewriteConfig.naturalnessThreshold,
  );
  const signalNotWorse =
    draft === null || rewrite === null || rewrite <= draft;
  const qualityRegression = isQualityRegression(
    quality.draftScore,
    quality.rewriteScore,
  );
  const customerUsablePass = customerUsablePassFromGates({
    hasRewrittenText: Boolean(rewrittenText),
    factsPassed: facts.passed,
    unsupportedFactsCount: unsupportedFacts.length,
    forbiddenViolationsCount: forbiddenViolations.length,
    qualityFailure: Boolean(qualityFailure),
    signalNotWorse,
    qualityRegression,
  });

  return {
    ...sample,
    diagnosisTags: plan.tags,
    rewritePlan: plan.summary,
    rewrittenText,
    wordCount: wordCount([sample.messageToReplyTo, sample.roughDraftReply].join(" ")),
    charCount: [sample.messageToReplyTo, sample.roughDraftReply].join(" ").length,
    draft,
    firstCandidate: firstCandidate?.aiLikePercent ?? null,
    repairCandidate: repairCandidate?.aiLikePercent ?? null,
    rewrite,
    change,
    candidateSignals,
    rejectedReasons,
    factsPreserved: facts.passed,
    missingFacts: facts.missing,
    unsupportedFactsIntroduced: unsupportedFacts,
    forbiddenClaimViolations: forbiddenViolations,
    qualityDraftScore: quality.draftScore,
    qualityRewriteScore: quality.rewriteScore,
    qualityDelta: quality.delta,
    qualityNotes: quality.notes,
    qualityRegression,
    qualityFailure: Boolean(qualityFailure),
    qualityFailureReason:
      qualityFailure && "reason" in qualityFailure
        ? qualityFailure.reason
        : qualityFailure
          ? "quality_gate_failed"
          : null,
    customerUsablePass,
    strictSignalPass:
      facts.passed &&
      unsupportedFacts.length === 0 &&
      forbiddenViolations.length === 0 &&
      signalPassed,
  };
}

function summarizeRows(rows: EvalResultRow[]) {
  const measured = rows.filter(
    (row) => row.draft !== null && row.rewrite !== null && row.change !== null,
  );
  const qualityMeasured = rows.filter(
    (row) => row.qualityDraftScore !== null && row.qualityRewriteScore !== null,
  );

  return {
    measured,
    averageDrop: measured.length
      ? Math.round(
          measured.reduce((total, row) => total + Math.abs(row.change ?? 0), 0) /
            measured.length,
        )
      : null,
    belowFifty: measured.filter((row) => (row.rewrite ?? 100) < 50).length,
    customerUsablePassed: rows.filter((row) => row.customerUsablePass).length,
    strictSignalPassed: rows.filter((row) => row.strictSignalPass).length,
    qualityMeasured,
    qualityImproved: qualityMeasured.filter(
      (row) => (row.qualityDelta ?? 0) > 0,
    ).length,
    qualityRegressions: rows.filter((row) => row.qualityRegression).length,
    averageQualityDelta: qualityMeasured.length
      ? Number(
          (
            qualityMeasured.reduce(
              (total, row) => total + (row.qualityDelta ?? 0),
              0,
            ) / qualityMeasured.length
          ).toFixed(1),
        )
      : null,
    draftOnlyCasesEvaluated: rows.filter(
      (row) => row.messageToReplyTo.trim().length === 0,
    ).length,
    factFailures: rows.filter(
      (row) =>
        !row.factsPreserved ||
        row.unsupportedFactsIntroduced.length > 0 ||
        row.forbiddenClaimViolations.length > 0,
    ).length,
    longCases: rows.filter((row) => row.wordCount >= 300).length,
    longSupportCases: rows.filter(
      (row) => row.scenario === "Customer support" && row.wordCount >= 300,
    ).length,
    worseSelected: measured.filter(
      (row) =>
        !row.qualityFailure &&
        row.rewrite !== null &&
        row.draft !== null &&
        row.rewrite > row.draft,
    ).length,
    repairUsed: rows.filter((row) =>
      row.candidateSignals.some((item) => item.stage === "repair"),
    ).length,
    rejectedCount: rows.reduce(
      (total, row) =>
        total + row.candidateSignals.filter((item) => item.rejected).length,
      0,
    ),
  };
}

function formatSummaryHeader(
  rows: EvalResultRow[],
  evalMode: string,
  rewriteConfig: ReturnType<typeof getFactReconstructConfig>,
) {
  const summary = summarizeRows(rows);

  return [
    "# Scenario Evaluation Results",
    "",
    `Date: ${new Date().toISOString()}`,
    `Eval mode: ${evalMode}`,
    `Strategy: ${rewriteConfig.strategyVersion}`,
    `Naturalness threshold: ${rewriteConfig.naturalnessThreshold}%`,
    `Cases evaluated: ${rows.length}`,
    `Draft-only cases: ${summary.draftOnlyCasesEvaluated}`,
    `Measured cases: ${summary.measured.length}`,
    `Long cases (300+ words): ${summary.longCases}`,
    `Long customer-support cases (300+ words): ${summary.longSupportCases}`,
    `Average AI-like signal drop: ${
      summary.averageDrop === null ? "unavailable" : `${summary.averageDrop} pts`
    }`,
    `Rewrite below 50% AI-like signal: ${summary.belowFifty}/${summary.measured.length}`,
    `Final selected rewrites worse than draft: ${summary.worseSelected}/${summary.measured.length}`,
    `Quality-score measured cases: ${summary.qualityMeasured.length}`,
    `Quality-score improved cases: ${summary.qualityImproved}/${summary.qualityMeasured.length}`,
    `Quality regressions: ${summary.qualityRegressions}/${rows.length}`,
    `Average quality-score delta: ${
      summary.averageQualityDelta === null
        ? "unavailable"
        : `${summary.averageQualityDelta} pts`
    }`,
    `Cases using targeted repair: ${summary.repairUsed}/${rows.length}`,
    `Rejected candidate events: ${summary.rejectedCount}`,
    `Fact preservation or unsupported-addition failures: ${summary.factFailures}`,
    `Customer-usable pass count: ${summary.customerUsablePassed}/${rows.length}`,
    `Strict signal pass count: ${summary.strictSignalPassed}/${rows.length}`,
  ];
}

function formatCaseBlock(row: EvalResultRow) {
  return [
    `## ${row.id}`,
    "",
    `Scenario: ${row.scenario}`,
    `Tone: ${row.tonePreset}`,
    `Input word count: ${row.wordCount}`,
    `Input character count: ${row.charCount}`,
    `Diagnosis tags: ${row.diagnosisTags.length ? row.diagnosisTags.join(", ") : "none"}`,
    `Rewrite plan: ${row.rewritePlan}`,
    `Draft AI-like signal: ${points(row.draft)}`,
    `First candidate AI-like signal: ${points(row.firstCandidate)}`,
    `Repair candidate AI-like signal: ${points(row.repairCandidate)}`,
    `Final selected AI-like signal: ${points(row.rewrite)}`,
    `Change: ${row.change === null ? "unavailable" : `${row.change} pts`}`,
    `Rejected candidate reasons: ${row.rejectedReasons.length ? row.rejectedReasons.join(" | ") : "none"}`,
    `Facts preserved: ${row.factsPreserved ? "yes" : "no"}`,
    `Missing facts: ${row.missingFacts.length ? row.missingFacts.join("; ") : "none"}`,
    `Unsupported facts introduced: ${row.unsupportedFactsIntroduced.length ? row.unsupportedFactsIntroduced.join("; ") : "none"}`,
    `Forbidden-claim violations: ${row.forbiddenClaimViolations.length ? row.forbiddenClaimViolations.join("; ") : "none"}`,
    `Quality score before: ${row.qualityDraftScore ?? "unavailable"}`,
    `Quality score after: ${row.qualityRewriteScore ?? "unavailable"}`,
    `Quality score delta: ${row.qualityDelta === null ? "unavailable" : `${row.qualityDelta} pts`}`,
    `Quality regression: ${row.qualityRegression ? "yes" : "no"}`,
    `Quality notes: ${row.qualityNotes.length ? row.qualityNotes.join(" | ") : "none"}`,
    `Quality failure state: ${row.qualityFailure ? "yes" : "no"}`,
    `Quality failure reason: ${row.qualityFailureReason ?? "none"}`,
    `Customer-usable pass: ${row.customerUsablePass ? "yes" : "no"}`,
    `Strict signal pass: ${row.strictSignalPass ? "yes" : "no"}`,
    "",
    "Expected facts:",
    row.expectedFacts.map((fact) => `- ${fact}`).join("\n"),
    row.forbiddenClaims?.length
      ? [
          "",
          "Forbidden claims:",
          row.forbiddenClaims.map((claim) => `- ${claim}`).join("\n"),
        ].join("\n")
      : "",
    "",
    "Before:",
    "",
    "```text",
    row.roughDraftReply,
    "```",
    "",
    "After:",
    "",
    "```text",
    row.rewrittenText,
    "```",
    "",
  ].join("\n");
}

function formatLegacyOutput(
  rows: EvalResultRow[],
  evalMode: EvalMode,
  rewriteConfig: ReturnType<typeof getFactReconstructConfig>,
) {
  return [
    ...formatSummaryHeader(rows, evalMode, rewriteConfig),
    "",
    "Customer-usable pass requires: rewritten output exists, all expected facts are preserved, no unsupported names/dates/amounts/counts are added, no quality failure or material quality regression is raised, and the selected rewrite is not worse than the draft when scores are available.",
    `Strict signal pass additionally requires scores available and: if the draft is above ${rewriteConfig.naturalnessThreshold}%, the final rewrite is at or below ${rewriteConfig.naturalnessThreshold}%; if the draft is already at or below ${rewriteConfig.naturalnessThreshold}%, the final rewrite does not raise the signal.`,
    "",
    ...rows.map(formatCaseBlock),
  ].join("\n");
}

function resolveFromCwd(filePath: string) {
  return path.isAbsolute(filePath) ? filePath : path.join(process.cwd(), filePath);
}

async function writeFileEnsuringDirectory(filePath: string, content: string) {
  await mkdir(path.dirname(filePath), { recursive: true });
  await writeFile(filePath, content);
}

async function loadLegacyEvalProgress(
  options: EvalCliOptions,
  rewriteConfig: ReturnType<typeof getFactReconstructConfig>,
): Promise<LegacyEvalProgress> {
  const now = new Date().toISOString();

  if (options.resume) {
    try {
      const existing = JSON.parse(
        await readFile(resolveFromCwd(options.progressPath), "utf8"),
      ) as LegacyEvalProgress;

      return {
        ...existing,
        mode: options.mode,
        outputPath: options.outputPath,
        strategyVersion: rewriteConfig.strategyVersion,
        naturalnessThreshold: rewriteConfig.naturalnessThreshold,
        completedRows: existing.completedRows ?? [],
      };
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code !== "ENOENT") {
        throw error;
      }
    }
  }

  return {
    mode: options.mode,
    outputPath: options.outputPath,
    startedAt: now,
    lastUpdatedAt: now,
    strategyVersion: rewriteConfig.strategyVersion,
    naturalnessThreshold: rewriteConfig.naturalnessThreshold,
    completedRows: [],
  };
}

async function writeLegacyEvalProgress(
  progressPath: string,
  progress: LegacyEvalProgress,
) {
  const outputPath = resolveFromCwd(progressPath);
  const tmpPath = `${outputPath}.${process.pid}.${Date.now()}.tmp`;

  await mkdir(path.dirname(outputPath), { recursive: true });
  await writeFile(tmpPath, `${JSON.stringify(progress, null, 2)}\n`);
  await rename(tmpPath, outputPath);
}

function legacyScriptErrorRow(sample: EvalCase, reason: string): EvalResultRow {
  const plan = createRewritePlan({
    scenario: sample.scenario,
    messageToReplyTo: sample.messageToReplyTo,
    roughDraftReply: sample.roughDraftReply,
    audience: sample.audience ?? "",
    purpose: sample.purpose ?? "",
    whatHappened: sample.whatHappened ?? "",
    factsToPreserve: sample.factsToPreserve ?? "",
    tone: tonePresetToTone(sample.tonePreset),
    tonePreset: sample.tonePreset,
  });

  return {
    ...sample,
    diagnosisTags: plan.tags,
    rewritePlan: plan.summary,
    rewrittenText: "",
    wordCount: wordCount([sample.messageToReplyTo, sample.roughDraftReply].join(" ")),
    charCount: [sample.messageToReplyTo, sample.roughDraftReply].join(" ").length,
    draft: null,
    firstCandidate: null,
    repairCandidate: null,
    rewrite: null,
    change: null,
    candidateSignals: [],
    rejectedReasons: [],
    factsPreserved: false,
    missingFacts: sample.expectedFacts,
    unsupportedFactsIntroduced: [],
    forbiddenClaimViolations: [],
    qualityDraftScore: null,
    qualityRewriteScore: null,
    qualityDelta: null,
    qualityNotes: [reason],
    qualityRegression: false,
    qualityFailure: true,
    qualityFailureReason: reason,
    customerUsablePass: false,
    strictSignalPass: false,
  };
}

async function runLegacyEval(
  options: EvalCliOptions,
  rewriteConfig: ReturnType<typeof getFactReconstructConfig>,
) {
  const startTime = Date.now();
  const evalCases = await loadSelectedEvalCases(options.mode);
  const progress = await loadLegacyEvalProgress(options, rewriteConfig);
  const rows: EvalResultRow[] = [...progress.completedRows];
  const completedIds = new Set(rows.map((row) => row.id));
  let processedThisRun = 0;

  console.log(
    `[eval] mode=${options.mode} strategy=${rewriteConfig.strategyVersion} threshold=${rewriteConfig.naturalnessThreshold} completed=${rows.length}/${evalCases.length}`,
  );

  for (const sample of evalCases) {
    if (completedIds.has(sample.id)) {
      continue;
    }

    if (processedThisRun >= options.limit) {
      console.log(
        `[eval] --limit=${options.limit} reached; ${processedThisRun} cases this run; exiting cleanly`,
      );
      break;
    }
    if (Date.now() - startTime > options.timeBudgetMs) {
      console.log(
        `[eval] time-budget ${options.timeBudgetMs}ms exhausted after ${processedThisRun} cases; exiting cleanly. ` +
          `Total completed: ${rows.length}/${evalCases.length}. Next --resume run will continue.`,
      );
      break;
    }

    console.log(`[eval] ${rows.length + 1}/${evalCases.length} ${sample.id}`);
    let row: EvalResultRow;

    try {
      row = await evaluateSample(sample, rewriteConfig);
    } catch (error) {
      row = legacyScriptErrorRow(sample, `script_error: ${firstErrorLine(error)}`);
    }

    rows.push(row);
    completedIds.add(sample.id);
    processedThisRun += 1;
    progress.completedRows = rows;
    progress.lastUpdatedAt = new Date().toISOString();
    await writeLegacyEvalProgress(options.progressPath, progress);
    await writeFileEnsuringDirectory(
      resolveFromCwd(options.outputPath),
      formatLegacyOutput(rows, options.mode, rewriteConfig),
    );
  }

  const outputPath = resolveFromCwd(options.outputPath);
  await writeFileEnsuringDirectory(
    outputPath,
    formatLegacyOutput(rows, options.mode, rewriteConfig),
  );
  console.log(`Wrote ${outputPath}`);
}

async function loadLearningBaselineCorpus(corpusPath: string) {
  const markdown = await readFile(resolveFromCwd(corpusPath), "utf8");

  return parseLearningBaselineCorpus(markdown);
}

async function loadCorpusProgress(
  options: EvalCliOptions & { corpusPath: string },
  rewriteConfig: ReturnType<typeof getFactReconstructConfig>,
): Promise<CorpusProgress> {
  const now = new Date().toISOString();

  if (options.resume) {
    try {
      const existing = JSON.parse(
        await readFile(resolveFromCwd(options.progressPath), "utf8"),
      ) as CorpusProgress;

      return {
        ...existing,
        corpusPath: options.corpusPath,
        outputPath: options.outputPath,
        strategyVersion: rewriteConfig.strategyVersion,
        naturalnessThreshold: rewriteConfig.naturalnessThreshold,
      };
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code !== "ENOENT") {
        throw error;
      }
    }
  }

  return {
    corpusPath: options.corpusPath,
    outputPath: options.outputPath,
    startedAt: now,
    lastUpdatedAt: now,
    strategyVersion: rewriteConfig.strategyVersion,
    naturalnessThreshold: rewriteConfig.naturalnessThreshold,
    completedCases: [],
  };
}

async function writeCorpusProgress(progressPath: string, progress: CorpusProgress) {
  const outputPath = resolveFromCwd(progressPath);
  const tmpPath = `${outputPath}.${process.pid}.${Date.now()}.tmp`;

  await mkdir(path.dirname(outputPath), { recursive: true });
  await writeFile(tmpPath, `${JSON.stringify(progress, null, 2)}\n`);
  await rename(tmpPath, outputPath);
}

function corpusProgressFromRow(
  row: EvalResultRow,
  completedAt: string,
): CompletedCorpusCase {
  return {
    id: row.id,
    scenario: row.scenario,
    tonePreset: row.tonePreset,
    draftAiLikePercent: row.draft,
    rewriteAiLikePercent: row.rewrite,
    changePoints: row.change,
    factsPreserved: row.factsPreserved,
    unsupportedFactsCount: row.unsupportedFactsIntroduced.length,
    forbiddenViolationsCount: row.forbiddenClaimViolations.length,
    qualityRegression: row.qualityRegression,
    qualityFailure: row.qualityFailure,
    customerUsablePass: row.customerUsablePass,
    strictSignalPass: row.strictSignalPass,
    completedAt,
  };
}

function scriptErrorProgress(
  sample: EvalCase,
  completedAt: string,
): CompletedCorpusCase {
  return {
    id: sample.id,
    scenario: sample.scenario,
    tonePreset: sample.tonePreset,
    draftAiLikePercent: null,
    rewriteAiLikePercent: null,
    changePoints: null,
    factsPreserved: false,
    unsupportedFactsCount: 0,
    forbiddenViolationsCount: 0,
    qualityRegression: false,
    qualityFailure: true,
    customerUsablePass: false,
    strictSignalPass: false,
    completedAt,
  };
}

function firstErrorLine(error: unknown) {
  const message = error instanceof Error ? error.message : String(error);

  return message.split(/\r?\n/)[0] || "unknown error";
}

function formatScriptErrorBlock(sample: EvalCase, reason: string) {
  return [
    `## ${sample.id}`,
    "",
    `Scenario: ${sample.scenario}`,
    `Tone: ${sample.tonePreset}`,
    "Quality failure state: yes",
    `Quality failure reason: ${reason}`,
    "",
    "Expected facts:",
    sample.expectedFacts.map((fact) => `- ${fact}`).join("\n"),
    "",
    "Before:",
    "",
    "```text",
    sample.roughDraftReply,
    "```",
    "",
  ].join("\n");
}

function formatCorpusHeader(progress: CorpusProgress, corpusLength: number) {
  return [
    "# Learning Baseline Evaluation Results",
    "",
    `Date: ${progress.startedAt}`,
    `Corpus: ${progress.corpusPath}`,
    `Strategy: ${progress.strategyVersion}`,
    `Naturalness threshold: ${progress.naturalnessThreshold}%`,
    `Cases in corpus: ${corpusLength}`,
    "",
  ].join("\n");
}

function formatProgressSummaryBlock(progress: CorpusProgress, corpusLength: number) {
  const measured = progress.completedCases.filter(
    (item) =>
      item.draftAiLikePercent !== null &&
      item.rewriteAiLikePercent !== null &&
      item.changePoints !== null,
  );
  const averageDrop = measured.length
    ? Math.round(
        measured.reduce(
          (total, item) => total + Math.abs(item.changePoints ?? 0),
          0,
        ) / measured.length,
      )
    : null;
  const belowThreshold = measured.filter(
    (item) => (item.rewriteAiLikePercent ?? 100) <= progress.naturalnessThreshold,
  ).length;
  const factFailures = progress.completedCases.filter(
    (item) =>
      !item.factsPreserved ||
      item.unsupportedFactsCount > 0 ||
      item.forbiddenViolationsCount > 0,
  ).length;
  const qualityFailures = progress.completedCases.filter(
    (item) => item.qualityFailure,
  ).length;
  const qualityRegressions = progress.completedCases.filter(
    (item) => item.qualityRegression,
  ).length;
  const customerUsablePassed = progress.completedCases.filter(
    (item) => item.customerUsablePass,
  ).length;
  const strictSignalPassed = progress.completedCases.filter(
    (item) => item.strictSignalPass,
  ).length;

  return [
    "<!-- summary -->",
    `Cases completed: ${progress.completedCases.length}/${corpusLength}`,
    `Measured cases: ${measured.length}`,
    `Average AI-like signal drop: ${
      averageDrop === null ? "unavailable" : `${averageDrop} pts`
    }`,
    `Rewrite at or below naturalness threshold: ${belowThreshold}/${measured.length}`,
    `Fact preservation or unsupported-addition failures: ${factFailures}`,
    `Quality failures: ${qualityFailures}`,
    `Quality regressions: ${qualityRegressions}`,
    `Customer-usable pass count: ${customerUsablePassed}/${progress.completedCases.length}`,
    `Strict signal pass count: ${strictSignalPassed}/${progress.completedCases.length}`,
    `Last updated: ${progress.lastUpdatedAt}`,
    "<!-- /summary -->",
  ].join("\n");
}

async function rewriteOutputSummary(
  outputPath: string,
  progress: CorpusProgress,
  corpusLength: number,
) {
  const resolvedPath = resolveFromCwd(outputPath);
  const content = await readFile(resolvedPath, "utf8");
  const summaryBlock = formatProgressSummaryBlock(progress, corpusLength);
  const summaryPattern = /<!-- summary -->[\s\S]*?<!-- \/summary -->/;
  const nextContent = summaryPattern.test(content)
    ? content.replace(summaryPattern, summaryBlock)
    : `${content.trimEnd()}\n\n${summaryBlock}\n`;

  await writeFileEnsuringDirectory(resolvedPath, nextContent);
}

async function appendCaseBlock(outputPath: string, block: string) {
  const resolvedPath = resolveFromCwd(outputPath);
  await mkdir(path.dirname(resolvedPath), { recursive: true });
  await appendFile(resolvedPath, `\n${block}\n`);
}

function hasCompletedAllCorpusCases(
  progress: CorpusProgress,
  corpus: EvalCase[],
) {
  const completedIds = new Set(progress.completedCases.map((item) => item.id));

  return corpus.every((sample) => completedIds.has(sample.id));
}

async function runCorpusEval(
  options: EvalCliOptions & { corpusPath: string },
  rewriteConfig: ReturnType<typeof getFactReconstructConfig>,
) {
  const startTime = Date.now();
  const corpus = await loadLearningBaselineCorpus(options.corpusPath);
  const progress = await loadCorpusProgress(options, rewriteConfig);
  const completedIds = new Set(progress.completedCases.map((item) => item.id));
  let processedThisRun = 0;

  if (progress.completedCases.length === 0) {
    await writeFileEnsuringDirectory(
      resolveFromCwd(options.outputPath),
      formatCorpusHeader(progress, corpus.length),
    );
  }

  console.log(
    `[eval] corpus=${options.corpusPath} strategy=${rewriteConfig.strategyVersion} threshold=${rewriteConfig.naturalnessThreshold} completed=${progress.completedCases.length}/${corpus.length}`,
  );

  for (const sample of corpus) {
    if (completedIds.has(sample.id)) {
      continue;
    }

    if (processedThisRun >= options.limit) {
      console.log(
        `[eval] --limit=${options.limit} reached; ${processedThisRun} cases this run; exiting cleanly`,
      );
      break;
    }
    if (Date.now() - startTime > options.timeBudgetMs) {
      console.log(
        `[eval] time-budget ${options.timeBudgetMs}ms exhausted after ${processedThisRun} cases — exiting cleanly. ` +
          `Total completed: ${progress.completedCases.length}/${corpus.length}. ` +
          "Next --resume run will continue.",
      );
      break;
    }

    console.log(
      `[eval] ${progress.completedCases.length + 1}/${corpus.length} ${sample.id}`,
    );

    try {
      const row = await evaluateSample(sample, rewriteConfig);
      const completedAt = new Date().toISOString();
      await appendCaseBlock(options.outputPath, formatCaseBlock(row));
      progress.completedCases.push(corpusProgressFromRow(row, completedAt));
      progress.lastUpdatedAt = completedAt;
    } catch (error) {
      const completedAt = new Date().toISOString();
      const reason = `script_error: ${firstErrorLine(error)}`;
      await appendCaseBlock(options.outputPath, formatScriptErrorBlock(sample, reason));
      progress.completedCases.push(scriptErrorProgress(sample, completedAt));
      progress.lastUpdatedAt = completedAt;
    }

    await writeCorpusProgress(options.progressPath, progress);
    completedIds.add(sample.id);
    processedThisRun += 1;
  }

  if (hasCompletedAllCorpusCases(progress, corpus)) {
    await rewriteOutputSummary(options.outputPath, progress, corpus.length);
    console.log(
      `[eval] corpus complete; summary populated in ${resolveFromCwd(options.outputPath)}`,
    );
  }
}

async function main() {
  await loadEnvLocal();

  const options = parseEvalCliOptions();
  const rewriteConfig = getFactReconstructConfig();

  if (options.corpusPath) {
    await runCorpusEval({ ...options, corpusPath: options.corpusPath }, rewriteConfig);
    return;
  }

  await runLegacyEval(options, rewriteConfig);
}

const entryPoint = process.argv[1];
if (entryPoint && import.meta.url === pathToFileURL(entryPoint).href) {
  await main();
}
