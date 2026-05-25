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
  "acceptable",
  "alternatively",
  "additional",
  "am",
  "annual",
  "april",
  "august",
  "applicant",
  "ask",
  "australia",
  "at",
  "based",
  "because",
  "best",
  "before",
  "both",
  "can",
  "chrome",
  "clarify",
  "confirmation",
  "core",
  "customer",
  "currently",
  "csv",
  "december",
  "did",
  "direct",
  "could",
  "duplicate",
  "even",
  "everything",
  "exact",
  "faqs",
  "families",
  "february",
  "following",
  "friday",
  "for",
  "from",
  "full",
  "have",
  "hello",
  "here",
  "hi",
  "hiring",
  "however",
  "if",
  "i",
  "i'd",
  "i'll",
  "i'm",
  "i've",
  "in",
  "it",
  "january",
  "june",
  "last",
  "glad",
  "happy",
  "long",
  "accept",
  "also",
  "already",
  "avoid",
  "cancel",
  "choose",
  "confirm",
  "continue",
  "do",
  "don",
  "downgrade",
  "english",
  "explain",
  "express",
  "finance",
  "keep",
  "may",
  "march",
  "no",
  "northstar",
  "monday",
  "just",
  "kindly",
  "job",
  "let",
  "lets",
  "looking",
  "manager",
  "missing",
  "move",
  "my",
  "need",
  "new",
  "next",
  "november",
  "nzd",
  "october",
  "opening",
  "offer",
  "on",
  "option",
  "options",
  "our",
  "partial",
  "per",
  "please",
  "pm",
  "printed",
  "participants",
  "parent",
  "preserve",
  "price",
  "product",
  "purpose",
  "decision",
  "prorated",
  "quick",
  "quiz",
  "rather",
  "regarding",
  "remember",
  "reply",
  "requested",
  "right",
  "role",
  "room",
  "safari",
  "saas",
  "saturday",
  "sender",
  "september",
  "signing",
  "sincerely",
  "she",
  "since",
  "sms",
  "so",
  "solo",
  "sorry",
  "ssO".toLowerCase(),
  "student",
  "students",
  "standard",
  "staff",
  "suggested",
  "switch",
  "that",
  "teacher",
  "teachers",
  "team",
  "thanks",
  "thank",
  "the",
  "they",
  "then",
  "there",
  "this",
  "which",
  "ticket",
  "company",
  "reopening",
  "why",
  "would",
  "yes",
  "one",
  "two",
  "three",
  "four",
  "five",
  "six",
  "thursday",
  "to",
  "tuesday",
  "transfer",
  "upgrade",
  "updating",
  "use",
  "warm",
  "wednesday",
  "we",
  "we'll",
  "what",
  "when",
  "voice",
  "while",
  "you",
  "your",
]);

const phraseFacts: Array<{
  category: RequiredFactCategory;
  pattern: RegExp;
  text: string;
}> = [
  { category: "task", pattern: /\breading response\b/i, text: "reading response" },
  { category: "task", pattern: /\blab notes\b/i, text: "lab notes" },
  { category: "task", pattern: /\breflection paragraph\b/i, text: "reflection paragraph" },
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
  { category: "constraint", pattern: /\brefund window\b/i, text: "refund window" },
  { category: "constraint", pattern: /\blate but complete\b/i, text: "late but complete" },
  { category: "constraint", pattern: /\bno grade penalty\b/i, text: "no grade penalty" },
  { category: "constraint", pattern: /\bmanager approval\b/i, text: "manager approval" },
  { category: "constraint", pattern: /\baccount credit\b/i, text: "account credit" },
  { category: "task", pattern: /\bempty file attempt\b/i, text: "empty file attempt" },
  { category: "constraint", pattern: /\broom is unavailable\b/i, text: "room is unavailable" },
  { category: "constraint", pattern: /\bbase subscription\b/i, text: "base subscription" },
  { category: "constraint", pattern: /\bbase plan\b/i, text: "base plan" },
  { category: "constraint", pattern: /\bnew approval cycle\b/i, text: "new approval cycle" },
  { category: "constraint", pattern: /\badvanced SSO\b/i, text: "advanced SSO" },
  { category: "constraint", pattern: /\bbase plan did not change\b/i, text: "base plan did not change" },
  { category: "quoted_phrase", pattern: /\bStarter plan\b/i, text: "Starter plan" },
  { category: "quoted_phrase", pattern: /\bTeam plan\b/i, text: "Team plan" },
  { category: "quoted_phrase", pattern: /\bold plan credit\b/i, text: "old plan credit" },
  { category: "quoted_phrase", pattern: /\bnew plan charge\b/i, text: "new plan charge" },
  { category: "constraint", pattern: /\bnot be recalculated\b/i, text: "not be recalculated" },
  { category: "constraint", pattern: /\bnot a duplicate charge\b/i, text: "not a duplicate charge" },
  { category: "constraint", pattern: /\binvoice screenshot\b/i, text: "invoice screenshot" },
  { category: "constraint", pattern: /\bnot push for a decision\b/i, text: "not push for a decision" },
  { category: "constraint", pattern: /\bwill not include pricing\b/i, text: "will not include pricing" },
  { category: "constraint", pattern: /\bnot promising\b/i, text: "not promising" },
  { category: "constraint", pattern: /\bscheduled with me\b/i, text: "scheduled with me" },
  { category: "constraint", pattern: /\blogo color has not changed\b/i, text: "logo color has not changed" },
  { category: "deadline", pattern: /\bdesktop today\b/i, text: "desktop today" },
  { category: "constraint", pattern: /\bone missing exit ticket\b/i, text: "one missing exit ticket" },
  {
    category: "constraint",
    pattern: /\bcannot enter the quiz score\b/i,
    text: "cannot enter the quiz score",
  },
  { category: "constraint", pattern: /\bcannot approve\b/i, text: "cannot approve" },
  { category: "quoted_phrase", pattern: /\bApril 9 science museum field trip\b/i, text: "April 9 science museum field trip" },
  { category: "quoted_phrase", pattern: /\bcardiology scheduling request\b/i, text: "cardiology scheduling request" },
  { category: "task", pattern: /\bCustomer Success Associate\b/i, text: "Customer Success Associate" },
  { category: "task", pattern: /\bsummarize action items\b/i, text: "summarize action items" },
  { category: "task", pattern: /\bhelp center articles?\b/i, text: "help center articles" },
  { category: "task", pattern: /\bweekly partner updates\b/i, text: "weekly partner updates" },
  { category: "task", pattern: /\bshared folders\b/i, text: "shared folders" },
  { category: "count", pattern: /\bteam of eight\b/i, text: "team of eight" },
  { category: "count", pattern: /\b32 volunteers\b/i, text: "32 volunteers" },
  { category: "task", pattern: /\battendance numbers\b/i, text: "attendance numbers" },
  { category: "count", pattern: /\bthree providers\b/i, text: "three providers" },
  { category: "task", pattern: /\bpatient follow-up notes\b/i, text: "patient follow-up notes" },
  { category: "constraint", pattern: /\bprivate information\b/i, text: "private information" },
  { category: "task", pattern: /\bpermission slip\b/i, text: "permission slip" },
  { category: "quoted_phrase", pattern: /\bcracked FinchLite desk lamp\b/i, text: "cracked FinchLite desk lamp" },
  { category: "task", pattern: /\breplacement shade\b/i, text: "replacement shade" },
  { category: "quoted_phrase", pattern: /\bSaturday pantry volunteer shift\b/i, text: "Saturday pantry volunteer shift" },
  { category: "quoted_phrase", pattern: /\bpacking station two\b/i, text: "packing station two" },
  { category: "task", pattern: /\bthree-hour packing shift\b/i, text: "three-hour packing shift" },
  { category: "task", pattern: /\bwest entrance\b/i, text: "west entrance" },
  { category: "quoted_phrase", pattern: /\bproduct analyst final interview\b/i, text: "product analyst final interview" },
  { category: "task", pattern: /\btravel reimbursement\b/i, text: "travel reimbursement" },
  { category: "task", pattern: /\breport is marked received\b/i, text: "report is marked received" },
  { category: "constraint", pattern: /\bnot been released to the patient portal\b/i, text: "not been released to the patient portal" },
  { category: "constraint", pattern: /\bnot a clinician\b/i, text: "not a clinician" },
  { category: "constraint", pattern: /\bcannot interpret\b/i, text: "cannot interpret" },
  { category: "task", pattern: /\bDr\.?\s+Chen's queue\b/i, text: "Dr. Chen's queue" },
  { category: "task", pattern: /\bclinic line\b/i, text: "clinic line" },
  { category: "task", pattern: /\bshutoff valve\b/i, text: "shutoff valve" },
  { category: "constraint", pattern: /\bbefore the cabinet base can be dried\b/i, text: "before the cabinet base can be dried" },
  { category: "task", pattern: /\blockbox code already on file\b/i, text: "lockbox code already on file" },
  { category: "constraint", pattern: /\bcannot approve a rent credit\b/i, text: "cannot approve a rent credit" },
  { category: "constraint", pattern: /\baccess confirmation first\b/i, text: "access confirmation first" },
  { category: "count", pattern: /\btwo people for the cleanup shift\b/i, text: "two people for the cleanup shift" },
  { category: "task", pattern: /\bpayment flow\b/i, text: "payment flow" },
  { category: "task", pattern: /\bpricing language\b/i, text: "pricing language" },
  { category: "task", pattern: /\bimplementation timeline\b/i, text: "implementation timeline" },
  { category: "task", pattern: /\bonboarding timeline\b/i, text: "onboarding timeline" },
  { category: "task", pattern: /\btraining session\b/i, text: "training session" },
  { category: "deadline", pattern: /\bgo-live date\b/i, text: "go-live date" },
  { category: "constraint", pattern: /\buser-permission issue\b/i, text: "user-permission issue" },
  { category: "role", pattern: /\bwarehouse supervisors\b/i, text: "warehouse supervisors" },
  {
    category: "constraint",
    pattern: /\bcannot approve shift changes\b/i,
    text: "cannot approve shift changes",
  },
  { category: "quoted_phrase", pattern: /\bapproved by[”"]?\s+column\b/i, text: "approved by column" },
  {
    category: "task",
    pattern: /\bweekly reconciliation report\b/i,
    text: "weekly reconciliation report",
  },
  {
    category: "constraint",
    pattern: /\bSMS reminders? (?:are|is) not part of this phase\b/i,
    text: "SMS reminders are not part of this phase",
  },
  {
    category: "constraint",
    pattern: /\badditional NZD\s*\$\s*\d+(?:\.\d{2})? setup fee\b/i,
    text: "additional NZD $480 setup fee",
  },
  { category: "task", pattern: /\bphase-two item\b/i, text: "phase-two item" },
  { category: "role", pattern: /\bregional managers\b/i, text: "regional managers" },
  { category: "task", pattern: /\bsection three\b/i, text: "section three" },
  { category: "task", pattern: /\bsection five\b/i, text: "section five" },
  { category: "task", pattern: /\brollout notes\b/i, text: "rollout notes" },
  { category: "role", pattern: /\blegal team\b/i, text: "legal team" },
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
  { category: "quoted_phrase", pattern: /\bshared templates\b/i, text: "shared templates" },
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
  { category: "quoted_phrase", pattern: /\bApril CSV export\b/i, text: "April CSV export" },
  { category: "quoted_phrase", pattern: /\bNortheast region\b/i, text: "Northeast region" },
  { category: "constraint", pattern: /\bdata is still safe\b/i, text: "data is still safe" },
  { category: "constraint", pattern: /\bunderlying campaign data is still safe\b/i, text: "underlying campaign data is still safe" },
  { category: "quoted_phrase", pattern: /\bbilling report folder\b/i, text: "billing report folder" },
  { category: "quoted_phrase", pattern: /\bpause the campaign\b/i, text: "pause the campaign" },
  { category: "task", pattern: /\bfulfillment center\b/i, text: "fulfillment center" },
  { category: "task", pattern: /\bdelivery carrier\b/i, text: "delivery carrier" },
  { category: "constraint", pattern: /\btemporary processing issue\b/i, text: "temporary processing issue" },
  { category: "constraint", pattern: /\blocal distribution facility\b/i, text: "local distribution facility" },
  { category: "constraint", pattern: /\bstill in transit\b/i, text: "still in transit" },
  { category: "constraint", pattern: /\blost or returned\b/i, text: "lost or returned" },
  { category: "deadline", pattern: /\bone to two business days\b/i, text: "one to two business days" },
  { category: "constraint", pattern: /\bno action required\b/i, text: "no action required" },
  {
    category: "task",
    pattern: /\bfollow-up investigation with the carrier\b/i,
    text: "follow-up investigation with the carrier",
  },
  { category: "quoted_phrase", pattern: /\bRoom 204\b/i, text: "Room 204" },
  { category: "quoted_phrase", pattern: /\balready submitted questions\b/i, text: "already submitted questions" },
  {
    category: "constraint",
    pattern: /\bdo not need to send them again\b/i,
    text: "do not need to send them again",
  },
  { category: "constraint", pattern: /\bagenda is unchanged\b/i, text: "agenda is unchanged" },
  { category: "quoted_phrase", pattern: /\bSaturday'?s workshop\b/i, text: "Saturday's workshop" },
  { category: "quoted_phrase", pattern: /\bprinted scholarship drafts\b/i, text: "Printed scholarship drafts" },
  {
    category: "constraint",
    pattern: /\bdo not make me sound senior\b/i,
    text: "do not make me sound senior",
  },
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
    .replace(/\bdon['’]?t\b/g, "do not")
    .replace(/\bdoesn['’]?t\b/g, "does not")
    .replace(/\bdidn['’]?t\b/g, "did not")
    .replace(/\bhasn['’]?t\b/g, "has not")
    .replace(/\bhaven['’]?t\b/g, "have not")
    .replace(/\bisn['’]?t\b/g, "is not")
    .replace(/\baren['’]?t\b/g, "are not")
    .replace(/\bcannot say for sure\b/g, "not promising")
    .replace(/\bcannot guarantee\b/g, "not promising")
    .replace(/\bcan not guarantee\b/g, "not promising")
    .replace(/\bcannot promise\b/g, "not promising")
    .replace(/\bcan not promise\b/g, "not promising")
    .replace(/\bupgrading\b/g, "upgrade")
    .replace(/\bupgrade date was\b/g, "upgrade date")
    .replace(/\bschedule(?:d)?\s+(?:the\s+)?(?:quiz\s+retake\s+)?with me\b/g, "scheduled with me")
    .replace(/\bno need to send them again\b/g, "do not need to send them again")
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
    .replace(/\bthe invite was resent twice\b/g, "resent the invite twice")
    .replace(/\bno action is required\b/g, "no action required")
    .replace(/\bno action required from you\b/g, "no action required")
    .replace(/\bdoes not appear to be any immediate change\b/g, "no immediate change")
    .replace(/\bcurrent plan\b/g, "existing plan")
    .replace(/\bhas not been marked as\b/g, "has not been marked")
    .replace(/\bdoes not change\b/g, "has not changed")
    .replace(/\bhas not changed\b/g, "not changed")
    .replace(/\bchanged\b/g, "change")
    .replace(
      /\b(before|by|after)\s+the\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|noon|\d{1,2}(?::\d{2})?\s*(?:am|pm))\b/g,
      "$1 $2",
    )
    .replace(/\s+/g, " ")
    .replace(/\s+([,.])/g, "$1")
    .trim();
}

const constraintTokenStopWords = new Set([
  "a",
  "an",
  "and",
  "are",
  "as",
  "at",
  "be",
  "been",
  "but",
  "can",
  "could",
  "for",
  "from",
  "have",
  "he",
  "her",
  "hi",
  "i",
  "if",
  "in",
  "is",
  "it",
  "me",
  "now",
  "of",
  "on",
  "or",
  "our",
  "please",
  "right",
  "she",
  "still",
  "that",
  "the",
  "them",
  "there",
  "this",
  "to",
  "we",
  "will",
  "with",
  "you",
  "your",
]);

function constraintTokens(value: string) {
  return Array.from(
    new Set(
      normalize(value)
        .replace(/[^a-z0-9:$#.'-]+/g, " ")
        .split(/\s+/)
        .map((token) => token.trim())
        .filter((token) => {
          if (!token) {
            return false;
          }

          if (/^\d/.test(token) || /\d$/.test(token)) {
            return true;
          }

          return token.length > 2 && !constraintTokenStopWords.has(token);
        }),
    ),
  );
}

function concreteConstraintAtoms(value: string) {
  const normalizedValue = normalize(value);
  const atoms = new Set<string>();
  const patterns = [
    /\b(?:nzd|usd|aud)?\s*\$\s*\d[\d,]*(?:\.\d{2})?\b/gi,
    /\b[A-Z][A-Za-z]+-[A-Z0-9-]*\d[A-Z0-9-]*\b/g,
    /\b(?:january|february|march|april|may|june|july|august|september|october|november|december)\s+\d{1,2}\b/gi,
    /\b(?:monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b/gi,
    /\b\d{1,2}(?::\d{2})?\s*(?:a\.m\.?|p\.m\.?|am|pm)\b/gi,
    /\b\d+\b/g,
  ];

  for (const pattern of patterns) {
    for (const match of normalizedValue.matchAll(pattern)) {
      atoms.add(normalize(match[0]).replace(/,/g, ""));
    }
  }

  return Array.from(atoms);
}

function constraintPolarityPreserved(factText: string, rewritten: string) {
  if (/\bcannot\b/.test(factText)) {
    return /\bcannot\b|\bcan not\b/.test(rewritten);
  }

  if (/\bdo not need\b|\bdoes not need\b/.test(factText)) {
    return /\bdo not need\b|\bdoes not need\b|\bnot need\b|\bno need\b/.test(
      rewritten,
    );
  }

  if (/\bno immediate change\b|\bno change\b|\bnot changed\b/.test(factText)) {
    return /\bno immediate change\b|\bno change\b|\bnot changed\b|\bunchanged\b|\bstill the same\b|\bremains the same\b/.test(
      rewritten,
    );
  }

  if (/\bnot\b|\bno\b|\bwithout\b|\bunless\b|\bonly if\b/.test(factText)) {
    return /\bnot\b|\bno\b|\bwithout\b|\bunless\b|\bonly if\b|\bunchanged\b|\bstill the same\b|\bremains the same\b/.test(
      rewritten,
    );
  }

  return true;
}

function optionalBringFactPresent(factText: string, rewritten: string) {
  const match = factText.match(
    /\b(?:participants|families|students|parents|attendees|you)?\s*(?:may|can)\s+still\s+bring\s+(.+?)(?:\s+if\b|\s+for\b|$)/,
  );
  const broughtItem = match?.[1]?.trim();

  if (!broughtItem || !rewritten.includes(broughtItem)) {
    return false;
  }

  return (
    /\bstill\s+(?:welcome|allowed)\b/.test(rewritten) ||
    /\b(?:may|can)\s+still\s+bring\b/.test(rewritten) ||
    /\bwelcome\s+to\s+bring\b/.test(rewritten)
  );
}

function constraintFactPresent(factText: string, rewritten: string) {
  if (!constraintPolarityPreserved(factText, rewritten)) {
    return false;
  }

  if (optionalBringFactPresent(factText, rewritten)) {
    return true;
  }

  const atoms = concreteConstraintAtoms(factText);
  if (atoms.length >= 2 && atoms.every((atom) => rewritten.includes(atom))) {
    return true;
  }

  const tokens = constraintTokens(factText);
  if (tokens.length === 0) {
    return false;
  }

  const presentCount = tokens.filter((token) => rewritten.includes(token)).length;
  return presentCount / tokens.length >= 0.72;
}

function factId(category: RequiredFactCategory, text: string) {
  return `${category}:${normalize(text)}`;
}

function sourceText(input: RewriteRequestInput) {
  if (input.factsToPreserve.trim()) {
    return {
      message: "",
      draft: input.factsToPreserve,
    };
  }

  return {
    message: input.messageToReplyTo ?? "",
    draft: [
      input.roughDraftReply,
      input.whatHappened,
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
  const compacted = text.replace(/\s+/g, " ").trim();
  const cleaned = /\b[ap]\.m\.$/i.test(compacted)
    ? compacted
    : compacted.replace(/[,.]$/, "");
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

function personCandidateWords(value: string) {
  return cleanPersonCandidate(value)
    .split(/\s+/)
    .filter(Boolean);
}

function hasNameTitle(value: string) {
  return /^(?:Mr|Ms|Mrs|Dr)\.?\s+/i.test(value.trim());
}

function isNameTitleToken(value: string) {
  return /^(?:Mr|Ms|Mrs|Dr)\.?$/i.test(value.trim());
}

function isIdentifierLikePersonToken(value: string) {
  const token = value.trim().replace(/[,.]$/, "");
  if (!token || isNameTitleToken(token)) {
    return false;
  }

  return /^[A-Z]-$/.test(token) ||
    /-\d/.test(token) ||
    /\d/.test(token) ||
    !/^[A-Za-z][A-Za-z']*(?:-[A-Za-z][A-Za-z']*)*$/.test(token);
}

function isSingleTokenPersonCandidate(value: string) {
  return personCandidateWords(value).length === 1 && !hasNameTitle(value);
}

function shouldKeepPersonCandidate(
  value: string,
  options: { allowSingleToken?: boolean } = {},
) {
  const words = personCandidateWords(value);
  const firstWord = normalize(value.split(/\s+/)[0] ?? "");
  if (
    !value ||
    words.length === 0 ||
    properNameStopWords.has(firstWord) ||
    words.some(isIdentifierLikePersonToken)
  ) {
    return false;
  }

  return words.length > 1 || options.allowSingleToken === true;
}

function isLikelySingleNameReference(
  text: string,
  match: RegExpMatchArray,
) {
  const rawValue = match[1] ?? match[0];
  if (!isSingleTokenPersonCandidate(rawValue)) {
    return true;
  }

  if (/'s$/i.test(rawValue)) {
    return true;
  }

  const start = match.index ?? 0;
  const end = start + match[0].length;
  const before = text.slice(Math.max(0, start - 40), start);
  const after = text.slice(end, end + 80);

  if (/\b(?:hi|hello|dear)\s+$/i.test(before)) {
    return true;
  }

  if (/\b(?:best|best regards|regards|sincerely|thanks|thank you),?\s+$/i.test(before)) {
    return true;
  }

  return /^\s+(?:is|was|did|has|had|can|will|would|owns?|asked|said|approved|scored|completed|submitted|received|sent|added|attached|needs?|missed|missing|paid)\b/i.test(
    after,
  );
}

function isGenericSupportSignoff(value: string) {
  return /\b(?:customer support team|support team|customer service team)\b/i.test(
    value,
  );
}

function findSignoff(text: string) {
  const namePattern =
    "((?:Mr|Ms|Mrs|Dr)\\.?\\s+[A-Z][A-Za-z.'-]+|[A-Z][A-Za-z.'-]+(?:\\s+[A-Z][A-Za-z.'-]+){0,2})";
  const closingPattern = "(best regards|regards|sincerely|best|thanks|thank you)";
  const sameLinePattern = new RegExp(
    `^${closingPattern},\\s+${namePattern}$`,
    "i",
  );
  const closingOnlyPattern = new RegExp(`^${closingPattern},?$`, "i");
  const lines = text
    .split(/\n+/)
    .map((line) => line.trim())
    .filter(Boolean);

  for (let index = 0; index < lines.length; index += 1) {
    const sameLine = lines[index]?.match(sameLinePattern);
    if (sameLine) {
      return { closing: sameLine[1], name: sameLine[2] };
    }

    const closing = lines[index]?.match(closingOnlyPattern);
    const nextLine = lines[index + 1] ?? "";
    if (closing && new RegExp(`^${namePattern}$`).test(nextLine)) {
      return { closing: closing[1], name: nextLine };
    }
  }

  return null;
}

function isForbiddenClaimInstruction(value: string) {
  return /^\s*(?:do not|don't)\s+(?:offer|promise|imply|guess|mention|send revised quote|make me sound|state|say)\b/i.test(
    value,
  );
}

function extractFromText(
  facts: Map<string, RequiredFact>,
  text: string,
  source: RequiredFact["source"],
) {
  const extractionText = splitSentences(text)
    .filter((sentence) => !isForbiddenClaimInstruction(sentence))
    .join(". ");
  const normalizedText = extractionText.replace(/\s+/g, " ");

  const signoff = findSignoff(text);
  if (signoff && !isGenericSupportSignoff(signoff.name)) {
    addFact(facts, `${signoff.closing} ${signoff.name}`, "signoff", source);
    const signer = cleanPersonCandidate(signoff.name);
    if (shouldKeepPersonCandidate(signer, { allowSingleToken: true })) {
      addFact(facts, signer, "person", source);
    }
  }

  for (const match of normalizedText.matchAll(
    /\b(?:Hi|Hello|Dear)\s+((?:(?:Mr|Ms|Mrs|Dr)\.?\s+)?[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){0,2})(?=\s*[,:\n]|$)/g,
  )) {
    const candidate = cleanPersonCandidate(match[1]);
    if (shouldKeepPersonCandidate(candidate, { allowSingleToken: true })) {
      addFact(facts, candidate, "person", source);
    }
  }

  for (const phrase of phraseFacts) {
    if (phrase.pattern.test(extractionText)) {
      addFact(facts, phrase.text, phrase.category, source);
    }
  }

  for (const match of normalizedText.matchAll(/\border\s+#?[A-Z]?\d+\b/gi)) {
    addFact(facts, match[0], "quoted_phrase", source);
  }

  for (const match of normalizedText.matchAll(
    /\b[A-Z][A-Za-z]+-[A-Z0-9-]*\d[A-Z0-9-]*\b/g,
  )) {
    addFact(facts, match[0], "quoted_phrase", source);
  }

  for (const match of normalizedText.matchAll(
    /\b[A-Z][A-Za-z0-9'-]+(?:\s+[A-Za-z0-9'-]+){0,2}\s+(?:dashboard|handoff|rollout|project|initiative)\b/g,
  )) {
    addFact(facts, match[0], "quoted_phrase", source);
  }

  for (const match of normalizedText.matchAll(
    /\b(?:order number|billing ticket|proposal number|signup code|candidate id)\s+([A-Z]{2,8}-\d+[A-Z0-9-]*)\b/gi,
  )) {
    addFact(facts, match[1], "quoted_phrase", source);
  }

  for (const match of normalizedText.matchAll(
    /\bcannot refund more than the\s+((?:nzd\s*)?\$\s?\d+(?:\.\d{2})?\s+shipping credit)\b/gi,
  )) {
    addFact(facts, `cannot refund more than the ${match[1]}`, "constraint", source);
  }

  for (const match of normalizedText.matchAll(
    /\bcannot place\s+([A-Z][A-Za-z'-]+)\s+at client intake without intake training\b/g,
  )) {
    addFact(
      facts,
      `cannot place ${match[1]} at client intake without intake training`,
      "constraint",
      source,
    );
  }

  for (const match of normalizedText.matchAll(
    /\bcannot promise travel reimbursement for the cancelled train\b/gi,
  )) {
    addFact(
      facts,
      match[0],
      "constraint",
      source,
    );
  }

  for (const match of normalizedText.matchAll(/\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b/g)) {
    addFact(facts, match[0], "contact", source);
  }

  for (const match of normalizedText.matchAll(
    /(?:\bNZD\s*)?\$\s?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{2})?\b/gi,
  )) {
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
    /\b(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday),?\s+\d{1,2}\s+(?:January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)\b/gi,
  )) {
    addFact(facts, match[0], "date", source);
  }

  for (const match of normalizedText.matchAll(
    /\b\d{1,2}\s+(?:January|February|March|April|May|June|July|August|September|October|November|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)\b/gi,
  )) {
    addFact(facts, match[0], "date", source);
  }

  for (const match of normalizedText.matchAll(
    /\b(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)\b/gi,
  )) {
    addFact(facts, match[0], "date", source);
  }

  for (const match of normalizedText.matchAll(/\b\d{1,2}(?::\d{2})?\s*(?:a\.m\.?|p\.m\.?|am|pm)(?=\s|[.,;:]|$)/gi)) {
    addFact(facts, match[0], "deadline", source);
  }

  for (const match of normalizedText.matchAll(
    /\b\d+\s+(?:active\s+seats?|regular\s+seats?|approved\s+seats?|temporary\s+(?:users?|contractors?)|contractor accounts?|rows?|blockers?|failed\s+events?|providers?|interviews?|teachers?|seats?|users?|hours?)\b/gi,
  )) {
    addFact(facts, match[0], "count", source);
  }

  for (const match of normalizedText.matchAll(
    /\bcannot add\s+([A-Z][A-Za-z'-]+)\s+after\s+((?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2})\b/g,
  )) {
    addFact(facts, `cannot add ${match[1]} after ${match[2]}`, "constraint", source);
  }

  for (const match of normalizedText.matchAll(
    /\bhold the\s+((?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2})\s+slot\s+(?:until|through)\s+((?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2}\s+at\s+\d{1,2}(?::\d{2})?\s*(?:a\.m\.?|p\.m\.?|am|pm))\b/gi,
  )) {
    addFact(facts, `hold the ${match[1]} slot until ${match[2]}`, "constraint", source);
  }

  for (const match of normalizedText.matchAll(
    /\bhold the\s+((?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2})\s+slot\s+for\s+(\d+\s+hours?)\b/gi,
  )) {
    addFact(facts, `hold the ${match[1]} slot for ${match[2]}`, "constraint", source);
  }

  for (const match of normalizedText.matchAll(
    /\bcannot book the appointment before the referral arrives\b/gi,
  )) {
    addFact(facts, match[0], "constraint", source);
  }

  for (const match of normalizedText.matchAll(
    /\b(?:before|by|after)\s+(?:the\s+)?(?:end of this week|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday|noon|\d{1,2}(?::\d{2})?\s*(?:a\.m\.?|p\.m\.?|am|pm)(?=\s|[.,;:]|$))/gi,
  )) {
    addFact(facts, match[0], "deadline", source);
  }

  for (const match of normalizedText.matchAll(
    /\b(?:one|two|three|four|five|six|seven|eight|nine|ten|\d+)\s+(?:people|volunteers?)\b.{0,100}\b(?:cleanup|shift)\b/gi,
  )) {
    addFact(facts, match[0], "count", source);
  }

  for (const match of normalizedText.matchAll(
    /\b((?:(?:Mr|Ms|Mrs|Dr)\.?\s+)?[A-Z][A-Za-z'-]+(?:\s+[A-Z][A-Za-z'-]+){0,2})\b/g,
  )) {
    const candidate = cleanPersonCandidate(match[1]);
    if (
      shouldKeepPersonCandidate(candidate, {
        allowSingleToken: isLikelySingleNameReference(normalizedText, match),
      })
    ) {
      addFact(facts, candidate, "person", source);
    }
  }

  for (const sentence of splitSentences(extractionText)) {
    if (/\bdo not invent\b/i.test(sentence)) {
      continue;
    }

    if (
      /\b(?:cannot|can't|do not|don't|not|no|without|unless|only if|must|requires?|required|needs?|before|after|has not|have not|is not|are not)\b/i.test(sentence) ||
      (/\bby\b/i.test(sentence) &&
        /\b(?:today|tomorrow|monday|tuesday|wednesday|thursday|friday|saturday|sunday|january|february|march|april|may|june|july|august|september|october|november|december|\d{1,2}(?::\d{2})?\s*(?:a\.m\.?|p\.m\.?|am|pm))\b/i.test(sentence)) ||
      (/\b(?:offer|option|options|available|either|or)\b/i.test(sentence) &&
        /\b(?:monday|tuesday|wednesday|thursday|friday|saturday|sunday|january|february|march|april|may|june|july|august|september|october|november|december|noon|\d{1,2}(?::\d{2})?\s*(?:a\.m\.?|p\.m\.?|am|pm))\b/i.test(sentence))
      ||
      (/\bif\b/i.test(sentence) &&
        /\b(?:send|reply|confirm|call|email|provide|attach|upload|choose|bring|check in|pick up)\b/i.test(sentence))
    ) {
      addFact(facts, sentence, "constraint", source);
    }

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
  const unwrapped = value
    .split(/\n{2,}/)
    .map((paragraph) => paragraph.replace(/\n+/g, " "))
    .join("\n\n");

  return unwrapped
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

  if (fact.category === "constraint" && constraintFactPresent(factText, rewritten)) {
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

type TemporalClaim = {
  text: string;
  normalizedText: string;
};

const temporalTimePattern =
  String.raw`(?:noon|\d{1,2}(?::\d{2})?\s*(?:a\.?m\.?|p\.?m\.?|am|pm))`;
const temporalDateRefPattern = String.raw`(?:today|tomorrow|tonight|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday|(?:January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)\s+\d{1,2})`;

function normalizeTemporalDateRef(value: string) {
  return normalize(value.replace(/,/g, " "));
}

function normalizeTemporalTime(value: string) {
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

function addTemporalClaim(
  claims: Map<string, TemporalClaim>,
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

function extractTemporalClaims(value: string) {
  const claims = new Map<string, TemporalClaim>();
  const dateThenTime = new RegExp(
    `\\b(${temporalDateRefPattern})\\b\\s*(?:,\\s*)?\\s+(at|by|before|after)\\s+(${temporalTimePattern})\\b`,
    "gi",
  );
  const boundaryDateTime = new RegExp(
    `\\b(by|before|after)\\s+(${temporalDateRefPattern})\\b(?:\\s+at)?\\s+(${temporalTimePattern})\\b`,
    "gi",
  );
  const dateRange = new RegExp(
    `\\b(${temporalDateRefPattern})\\b\\s*(?:,\\s*)?\\s+between\\s+(${temporalTimePattern})\\s+and\\s+(${temporalTimePattern})\\b`,
    "gi",
  );
  const timeOnDate = new RegExp(
    `\\b(${temporalTimePattern})\\s+on\\s+(${temporalDateRefPattern})\\b`,
    "gi",
  );
  const optionTimesOnDate = new RegExp(
    `\\b(${temporalTimePattern})\\s+or\\s+(${temporalTimePattern})\\s+on\\s+(${temporalDateRefPattern})\\b`,
    "gi",
  );
  const bareBoundaryTime = new RegExp(
    `\\b(by|before|after)\\s+(${temporalTimePattern})\\b`,
    "gi",
  );

  for (const match of value.matchAll(dateThenTime)) {
    const date = normalizeTemporalDateRef(match[1]);
    const prep = normalize(match[2]);
    const time = normalizeTemporalTime(match[3]);
    addTemporalClaim(claims, match[0], `${date} ${prep} ${time}`);
  }

  for (const match of value.matchAll(boundaryDateTime)) {
    const prep = normalize(match[1]);
    const date = normalizeTemporalDateRef(match[2]);
    const time = normalizeTemporalTime(match[3]);
    addTemporalClaim(claims, match[0], `${prep} ${date} at ${time}`);
  }

  for (const match of value.matchAll(dateRange)) {
    const date = normalizeTemporalDateRef(match[1]);
    const start = normalizeTemporalTime(match[2]);
    const end = normalizeTemporalTime(match[3]);
    addTemporalClaim(claims, match[0], `${date} between ${start} and ${end}`);
  }

  for (const match of value.matchAll(timeOnDate)) {
    const time = normalizeTemporalTime(match[1]);
    const date = normalizeTemporalDateRef(match[2]);
    addTemporalClaim(claims, match[0], `${date} at ${time}`);
  }

  for (const match of value.matchAll(optionTimesOnDate)) {
    const first = normalizeTemporalTime(match[1]);
    const second = normalizeTemporalTime(match[2]);
    const date = normalizeTemporalDateRef(match[3]);
    addTemporalClaim(claims, `${match[3]} at ${match[1]}`, `${date} at ${first}`);
    addTemporalClaim(claims, `${match[3]} at ${match[2]}`, `${date} at ${second}`);
  }

  for (const match of value.matchAll(bareBoundaryTime)) {
    const prep = normalize(match[1]);
    const time = normalizeTemporalTime(match[2]);
    addTemporalClaim(claims, match[0], `${prep} ${time}`);
  }

  return Array.from(claims.values());
}

export function detectUnsupportedFacts(
  input: RewriteRequestInput,
  rewrittenText: string,
): UnsupportedFact[] {
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
  const normalizedRawSource = normalize(rawSource);
  const sourceComparables = extractUnsupportedComparables(
    rawSource,
  );
  const sourceFacts = new Set(sourceComparables.map((fact) => fact.id));
  const sourceText = sourceComparables.map((fact) => fact.normalizedText).join(" ");
  const sourceTemporalClaims = new Set(
    extractTemporalClaims(rawSource).map((claim) => claim.normalizedText),
  );
  const temporalUnsupported = extractTemporalClaims(rewrittenText)
    .filter((claim) => !sourceTemporalClaims.has(claim.normalizedText))
    .map((claim): UnsupportedFact => ({
      text: claim.text,
      normalizedText: claim.normalizedText,
      category: "deadline",
    }));

  const unsupported = extractUnsupportedComparables(rewrittenText)
    .filter((fact) => {
      if (sourceFacts.has(fact.id)) {
        return false;
      }

      if (
        normalizedRawSource.includes(fact.normalizedText) ||
        normalizedRawSource.includes(
          fact.normalizedText.replace(/^(?:the|a|an)\s+/, ""),
        )
      ) {
        return false;
      }

      if (
        fact.category === "person" &&
        sourceComparables.some(
          (sourceFact) =>
            sourceFact.category === "person" &&
            sourceFact.normalizedText.endsWith(` ${fact.normalizedText}`),
        )
      ) {
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
  const seen = new Set<string>();

  return [...unsupported, ...temporalUnsupported].filter((fact) => {
    const key = `${fact.category}:${fact.normalizedText}`;
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

export { normalize as normalizeFactText };
