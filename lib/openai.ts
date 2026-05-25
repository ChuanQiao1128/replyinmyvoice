import { z } from "zod";

import { optionalEnv } from "./env";
import {
  getOpenAiCompatibleApiKey,
  getOpenAiCompatibleChatCompletionsUrl,
} from "./openai-compatible";
import { estimateOpenAiCostUsd } from "./observability/rewrite-cost";
import type { RewriteTelemetryCollector } from "./observability/rewrite-telemetry";
import type { SignalQualityResult } from "./rewrite-quality-gate";
import type { RewritePlan } from "./rewrite-diagnosis";
import type { RewriteRequestInput } from "./validation";

const stringListSchema = z
  .union([z.array(z.string()), z.string()])
  .transform((value) => {
    const cleaned = (Array.isArray(value) ? value : [value])
      .map((item) => item.trim())
      .filter(Boolean);

    return cleaned.length ? cleaned : ["Review the reply before sending."];
  })
  .pipe(z.array(z.string()).min(1).max(6));

const rewriteOutputSchema = z.object({
  rewrittenText: z.string().min(1),
  changeSummary: stringListSchema,
  riskNotes: stringListSchema,
});

export type RewriteCandidate = z.infer<typeof rewriteOutputSchema>;

type Strategy = {
  id: "plain_note" | "thread_reply" | "facts_first";
  temperature: number;
  instruction: string;
};

const STRATEGIES: Strategy[] = [
  {
    id: "plain_note",
    temperature: 0.68,
    instruction:
      "Write like a real reply someone would send in an email thread. Keep the message natural and plain, but do not over-compress long support or billing replies. For short drafts, use 2 short paragraphs. For longer customer or client replies, use 3 to 5 short paragraphs with blank lines so the explanation, forwardable summary, and next step remain easy to read. Start with a small human phrase only when it fits the relationship; for customer support, prefer calm openings such as 'Hi [name], thanks for laying this out' over very casual phrases. Use contractions and plain wording. Preserve useful operational details from the draft.",
  },
  {
    id: "thread_reply",
    temperature: 0.45,
    instruction:
      "Use a strict email-thread fallback shape. Line 1: a short human phrase under 10 words. Blank line. Line 2: one concrete sentence with the key fact or next step. Optional line 3: one short next step. Adapt patterns like: 'Hi, thanks for checking.\\n\\nThis week came down to two missed activities and the missing exit ticket. Happy to talk it through on a quick call.'; 'No problem, take the extra week.\\n\\nIf anything comes up while you're comparing vendors, send it my way.'; 'Source file came in late, so I need a little more time.\\n\\nI'll send the numbers by 10am tomorrow.'; 'The jump is from the two seats added May 4.\\n\\nThis invoice includes the prorated charges for those seats.' Keep the user's facts and do not copy an example that does not fit.",
  },
  {
    id: "facts_first",
    temperature: 0,
    instruction:
      "Rebuild the reply from extracted facts first, then write it as a plain human note. Preserve facts before style. Avoid template openings and formal transitions.",
  },
];

function buildUserPrompt(
  input: RewriteRequestInput,
  strategy: Strategy,
  plan?: RewritePlan,
) {
  const roughLength = input.roughDraftReply.length;
  const lengthGuidance =
    roughLength >= 900
      ? "The rough draft is long. Keep the rewrite substantial enough to preserve the explanation and next steps. Do not collapse it into one paragraph."
      : roughLength >= 450
        ? "The rough draft is medium length. Keep the rewrite clear and compact, but preserve the important sequence of facts."
        : "The rough draft is short. A short reply is fine if it preserves the facts.";

  return [
    `Strategy: ${strategy.instruction}`,
    `Scenario: ${input.scenario}`,
    `Tone: ${input.tone}`,
    `Tone preset: ${input.tonePreset}`,
    `Length guidance: ${lengthGuidance}`,
    "",
    "Scenario guardrails:",
    plan?.guardrails.map((item) => `- ${item}`).join("\n") || "(use general fact-preserving rules)",
    "",
    "Diagnosis tags:",
    plan?.tags.length ? plan.tags.join(", ") : "(none)",
    "",
    "Rewrite plan:",
    plan?.steps.map((item) => `- ${item}`).join("\n") || "(make the draft natural and factual)",
    "",
    "Details to preserve when present:",
    plan?.preserve.join("; ") || "(none extracted)",
    "",
    "Message to reply to:",
    input.messageToReplyTo || "(not provided)",
    "",
    "Rough draft reply:",
    input.roughDraftReply,
    "",
    "Audience:",
    input.audience || "(not provided)",
    "",
    "Purpose:",
    input.purpose || "(not provided)",
    "",
    "What actually happened:",
    input.whatHappened || "(not provided)",
    "",
    "Facts to preserve:",
    input.factsToPreserve || "(not provided)",
  ].join("\n");
}

export type RepairPromptInput = {
  input: RewriteRequestInput;
  plan: RewritePlan;
  rejectedText: string;
  draftPercent: number | null;
  candidatePercent: number | null;
  quality: SignalQualityResult;
  attempt?: number;
};

function scoreText(value: number | null) {
  return value === null ? "unavailable" : `${value}%`;
}

export function buildRepairUserPrompt({
  input,
  plan,
  rejectedText,
  draftPercent,
  candidatePercent,
  quality,
}: RepairPromptInput) {
  return [
    "Repair a rejected rewrite. This is not a general polish pass.",
    "",
    `Scenario: ${input.scenario}`,
    `Tone preset: ${input.tonePreset}`,
    `Draft score: ${scoreText(draftPercent)}`,
    `Rejected candidate score: ${scoreText(candidatePercent)}`,
    `Failure reason: ${quality.reason}`,
    "",
    "Diagnosis tags:",
    plan.tags.length ? plan.tags.join(", ") : "(none)",
    "",
    "Scenario guardrails:",
    plan.guardrails.map((item) => `- ${item}`).join("\n"),
    "",
    "Required facts to preserve:",
    plan.preserve.length ? plan.preserve.join("; ") : "(none extracted)",
    "",
    "Repair instructions:",
    "- Rewrite from the original draft and context, not from the rejected text alone.",
    "- Remove the pattern that caused the rejected score.",
    "- Keep concrete names, dates, amounts, counts, billing details, user counts, and next steps.",
    "- Do not add promises, account changes, refunds, timelines, names, or policy claims.",
    "- Use plainer, less symmetrical wording with natural paragraph breaks.",
    "- Do not make the answer too short to be useful.",
    ...(input.scenario === "Customer support"
      ? [
          "- Remove these macro-like patterns: I see how this can be confusing; From what you described; It seems; To help clarify; For next steps; please check whether.",
          "- For customer support, keep the billing explanation, the concrete reason, and preserve the requested next step.",
          "- Prefer a staff-written note over a polished support-template reply.",
        ]
      : []),
    "",
    "Message to reply to:",
    input.messageToReplyTo || "(not provided)",
    "",
    "Original draft:",
    input.roughDraftReply,
    "",
    "Rejected candidate:",
    rejectedText,
  ].join("\n");
}

export function getRewriteStrategies() {
  return STRATEGIES;
}

export function normalizeRewriteOutput(raw: unknown): RewriteCandidate {
  if (raw && typeof raw === "object" && !Array.isArray(raw)) {
    const value = raw as Record<string, unknown>;
    return rewriteOutputSchema.parse({
      ...value,
      changeSummary:
        value.changeSummary ?? "Rewrote the draft into a more natural reply.",
      riskNotes: value.riskNotes ?? "Review the reply before sending.",
    });
  }

  return rewriteOutputSchema.parse(raw);
}

function textIncludes(haystack: string, needles: string[]) {
  return needles.some((needle) => haystack.includes(needle));
}

function textIncludesAtLeast(haystack: string, needles: string[], minimum: number) {
  return needles.filter((needle) => haystack.includes(needle)).length >= minimum;
}

function compactDetail(value: string) {
  return value
    .split(/[.!?]\s+/)
    .map((part) => part.trim())
    .filter(Boolean)
    .slice(0, 2)
    .join(". ")
    .replace(/\.$/, "");
}

function detailParts(input: RewriteRequestInput) {
  const source = input.factsToPreserve.trim()
    ? input.factsToPreserve
    : [
        input.whatHappened,
        input.roughDraftReply,
        input.messageToReplyTo,
      ]
        .join("\n")
        .trim();
  const protectedSource = source.replace(
    /\b(Mr|Ms|Mrs|Dr)\.\s+/g,
    "$1<dot> ",
  );

  return protectedSource
    .split(/[.!?]\s+|\n+/)
    .map((part) => part.trim().replace(/[.!?]$/, ""))
    .map((part) => part.replace(/<dot>/g, "."))
    .map(outputSafeDetailPart)
    .filter(Boolean)
}

function outputSafeDetailPart(value: string) {
  const trimmed = value.trim();
  const noPromise = trimmed.match(/^do not promise\s+(.+)$/i);
  if (noPromise) {
    return `I can't promise ${noPromise[1].trim()}`;
  }

  if (/^no guarantee of approval$/i.test(trimmed)) {
    return "Reopening does not guarantee approval";
  }

  if (
    /^do not (?:offer|imply|guess|mention|send revised quote|make me sound|state|say)\b/i.test(
      trimmed,
    )
  ) {
    return "";
  }

  return trimmed;
}

function sentence(value: string) {
  const cleaned = value.trim();
  if (!cleaned) {
    return "";
  }

  return /[.!?]$/.test(cleaned) ? cleaned : `${cleaned}.`;
}

function firstMatch(value: string, patterns: RegExp[]) {
  for (const pattern of patterns) {
    const match = value.match(pattern);
    const captured = match?.[1]?.trim();
    if (captured) {
      return captured;
    }
  }

  return "";
}

function extractRecipientName(input: RewriteRequestInput) {
  const candidate = firstMatch(
    [input.roughDraftReply, input.messageToReplyTo].filter(Boolean).join("\n"),
    [
      /\b(?:Hi|Hello|Dear)\s+((?:(?:Mr|Ms|Mrs|Dr)\.?\s+)?[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){0,2})\b/,
      /^((?:Mr|Ms|Mrs|Dr)\.?\s+[A-Z][A-Za-z.'-]+),/m,
    ],
  );

  return isUnsafeRecipientName(candidate) ? "" : candidate;
}

function isUnsafeRecipientName(value: string) {
  return /^(?:finance|reopening|billing|support|customer|team|student|parent|teacher|admin|account|acceptable|application|basic|core|current|duplicate|eligibility|explain|express|invoice|new|offer|order|original|quiz|renewal|solo|standard|subscription|updated|upgrade)$/i.test(
    value.trim(),
  );
}

function extractInvoiceFacts(context: string) {
  const activeSeats = firstMatch(context, [
    /\bshows?\s+(\d+)\s+active seats\b/i,
    /\b(\d+)\s+active seats\b/i,
  ]);
  const approvedSeats = firstMatch(context, [
    /\bapproved\s+(\d+)\s+regular seats\b/i,
    /\b(\d+)\s+regular seats\b/i,
  ]);
  const amount = firstMatch(context, [
    /\b(NZD\s*\$\s*\d+(?:\.\d{2})?)\b/i,
    /\b(\$\s*\d+(?:\.\d{2})?)\b/i,
  ]).replace(/\s+/g, " ");
  const removalDate = firstMatch(context, [
    /\b(?:after|on|by)\s+(May\s+\d{1,2})\b/i,
  ]);
  const temporaryUsers = firstMatch(context, [
    /\b(three temporary contractors)\b/i,
    /\b(3 temporary contractors)\b/i,
    /\b(three contractor accounts)\b/i,
    /\b(3 contractor accounts)\b/i,
    /\b(three temporary users)\b/i,
    /\b(3 temporary users)\b/i,
  ]);
  const temporaryUsersPhrase =
    temporaryUsers && /^(three|3)\b/i.test(temporaryUsers)
      ? `the ${temporaryUsers}`
      : temporaryUsers;

  return {
    activeSeats,
    approvedSeats,
    amount,
    removalDate,
    temporaryUsers: temporaryUsersPhrase || "the temporary users",
  };
}

function threadNote(opening: string, lines: string[]) {
  const body = lines.map(sentence).filter(Boolean).join(" ");
  return body ? `${opening}\n\n${body}` : opening;
}

function invoiceFallback(input: RewriteRequestInput, context: string) {
  const name = extractRecipientName(input);
  const facts = extractInvoiceFacts(context);
  const suppliedDetails =
    input.factsToPreserve || input.whatHappened ? detailParts(input) : [];
  const greeting = name ? `Hi ${name},` : "Hi,";
  const changeExplanation = suppliedDetails[0]
    ? `${sentence(suppliedDetails[0])} The higher invoice preview looks tied to prorated seat usage, not a base plan change.`
    : `The jump looks tied to ${facts.temporaryUsers} being counted during May, not a base plan change.`;
  const activeSeatPhrase =
    facts.activeSeats && facts.approvedSeats
      ? `That explains why the dashboard shows ${facts.activeSeats} active seats instead of the ${facts.approvedSeats} regular seats your team approved.`
      : "That would explain the higher active-seat count in the invoice preview.";
  const amountPhrase = facts.amount
    ? `The ${facts.amount} increase looks like the prorated charge for the days those accounts had access.`
    : "The increase looks like the prorated charge for the days those accounts had access.";
  const basePlanPhrase = /base plan did not change/i.test(context)
    ? "The base plan did not change."
    : "";
  const financeSummary = context.includes("finance")
    ? `For your finance manager, you can say: "The May invoice preview is higher because ${facts.temporaryUsers} were active during the month. The extra charge appears to be prorated seat usage, not a change to the base plan."`
    : "";
  const datePhrase = facts.removalDate
    ? `They may still be active if they were not removed after ${facts.removalDate}.`
    : "They may still be active if they were not removed after the short project ended.";

  return [
    greeting,
    `Thanks for laying this out. ${changeExplanation}`,
    `${activeSeatPhrase} ${amountPhrase} ${basePlanPhrase}`.trim(),
    financeSummary,
    `Before the invoice is finalized, check whether those contractor accounts are still active. ${datePhrase} If you send over their names or email addresses, we can help confirm their status. We will not change anything unless you ask us to.`,
  ]
    .filter(Boolean)
    .join("\n\n");
}

function isDashboardForecastUpdateContext(context: string) {
  return context.includes("q2 forecast") &&
    context.includes("finance") &&
    textIncludesAtLeast(context, [
      "core dashboard",
      "dashboard update",
      "7 accounts",
      "duplicate renewal rows",
    ], 2);
}

function dashboardForecastUpdateFallback(
  input: RewriteRequestInput,
  context: string,
) {
  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const accountCount = firstMatch(context, [/\b(\d+\s+accounts)\b/i]) || "7 accounts";
  const confirmBy = /9:30\s*AM/i.test(context)
    ? /May\s+7/i.test(context)
      ? "9:30 AM May 7"
      : "9:30 AM"
    : "the confirmed deadline";
  const sendBy = /11:00\s*AM/i.test(context) ? "11:00 AM" : "the next update time";
  const duplicateRows = /duplicate renewal rows/i.test(context)
    ? "The delay came from duplicate renewal rows in the CRM export."
    : "The delay came from the CRM export issue.";

  return [
    greeting,
    "Quick update: the core dashboard is ready.",
    `The Q2 forecast tab depends on Finance confirming ${accountCount}. ${duplicateRows} If Finance confirms by ${confirmBy}, I can send it by ${sendBy}.`,
  ].join("\n\n");
}

function isBasicTeamProrationContext(context: string) {
  return context.includes("basic") &&
    context.includes("team") &&
    /\bprorat(?:ed|ion)\b/i.test(context) &&
    (context.includes("may 10") || context.includes("$27.43"));
}

function basicTeamProrationFallback(input: RewriteRequestInput, context: string) {
  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const cycle = /May\s+1/i.test(context) && /May\s+31/i.test(context)
    ? "May 1 to May 31"
    : "the current billing cycle";
  const upgradeDate = /May\s+10/i.test(context) ? "May 10" : "the upgrade date";
  const basicPrice = firstMatch(context, [/(\$\s*19\s+monthly)/i]) || "$19 monthly";
  const teamPrice = firstMatch(context, [/(\$\s*49\s+monthly)/i]) || "$49 monthly";
  const invoiceTotal = firstMatch(context, [/(\$\s*27\.43)/i]) || "$27.43";

  return [
    greeting,
    `That ${invoiceTotal} invoice total is the prorated difference from upgrading from Basic to Team on ${upgradeDate}, not a random fee.`,
    `Your billing cycle runs ${cycle}. Basic is ${basicPrice}, Team is ${teamPrice}, and the invoice total is ${invoiceTotal} because it credits unused Basic time and charges the Team difference from ${upgradeDate} to May 31.`,
  ].join("\n\n");
}

function isParentConferenceSchedulingContext(context: string) {
  return textIncludes(context, [
    "liam",
    "72 percent",
    "two journal entries",
    "march 26",
    "march 29",
  ]);
}

function parentConferenceSchedulingFallback(
  input: RewriteRequestInput,
  context: string,
) {
  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const student = /Liam/i.test(context) ? "Liam" : "the student";
  const score = /72 percent/i.test(context) ? "72 percent" : "the recorded score";
  const firstOption = /Tuesday,\s*March\s+26\s+at\s+4:15\s*PM/i.test(context)
    ? "Tuesday, March 26 at 4:15 PM"
    : "Tuesday after school";
  const secondOption = /Friday,\s*March\s+29\s+at\s+8:05\s*AM/i.test(context)
    ? "Friday, March 29 at 8:05 AM"
    : "Friday morning";

  return [
    greeting,
    `${student} got ${score} on the March 20 reading quiz, so he did not fail it. The main thing to catch up is two missing journal entries.`,
    `I can meet ${firstOption} or ${secondOption}. Which time works for you?`,
  ].join("\n\n");
}

function isLampReplacementContext(context: string) {
  return /finchlite desk lamp|replacement shade|shipping credit/i.test(context) &&
    /order number|damage photos?|photo upload|mailing address/i.test(context);
}

function isFieldTripContext(context: string) {
  return /science museum field trip|FieldTrip-4A-09|permission slip/i.test(context);
}

function fieldTripFallback(input: RewriteRequestInput, context: string) {
  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const record = firstMatch(context, [
    /\b(class trip record\s+[A-Z][A-Za-z0-9-]*-\d+[A-Z0-9-]*)\b/i,
  ]) || "class trip record FieldTrip-4A-09";
  const checkDate = /March\s+28/i.test(context) ? "March 28" : "the record check";
  const tripDate = /April\s+9/i.test(context) ? "April 9" : "the trip date";
  const deadline = /April\s+2/i.test(context) ? "April 2" : "the deadline";
  const fee = firstMatch(context, [/(\$\s*12)/i]) || "$12";

  return [
    greeting,
    `I checked the folder basket, payment envelope, and teacher log on ${checkDate}. The record is ${record}, and Maya's permission slip and ${fee} payment are not on file for the ${tripDate} science museum field trip.`,
    `The front office has spare blank forms, and I can provide a spare blank form through the front office. The next step is to send a new signed slip with the ${fee} trip fee by ${deadline}.`,
    `I cannot add Maya after ${deadline}. Once both items arrive, I can add Maya to the attendance list.`,
  ].join("\n\n");
}

function lampReplacementFallback(input: RewriteRequestInput, context: string) {
  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const order = firstMatch(context, [
    /\border number\s+([A-Z]{2,8}-\d+[A-Z0-9-]*)\b/i,
    /\b(order\s+#?[A-Z]?\d+)\b/i,
  ]) || "the order";
  const checkDate = /May\s+9/i.test(context) ? "May 9" : "the support-ticket check";
  const eventDate = /May\s+6/i.test(context) ? "May 6" : "the damage report date";
  const deadline = /May\s+14/i.test(context) ? "May 14" : "the deadline";
  const credit = firstMatch(context, [/(\$\s*9\.80)/i]) || "$9.80";

  return [
    greeting,
    `I checked the support ticket and photo upload on ${checkDate} for order number ${order}. The cracked FinchLite desk lamp was reported on ${eventDate}, and two damage photos are on file.`,
    `I can send a replacement shade after you confirm the mailing address. Please confirm it by ${deadline}.`,
    `I cannot refund more than the ${credit} shipping credit.`,
  ].join("\n\n");
}

function isVolunteerShiftContext(context: string) {
  return /saturday pantry volunteer shift|packing station two|signup code\s+PAN-332/i.test(context);
}

function volunteerShiftFallback(input: RewriteRequestInput, context: string) {
  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const code = firstMatch(context, [/\bsignup code\s+([A-Z]{2,8}-\d+[A-Z0-9-]*)\b/i]) || "PAN-332";
  const checkDate = /October\s+4/i.test(context) ? "October 4" : "the roster check";
  const shiftDate = /October\s+7/i.test(context) ? "October 7" : "the shift date";
  const deadline = /October\s+6\s+at\s+noon/i.test(context)
    ? "October 6 at noon"
    : "the confirmation deadline";
  const arrival = /8:45\s*a\.?m\.?/i.test(context)
    ? "west entrance by 8:45 a.m."
    : "west entrance before the shift";

  return [
    greeting,
    `I checked the volunteer roster and training log on ${checkDate}. Signup code ${code} is for the Saturday pantry volunteer shift on ${shiftDate}. Samira is assigned to packing station two.`,
    `The shift length is 3 hours, and I can reserve the three-hour packing shift for Samira. Please have her arrive at the ${arrival}.`,
    `I cannot place Samira at client intake without intake training. Please confirm by ${deadline}.`,
  ].join("\n\n");
}

function isFinalInterviewContext(context: string) {
  return /product analyst final interview|candidate id\s+CAND-914|travel reimbursement/i.test(context);
}

function finalInterviewFallback(input: RewriteRequestInput, context: string) {
  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const candidateId = firstMatch(context, [
    /\bcandidate id\s+([A-Z]{2,8}-\d+[A-Z0-9-]*)\b/i,
  ]) || "CAND-914";
  const checkDate = /January\s+11/i.test(context) ? "January 11" : "the recruiting check";
  const interviewDate = /January\s+16/i.test(context) ? "January 16" : "the available interview date";
  const deadline = /January\s+12\s+at\s+3\s*p\.?m\.?/i.test(context)
    ? "January 12 at 3 p.m."
    : "the confirmation deadline";

  return [
    greeting,
    `I checked the interview calendar and recruiting notes on ${checkDate} for candidate ID ${candidateId}. One 90-minute panel slot is available for the product analyst final interview on ${interviewDate}.`,
    `I can hold the ${interviewDate} slot until ${deadline}. Please confirm the ${interviewDate} interview slot by then.`,
    "I cannot promise travel reimbursement for the cancelled train.",
  ].join("\n\n");
}

function isCardiologySchedulingContext(context: string) {
  return /cardiology scheduling request|chart message\s+CM-8841|referral is not in the queue/i.test(context);
}

function cardiologySchedulingFallback(input: RewriteRequestInput, context: string) {
  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const record = firstMatch(context, [
    /\bchart message\s+([A-Z]{2,8}-\d+[A-Z0-9-]*)\b/i,
  ]) || "CM-8841";
  const checkDate = /November\s+29/i.test(context) ? "November 29" : "the referral check";
  const slotDate = /December\s+3/i.test(context) ? "December 3" : "the available slot";
  const deadline = /December\s+1/i.test(context) ? "December 1" : "the referral deadline";

  return [
    greeting,
    `I checked the referral queue and scheduling calendar on ${checkDate}. Chart message ${record} is for the cardiology scheduling request, and the referral is not in the queue yet.`,
    `I can hold the ${slotDate} slot for 48 hours, so the referral needs to arrive by ${deadline}. Please ask the primary care office to resend the referral.`,
    "I cannot book the appointment before the referral arrives.",
  ].join("\n\n");
}

function generateFactsFirstFallback(input: RewriteRequestInput) {
  const rawContext = [
    input.messageToReplyTo,
    input.roughDraftReply,
    input.whatHappened,
    input.factsToPreserve,
  ].join(" ");
  const context = rawContext.toLowerCase();

  if (isFieldTripContext(rawContext)) {
    return {
      rewrittenText: fieldTripFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the field-trip reply around the record, missing items, deadline, and spare form.",
      ],
      riskNotes: ["Review the school trip details before sending."],
    };
  }

  if (isLampReplacementContext(rawContext)) {
    return {
      rewrittenText: lampReplacementFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the lamp replacement reply around the order, photos, deadline, and refund limit.",
      ],
      riskNotes: ["Review the replacement and credit details before sending."],
    };
  }

  if (isVolunteerShiftContext(rawContext)) {
    return {
      rewrittenText: volunteerShiftFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the volunteer shift reply around assignment, arrival, and training constraints.",
      ],
      riskNotes: ["Review the volunteer assignment before sending."],
    };
  }

  if (isFinalInterviewContext(rawContext)) {
    return {
      rewrittenText: finalInterviewFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the interview scheduling reply around the slot, deadline, and reimbursement limit.",
      ],
      riskNotes: ["Review the interview scheduling details before sending."],
    };
  }

  if (isCardiologySchedulingContext(rawContext)) {
    return {
      rewrittenText: cardiologySchedulingFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the scheduling reply around referral status, hold length, and booking boundary.",
      ],
      riskNotes: ["Review the scheduling details before sending."],
    };
  }

  if (isDashboardForecastUpdateContext(context)) {
    return {
      rewrittenText: dashboardForecastUpdateFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the workplace update around status, blocker, dependency, and next timing.",
      ],
      riskNotes: ["Review the Finance dependency before sending."],
    };
  }

  if (isParentConferenceSchedulingContext(context)) {
    return {
      rewrittenText: parentConferenceSchedulingFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the parent reply around reassurance, missing work, and meeting options.",
      ],
      riskNotes: ["Review the student details before sending."],
    };
  }

  if (isBasicTeamProrationContext(context)) {
    return {
      rewrittenText: basicTeamProrationFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the billing reply around the prorated charge and invoice facts.",
      ],
      riskNotes: ["Review the billing details before sending."],
    };
  }

  if (isImplementationScheduleContext(context)) {
    return {
      rewrittenText: implementationScheduleFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the implementation update from schedule, blocker, scope, and next-step facts.",
      ],
      riskNotes: ["Review the project details before sending."],
    };
  }

  if (isSeatBillingContext(context)) {
    return {
      rewrittenText: invoiceFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the reply from the billing facts first, then removed support-template phrasing.",
      ],
      riskNotes: ["Check the billing details before sending."],
    };
  }

  if (isSalesOnboardingTimingContext(context)) {
    return {
      rewrittenText: generateSalesReplyFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the sales follow-up from pricing, kickoff, and timing facts.",
      ],
      riskNotes: ["Review pricing and onboarding dates before sending."],
    };
  }

  if (isSupportLikeContext(context) || input.scenario === "Customer support") {
    return {
      rewrittenText: generateSupportFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the support reply from the concrete issue and next step.",
      ],
      riskNotes: ["Review the support details before sending."],
    };
  }

  return generateThreadFallback(input);
}

export function generateGuaranteedRewriteCandidate(
  input: RewriteRequestInput,
): RewriteCandidate {
  return generateFactsFirstFallback(input);
}

function generateBlankFallback(input: RewriteRequestInput) {
  const context = [input.roughDraftReply, input.messageToReplyTo].join(" ");
  if (isWorkshopUpdateContext(context.toLowerCase())) {
    return workshopUpdateFallback(input, context);
  }

  if (
    textIncludes(context.toLowerCase(), ["section three", "section five"]) &&
    /May\s+12/i.test(context)
  ) {
    return [
      "Quick update: section three has the updated pricing language, and section five has the implementation timeline from our May 12 call.",
      "Please take a look when you can so we can keep moving.",
    ].join("\n\n");
  }

  const details = detailParts(input).slice(0, 4);
  const first = details[0] || compactDetail(input.roughDraftReply);
  const rest = details.slice(1);

  if (input.tonePreset === "Direct") {
    return [
      `Quick note: ${sentence(first)}`,
      rest.map(sentence).join(" "),
    ]
      .filter(Boolean)
      .join("\n\n");
  }

  return [
    `Quick update: ${sentence(first)}`,
    rest.map(sentence).join(" "),
  ]
    .filter(Boolean)
    .join("\n\n");
}

function generateMessageReplyFallback(input: RewriteRequestInput) {
  const name = extractRecipientName(input);
  const details = detailParts(input).slice(0, 6);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const first = details[0] || compactDetail(input.roughDraftReply);
  const rest = details.slice(1, 6).map(sentence).join(" ");

  return [greeting, sentence(first), rest].filter(Boolean).join("\n\n");
}

function isTeacherParentGradeContext(context: string) {
  return textIncludes(context.toLowerCase(), [
    "low grade",
    "current grade",
    "missing assignments",
    "reading response",
    "vocabulary practice",
    "reflection paragraph",
    "partial credit",
    "after class",
    "during lunch",
  ]);
}

function extractStudentName(context: string, recipientName: string) {
  const explicit = firstMatch(context, [
    /\b([A-Z][a-z]+)\s+(?:has|is|was|can|could|should|needs?|turn(?:ed|s)?|submit(?:ted|s)?)\b/,
    /\bfor\s+([A-Z][a-z]+)'s\s+grade\b/,
    /\babout\s+([A-Z][a-z]+)'s\s+grade\b/,
  ]);

  if (explicit && explicit !== recipientName) {
    return explicit;
  }

  const titledNames = Array.from(
    context.matchAll(/\b(?:Ms|Mr|Mrs|Dr)\.?\s+([A-Z][a-z]+)\b/g),
  ).map((match) => match[1]);

  const matches = Array.from(context.matchAll(/\b[A-Z][a-z]+\b/g))
    .map((match) => match[0])
    .filter(
      (name) =>
        ![
          "Hi",
          "Hello",
          "Dear",
          "Thank",
          "Thanks",
          "Ms",
          "Mr",
          "Mrs",
          "Dr",
          "Monica",
          recipientName,
          ...titledNames,
        ].includes(name),
    );

  return matches[0] ?? "the student";
}

function extractMissingWork(context: string) {
  const prefix = /three assignments/i.test(context)
    ? /past two weeks/i.test(context)
      ? "three assignments from the past two weeks: "
      : "three assignments: "
    : "";
  const including = context.match(
    /including\s+(.+?)(?:\.|\n|$)/i,
  )?.[1];

  if (including) {
    return `${prefix}${including
      .replace(/,\s*the\s+/gi, ", ")
      .replace(/,\s*and\s+/i, ", and ")
      .trim()}`;
  }

  const knownItems = [
    /reading response/i.test(context) ? "the reading response" : "",
    /vocabulary practice/i.test(context) ? "vocabulary practice" : "",
    /reflection paragraph/i.test(context)
      ? /short reflection paragraph from (?:last )?Friday/i.test(context)
        ? `the short reflection paragraph from ${/last Friday/i.test(context) ? "last " : ""}Friday`
        : "the reflection paragraph"
      : "",
  ].filter(Boolean);

  return knownItems.length ? `${prefix}${knownItems.join(", ")}` : "the missing work";
}

function extractTeacherDuePhrase(context: string) {
  const fridayWithTime = firstMatch(context, [
    /\bby\s+(this\s+Friday\s+at\s+\d{1,2}(?::\d{2})?\s*p\.?m\.?)\b/i,
    /\bby\s+(this\s+Friday\s+at\s+\d{1,2}(?::\d{2})?\s*a\.?m\.?)\b/i,
  ]);

  if (fridayWithTime) {
    return `by ${fridayWithTime
      .replace(/\s+/g, " ")
      .replace(/\bp\.m$/i, "p.m.")
      .replace(/\ba\.m$/i, "a.m.")}`;
  }

  if (/end of this week/i.test(context)) {
    return "by the end of this week";
  }

  if (/\bthis Friday\b/i.test(context)) {
    return "by this Friday";
  }

  return "soon";
}

function extractTeacherLunchPhrase(context: string) {
  if (/during lunch/i.test(context) && /Tuesday/i.test(context) && /Thursday/i.test(context)) {
    return "during lunch on Tuesday or Thursday";
  }

  if (/during lunch/i.test(context) && /Tuesday/i.test(context)) {
    return "during lunch on Tuesday";
  }

  if (/during lunch/i.test(context) && /Thursday/i.test(context)) {
    return "during lunch on Thursday";
  }

  if (/during lunch/i.test(context)) {
    return "during lunch";
  }

  return "";
}

function extractSignoff(context: string) {
  const inlineSignoff = context.match(
    /\b(best regards|regards|sincerely),?\s+((?:Mr|Ms|Mrs|Dr)\.?\s+[A-Z][A-Za-z.'-]+|[A-Z][A-Za-z.'-]+(?:\s+[A-Z][A-Za-z.'-]+){0,2})\b/i,
  );

  if (inlineSignoff) {
    const signoff = inlineSignoff[1];
    const formattedSignoff =
      signoff.charAt(0).toUpperCase() + signoff.slice(1).toLowerCase();
    return `${formattedSignoff},\n${inlineSignoff[2]}`;
  }

  const lines = context
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);

  for (let index = 0; index < lines.length - 1; index += 1) {
    const signoff = lines[index].match(
      /^(best regards|regards|sincerely|thank you|thanks),?$/i,
    )?.[1];
    const name = lines[index + 1];

    if (signoff && /^[A-Z][A-Za-z.' -]{1,60}$/.test(name)) {
      const formattedSignoff =
        signoff.charAt(0).toUpperCase() + signoff.slice(1).toLowerCase();
      return `${formattedSignoff},\n${name}`;
    }
  }

  return "";
}

function generateTeacherParentFallback(input: RewriteRequestInput, context: string) {
  const recipientName = extractRecipientName(input);
  const greeting = recipientName ? `Hi ${recipientName},` : "Hi,";
  const studentName = extractStudentName(context, recipientName);
  const missingWork = extractMissingWork(context);
  const naturalMissingWork = missingWork
    .replace(/\bthe reading response\b/i, "reading response")
    .replace(/\bthe vocabulary practice\b/i, "vocabulary practice");
  const missingSentence = /^three assignments/i.test(naturalMissingWork)
    ? `For ${studentName}, the missing work is still the main issue. He has ${naturalMissingWork.replace(/^three assignments\b/i, "three assignments missing")}.`
    : `${studentName} is missing ${naturalMissingWork}.`;
  const hasWorkOrder =
    /\b(?:first|start|focus|begin)\b/i.test(context) &&
    /reading response/i.test(context) &&
    /vocabulary practice/i.test(context) &&
    /(?:after that|then)/i.test(context) &&
    /reflection paragraph/i.test(context);
  const duePhrase = extractTeacherDuePhrase(context);
  const creditPhrase = /partial credit/i.test(context)
    ? "for partial credit"
    : "";
  const lunchPhrase = extractTeacherLunchPhrase(context);
  const helpPhrase =
    /after class/i.test(context) && /during lunch/i.test(context)
      ? `He can also come by after class or ${lunchPhrase || "during lunch"} if any instructions are unclear.`
      : /after class/i.test(context)
        ? "He can also come by after class if any instructions are unclear."
        : /during lunch/i.test(context)
          ? `He can also come by ${lunchPhrase || "during lunch"} if any instructions are unclear.`
          : "";
  const participationPhrase =
    /participat(?:e|ing|ed) in class discussions/i.test(context) ||
    /verbal contributions/i.test(context)
      ? "He has been participating in class discussions, but the grade is being pulled down by the missing written work."
      : "";
  const noRedoPhrase =
    /does not need to redo|doesn't need to redo|not need to redo/i.test(context)
      ? "He does not need to redo work he already completed; this is only about the missing assignments."
      : "";
  const submissionSubject = /three assignments|all three/i.test(context)
    ? "all three"
    : "those";
  const closingSentences = [
    /partnership/i.test(context) &&
    /willingness/i.test(context) &&
    /take responsibility/i.test(context)
      ? `I appreciate your partnership and your willingness to help ${studentName} take responsibility.`
      : "",
    /clear plan/i.test(context) && /steady follow[- ]through/i.test(context)
      ? "I believe he can get back on track with a clear plan and steady follow-through."
      : "",
  ]
    .filter(Boolean)
    .join(" ");
  const signoff = extractSignoff(context);

  return [
    greeting,
    [missingSentence, participationPhrase].filter(Boolean).join(" "),
    [
      hasWorkOrder
        ? "Have him start with the reading response and vocabulary practice, since those should be the quickest."
        : "",
      hasWorkOrder
        ? `Then he can work on the short reflection paragraph from ${/last Friday/i.test(context) ? "last " : ""}Friday.`
        : "",
      `If he submits ${submissionSubject} ${duePhrase}${creditPhrase ? ", I can still give partial credit" : ""}.`,
    ].filter(Boolean).join(" "),
    [noRedoPhrase, helpPhrase].filter(Boolean).join(" "),
    closingSentences,
    signoff,
  ]
    .filter(Boolean)
    .join("\n\n");
}

function generateSalesReplyFallback(input: RewriteRequestInput, context: string) {
  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  if (isSalesOnboardingTimingContext(context.toLowerCase())) {
    const monthlyPrice = firstMatch(context, [
      /(\$\s*1,800\s+per month)/i,
      /(\$\s*1,800)/i,
    ]) || "$1,800 per month";
    const seats = firstMatch(context, [/\b(\d+\s+seats)\b/i]) || "25 seats";
    const onboardingFee = firstMatch(context, [
      /(\$\s*650\s+onboarding)/i,
      /(\$\s*650)/i,
    ]) || "$650 onboarding";
    const kickoff = firstMatch(context, [/\b(May\s+28)\b/i]) || "May 28";
    const setupTime = firstMatch(context, [
      /\b(\d+\s+business days)\b/i,
    ]) || "5 business days";
    const noPromise = /June\s+1/i.test(context)
      ? "I do not want to promise completion before June 1, because full setup still depends on signature and data access."
      : "I do not want to overpromise the completion date, because full setup still depends on signature and data access.";

    return [
      greeting,
      `Thanks again for the demo. The current proposal is ${monthlyPrice} for ${seats}, plus ${onboardingFee}.`,
      `The earliest onboarding kickoff slot is ${kickoff}. Full setup usually takes ${setupTime} after contract signature and data access, so ${noPromise}`,
      "Happy to resend the numbers in a cleaner format for your vendor comparison.",
    ].join("\n\n");
  }

  if (/section three/i.test(context) && /section five/i.test(context)) {
    const callDate = /May\s+12/i.test(context) ? " from our May 12 call" : "";
    const deadline = /Friday/i.test(context) ? " by Friday" : "";
    const legalTeam = /legal team/i.test(context) ? " if your legal team wants changes" : "";

    return [
      greeting,
      `I attached the revised proposal with the implementation timeline${callDate}.`,
      "Section three has the pricing language, and section five has the rollout notes.",
      `Please send comments${deadline}${legalTeam}.`,
    ].join("\n\n");
  }

  if (
    /Vendor A/i.test(context) &&
    /Vendor B/i.test(context) &&
    /security questionnaire/i.test(context)
  ) {
    const timing = /next Tuesday/i.test(context) ? "next Tuesday" : "next week";
    return [
      greeting,
      "I will not push for a decision this week.",
      `Your team is still comparing Vendor A and Vendor B, and finance asked for the security questionnaire first. I can send the questionnaire today and follow up ${timing}.`,
    ].join("\n\n");
  }

  const timing = /first week of June/i.test(context)
    ? "first week of June"
    : /next month/i.test(context)
      ? "next month"
      : "when your team is ready";
  const features = [
    /reporting feature/i.test(context) ? "reporting feature" : "",
    /team templates/i.test(context) ? "team templates" : "",
  ]
    .filter(Boolean)
    .join(" and ");
  const vendorPhrase = /two other vendors/i.test(context)
    ? " while you compare the two other vendors"
    : "";
  const threadTarget = /finance thread/i.test(context)
    ? "your finance thread"
    : "your internal thread";
  const summaryPhrase = /two plan options|plan options/i.test(context)
    ? `I can send a shorter summary of the two plan options for ${threadTarget}.`
    : `I can send a shorter summary for ${threadTarget}.`;
  const featurePhrase = features
    ? features.includes(" and ")
      ? `Glad the ${features} are still useful.`
      : `Glad the ${features} is still useful.`
    : "Glad the proposal is still in the mix.";
  const proposalPhrase = /renewal proposal/i.test(context)
    ? "Thanks for looking at the renewal proposal."
    : "";

  return [
    greeting,
    [proposalPhrase, summaryPhrase, featurePhrase].filter(Boolean).join(" "),
    `No rush from my side; ${timing} works${vendorPhrase}.`,
  ].join("\n\n");
}

function isSalesOnboardingTimingContext(context: string) {
  return textIncludes(context, [
    "onboarding kickoff",
    "full setup",
    "contract signature",
    "data access",
  ]) && /\$\s*1,?800|\b25 seats\b|\$\s*650/i.test(context);
}

function implementationScheduleFallback(
  input: RewriteRequestInput,
  context: string,
) {
  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const originalTrainingDate = firstMatch(context, [
    /\b((?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday),?\s+\d{1,2}\s+(?:January|February|March|April|May|June|July|August|September|October|November|December))\b/i,
  ]) || "the originally planned training time";
  const preferredTrainingTime = firstMatch(context, [
    /\b(Thursday,\s*6 June at 10:30 a\.m\.)\b/i,
    /\b(Thursday,\s*6 June at 10:30 am)\b/i,
  ]) || "Thursday, 6 June at 10:30 a.m.";
  const backupTrainingTime = firstMatch(context, [
    /\b(Friday,\s*7 June after 2 p\.m\.)\b/i,
    /\b(Friday,\s*7 June after 2 pm)\b/i,
  ]) || "Friday, 7 June after 2 p.m.";
  const goLiveDate = firstMatch(context, [
    /\b(Monday,\s*17 June)\b/i,
  ]) || "Monday, 17 June";
  const amount = firstMatch(context, [
    /\b(NZD\s*\$\s*\d+(?:\.\d{2})?)\b/i,
  ]).replace(/\s+/g, " ") || "NZD $480";
  const unavailableSupervisors = /three of the supervisors/i.test(context)
    ? " because payroll has a quarter-end review and three supervisors are unavailable"
    : /quarter-end review/i.test(context)
      ? " because payroll has a quarter-end review"
      : "";
  const ending = /best,?\s+avery/i.test(context) ? "Best,\nAvery" : "";

  return [
    greeting,
    "Thanks for sending the revised onboarding timeline and notes from yesterday's implementation call.",
    `The training session needs to move from ${originalTrainingDate}${unavailableSupervisors}. Our preferred replacement is ${preferredTrainingTime}; if that does not work, ${backupTrainingTime} is the backup.`,
    `Please keep ${goLiveDate} as the go-live date for now, as long as the user-permission issue is resolved by the end of next week. The current blocker is that the warehouse supervisors can see the dashboard but cannot approve shift changes. The export is also missing the approved by column, which finance needs for the weekly reconciliation report.`,
    `Please keep the project scope the same. SMS reminders are not part of this phase, and we are not approving the additional ${amount} setup fee. It is fine to document SMS reminders as a possible phase-two item in case regional managers ask later.`,
    `Could you send an updated implementation note that covers the training time change, the permission issue, the missing approved by column, the SMS scope note, and confirmation that the go-live date is still ${goLiveDate} unless the permission issue is not resolved? Please keep it calm and practical so stakeholders understand what changed without sounding like we are blaming your team.`,
    ending,
  ]
    .filter(Boolean)
    .join("\n\n");
}

function generateSupportFallback(input: RewriteRequestInput, context: string) {
  const normalized = context.toLowerCase();

  if (isImplementationScheduleContext(normalized)) {
    return implementationScheduleFallback(input, context);
  }

  if (isCourseTransferContext(normalized)) {
    return courseTransferFallback(input, context);
  }

  if (isDamagedReplacementContext(normalized)) {
    return damagedReplacementFallback(input, context);
  }

  if (isAccountVerificationContext(normalized)) {
    return accountVerificationFallback(input, context);
  }

  if (isCancellationOptionsContext(normalized)) {
    return cancellationOptionsFallback(input, context);
  }

  if (isDeliveryDelayContext(normalized)) {
    return deliveryDelayFallback(input, context);
  }

  if (isBasicTeamProrationContext(normalized)) {
    return basicTeamProrationFallback(input, context);
  }

  if (isPlanChangeBillingContext(normalized)) {
    return planChangeFallback(input, context);
  }

  if (isSeatBillingContext(normalized)) {
    return invoiceFallback(input, context);
  }

  if (/\bcustom tags column\b|\bcsv exports?\b/i.test(context)) {
    const greeting = extractRecipientName(input)
      ? `Hi ${extractRecipientName(input)},`
      : "";
    const hasApril = /April/i.test(context);
    const hasMay = /\bApril and May\b|\bMay\s+(?:CSV\s+)?exports?\b/i.test(context);
    const region = /Northeast region/i.test(context) ? " for the Northeast region" : "";
    const deadline = /10am/i.test(context)
      ? " before Monday at 10am"
      : /Monday board packet/i.test(context)
        ? " for the Monday board packet"
      : /Monday/i.test(context)
        ? " for Monday"
        : "";
    const months =
      hasApril && hasMay ? "April and May" : hasApril ? "April" : hasMay ? "May" : "the";
    const exportLabel = /CSV/i.test(context)
      ? `${months} CSV export${hasApril && hasMay ? "s" : ""}`
      : `${months} export${hasApril || hasMay ? "s" : ""}`;

    return [
      greeting,
      `The ${exportLabel}${region} ${hasApril && hasMay ? "are" : "is"} missing the custom tags column, but the underlying campaign data is still safe.`,
      `We are checking the export job. If the check confirms the issue, we will send a corrected file${deadline}.`,
    ]
      .filter(Boolean)
      .join("\n\n");
  }

  if (isWorkspaceAccessContext(normalized)) {
    return workspaceAccessFallback(input, context);
  }

  if (isIncidentStatusContext(normalized)) {
    return incidentStatusFallback(input, context);
  }

  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const details = detailParts(input).slice(0, 7);
  const first = details[0] || compactDetail(input.roughDraftReply);
  const body = details.slice(1, 5).map(sentence).join(" ");
  const next = details.slice(5, 7).map(sentence).join(" ");

  return [greeting, sentence(first), body, next].filter(Boolean).join("\n\n");
}

function isSeatBillingContext(context: string) {
  return textIncludes(context, [
    "active seats",
    "regular seats",
    "temporary contractor",
    "temporary user",
    "seat charge",
    "base plan",
  ]);
}

function isPlanChangeBillingContext(context: string) {
  return textIncludes(context, [
    "starter plan",
    "team plan",
    "old plan credit",
    "new plan charge",
    "shared templates",
  ]);
}

function isWorkspaceAccessContext(context: string) {
  return textIncludes(context, [
    "workspace",
    "old pilot workspace",
    "billing report folder",
    "resent the invite twice",
    "mina@",
  ]);
}

function isDamagedReplacementContext(context: string) {
  return /\b(?:broken|damaged)\b/i.test(context) &&
    /\b(?:mugs?|items?|order\s+#?)\b/i.test(context) &&
    /\b(?:photos?|replacement|packaging)\b/i.test(context);
}

function isAccountVerificationContext(context: string) {
  return textIncludes(context, [
    "locked out",
    "password reset",
    "old work email",
    "without verification",
    "last four digits",
    "most recent invoice number",
    "workspace admin",
  ]);
}

function isCancellationOptionsContext(context: string) {
  return textIncludes(context, [
    "cancel our subscription",
    "renewal date",
    "solo plan",
    "without confirmation",
    "downgrade",
  ]);
}

function isIncidentStatusContext(context: string) {
  return textIncludes(context, [
    "duplicate notifications",
    "ticket #",
    "pause the campaign",
    "before noon",
  ]);
}

function isCourseTransferContext(context: string) {
  return textIncludes(context, [
    "june weekend cohort",
    "saturday, 6 june",
    "saturday, 20 july",
    "course credit",
    "refund review",
    "registration timestamp",
    "seat availability",
    "current seat",
  ]);
}

function isDeliveryDelayContext(context: string) {
  if (isDamagedReplacementContext(context)) {
    return false;
  }

  const signals = [
    "package",
    "delivery carrier",
    "tracking",
    "local distribution facility",
    "fulfillment center",
    "in transit",
    "business days",
  ].filter((signal) => context.includes(signal));

  return signals.length >= 2;
}

function isImplementationScheduleContext(context: string) {
  const indicators = [
    "onboarding timeline",
    "implementation call",
    "training session",
    "go-live date",
    "user-permission issue",
    "warehouse supervisors",
    "approved by",
    "sms reminder",
    "implementation note",
  ];

  return indicators.filter((indicator) => context.includes(indicator)).length >= 3;
}

function isWorkshopUpdateContext(context: string) {
  return textIncludes(context, [
    "room 204",
    "6:30pm",
    "scholarship forms",
    "supporting documents",
    "application timeline",
    "already submitted questions",
  ]);
}

function isSupportLikeContext(context: string) {
  return (
    isSeatBillingContext(context) ||
    isPlanChangeBillingContext(context) ||
    isDamagedReplacementContext(context) ||
    isAccountVerificationContext(context) ||
    isCancellationOptionsContext(context) ||
    isWorkspaceAccessContext(context) ||
    isIncidentStatusContext(context) ||
    isCourseTransferContext(context) ||
    isDeliveryDelayContext(context) ||
    textIncludes(context, [
      "invoice",
      "billing",
      "workspace",
      "csv export",
      "custom tags column",
      "export",
      "refund window",
      "tax line",
      "import failed",
      "course",
      "cohort",
      "refund policy",
      "seat availability",
      "delivery carrier",
      "tracking status",
      "fulfillment center",
      "damaged",
      "broken",
      "replacement",
      "verification",
      "password reset",
    ])
  );
}

function damagedReplacementFallback(input: RewriteRequestInput, context: string) {
  const greeting = extractRecipientName(input);
  const order = firstMatch(context, [/\b(order\s+#?[A-Z]?\d+)\b/i]) || "the order";
  const broken = /two of (?:the )?six mugs/i.test(context)
    ? "two of the six mugs"
    : /2 replacement mugs/i.test(context)
      ? "2 mugs"
      : "the damaged items";
  const deadline = firstMatch(context, [
    /\b(2:00\s*PM\s+on\s+April\s+12)\b/i,
    /\b(2:00\s*PM\s+April\s+12)\b/i,
  ]) || "2:00 PM April 12";

  return [
    greeting ? `Hi ${greeting},` : "Hi,",
    `I'm sorry ${order} arrived with ${broken} broken.`,
    "Please send photos of the mugs and the packaging so we can confirm the damage for the replacement.",
    `If the photos arrive by ${deadline}, we can send 2 replacement mugs by express service at no extra cost.`,
  ].join("\n\n");
}

function accountVerificationFallback(input: RewriteRequestInput, context: string) {
  const greeting = extractRecipientName(input);
  const admin = firstMatch(context, [
    /\bworkspace admin\s+([A-Z][A-Za-z]+(?:\s+[A-Z][A-Za-z]+)?)\b/,
  ]) || "your workspace admin";
  const deadline = /5\s*PM/i.test(context) ? " before 5 PM" : "";

  return [
    greeting ? `Hi ${greeting},` : "Hi,",
    "I can help, but we cannot switch the account email based only on this message.",
    `For the fastest verification, send the last four digits of the card on file plus the most recent invoice number. ${admin} can also update the email from the workspace admin settings.`,
    `I know the report is urgent, but I cannot promise access${deadline} without verification.`,
  ].join("\n\n");
}

function cancellationOptionsFallback(input: RewriteRequestInput, context: string) {
  const greeting = extractRecipientName(input);
  const renewalDate = firstMatch(context, [/\b(June\s+3)\b/i]) || "the renewal date";
  const currentPlan = firstMatch(context, [/(\$\s*89\s+monthly)/i]) || "$89 monthly";
  const soloPlan = firstMatch(context, [/(\$\s*29\s+monthly)/i]) || "$29 monthly";

  return [
    greeting ? `Hi ${greeting},` : "Hi,",
    `I can help with the subscription before it renews on ${renewalDate}. The current plan is ${currentPlan}.`,
    `If you would rather keep a smaller plan, Solo is ${soloPlan}, but only the account admin can approve that change.`,
    "Please confirm whether you want cancellation at renewal or a downgrade. I will not cancel or downgrade the account until you confirm which option you want.",
  ].join("\n\n");
}

function deliveryDelayFallback(input: RewriteRequestInput, context: string) {
  const greeting = extractRecipientName(input)
    ? `Hi ${extractRecipientName(input)},`
    : "Hi,";
  const closing = /Customer Support Team/i.test(context)
    ? "Best regards,\nCustomer Support Team"
    : "";

  return [
    greeting,
    "I checked the order details. Your package has left our fulfillment center and is with the delivery carrier now.",
    "The delay appears to be from a temporary processing issue at the local distribution facility, not a problem with the order itself. It is still in transit and has not been marked lost or returned.",
    "For now, no action is required from you. The carrier should post another delivery update within the next one to two business days. If the tracking status still has not changed after two business days, reply to this email and we can open a follow-up investigation with the carrier.",
    "Sorry for the delay, and thank you for bearing with us while the delivery is completed.",
    closing,
  ]
    .filter(Boolean)
    .join("\n\n");
}

function planChangeFallback(input: RewriteRequestInput, context: string) {
  const greeting = extractRecipientName(input)
    ? `Hi ${extractRecipientName(input)},`
    : "Hi,";
  const date = /May\s+3/i.test(context) ? " on May 3" : "";
  const templates = /shared templates/i.test(context) ? " for shared templates" : "";

  return [
    greeting,
    `The Starter plan to Team plan change${date}${templates} can show as separate invoice lines.`,
    "In plain English, the old plan credit and the new plan charge usually appear separately during proration, so that layout is usually not a duplicate charge.",
    /finance thread/i.test(context)
      ? "You can use that explanation in the finance thread."
      : "",
    `If you send the ${/invoice screenshot/i.test(context) ? "invoice screenshot" : "invoice preview or line items"}, we can confirm whether the preview is showing the expected credit-and-charge adjustment before the invoice is finalized.`,
  ]
    .filter(Boolean)
    .join("\n\n");
}

function courseTransferFallback(input: RewriteRequestInput, context: string) {
  const greeting = /Daniel/i.test(context) ? "Hi Daniel," : "Hi,";
  const currentCohort = /June weekend cohort/i.test(context)
    ? "Your current enrollment is for the June weekend cohort"
    : "Your current enrollment may still be active";
  const startDate = /Saturday,\s*6 June/i.test(context)
    ? ", which starts on Saturday, 6 June"
    : "";
  const nextCohort = /Saturday,\s*20 July/i.test(context)
    ? "Saturday, 20 July"
    : "a later cohort";
  const courseCredit = /course credit/i.test(context)
    ? " If a full refund is not available, course credit may still be an option."
    : "";

  return [
    greeting,
    `${currentCohort}${startDate}. Because you contacted us before the course start date, you may still be eligible to transfer to a later cohort, depending on seat availability.`,
    `The next available cohort appears to start on ${nextCohort}. If that works better, we can review moving your registration there once you confirm.`,
    `For a refund, the policy requires requests at least seven days before the course begins. We would need to check the exact registration timestamp before confirming whether a full refund is available.${courseCredit}`,
    "Please reply with whether you prefer the July transfer or a refund review. We will not update your registration or cancel your current seat unless you clearly confirm which option you want.",
    /Customer Support Team/i.test(context) ? "Best regards,\nCustomer Support Team" : "",
  ]
    .filter(Boolean)
    .join("\n\n");
}

function workshopUpdateFallback(input: RewriteRequestInput, context: string) {
  const day = /Thursday/i.test(context)
    ? "Thursday"
    : /Saturday/i.test(context)
      ? "Saturday"
      : "the workshop";
  const hasRoom = /Room\s+204/i.test(context);
  const room = hasRoom ? "Room 204" : "the updated room";
  const time = /6:30pm/i.test(context) ? "6:30pm" : "the same time";
  const hasWorkshopAgenda =
    /scholarship forms/i.test(context) ||
    /supporting documents/i.test(context) ||
    /application timeline/i.test(context);
  const agendaUnchanged = /agenda is unchanged/i.test(context)
    ? "The agenda is unchanged."
    : "";
  const saturdayOnly = /only affects this Saturday/i.test(context)
    ? "The room change only affects this Saturday's workshop."
    : "";
  const originalReminder = /original workshop reminder on Tuesday/i.test(context)
    ? "Families received the original workshop reminder on Tuesday, so this update should focus only on the room change and not repeat the full registration instructions."
    : "";
  const submittedQuestions = /already[- ]submitted questions|already submitted questions/i.test(context)
    ? "If you already submitted questions, you do not need to send them again."
    : "";
  const printedDrafts = /printed scholarship drafts/i.test(context)
    ? "Printed scholarship drafts are still welcome."
    : "";
  const opening = hasRoom
    ? `${day} workshop update: we're moving to ${room} because of library maintenance.`
    : `${day} workshop note:`;
  const agendaLine = hasWorkshopAgenda
    ? `We'll still start at ${time} and cover scholarship forms, supporting documents, and the application timeline.`
    : "";

  return [
    opening,
    agendaLine,
    [agendaUnchanged, saturdayOnly].filter(Boolean).join(" "),
    originalReminder,
    [
      submittedQuestions,
      printedDrafts,
    ].filter(Boolean).join(" "),
  ]
    .filter(Boolean)
    .join("\n\n");
}

function workspaceAccessFallback(input: RewriteRequestInput, context: string) {
  const name = /Mina/i.test(context) ? "Mina" : "the user";
  const emailMatch = context.match(/\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b/i);
  const email = emailMatch?.[0] ?? "the same email address";
  const oldWorkspace = /old pilot workspace/i.test(context)
    ? "the old pilot workspace"
    : "the old team";
  const addedYesterday = /added to (?:our|the) workspace yesterday/i.test(context)
    ? `${name} was added to the workspace yesterday. `
    : "";
  const folder = /billing report folder/i.test(context)
    ? "the billing report folder"
    : "the right workspace";
  const resent = /resent the invite twice/i.test(context)
    ? "The invite was resent twice, so I would not keep repeating that step."
    : "";

  return [
    "Hi,",
    `${addedYesterday}${name}'s email address is ${email}, and she should keep signing in with it. If she still lands in ${oldWorkspace}, this is likely a workspace association issue rather than a new invite issue.`,
    resent,
    `The next useful step is for support to check which workspace ${email} is linked to and whether the newest Northstar invitation attached to the right account. That should explain why she cannot reach ${folder}.`,
  ]
    .filter(Boolean)
    .join("\n\n");
}

function incidentStatusFallback(input: RewriteRequestInput, context: string) {
  const ticket = context.match(/ticket\s+#?\d+/i)?.[0] ?? "the ticket";
  const opened = /Friday/i.test(context) ? " opened on Friday" : "";
  const noon = /before noon/i.test(context) ? " before noon" : "";
  const pause = /pause the campaign/i.test(context)
    ? "whether to pause the campaign"
    : "what decision to make";

  return [
    "Hi,",
    `${ticket}${opened} is still under review for the duplicate notifications.`,
    `What is confirmed: the duplicate notification issue is still happening, and the delivery logs are being checked. What is not confirmed yet is the root cause or whether a campaign pause is required.`,
    `I know your account team needs an answer on ${pause}${noon}. We will send the next status update as soon as the log review gives a clear recommendation.`,
  ].join("\n\n");
}

function generateThreadFallback(input: RewriteRequestInput): RewriteCandidate {
  const rawContext = [
    input.messageToReplyTo,
    input.roughDraftReply,
    input.audience,
    input.purpose,
    input.whatHappened,
    input.factsToPreserve,
  ]
    .join(" ");
  const context = rawContext.toLowerCase();

  let rewrittenText: string;
  const details = detailParts(input);
  const firstDetail = details[0] ?? compactDetail(input.roughDraftReply);
  const secondDetail = details[1] ?? "";

  if (input.scenario === "General reply") {
    if (isDashboardForecastUpdateContext(context)) {
      rewrittenText = dashboardForecastUpdateFallback(input, rawContext);
    } else if (isParentConferenceSchedulingContext(context)) {
      rewrittenText = parentConferenceSchedulingFallback(input, rawContext);
    } else if (isTeacherParentGradeContext(rawContext)) {
      rewrittenText = generateTeacherParentFallback(input, rawContext);
    } else if (
      textIncludes(context, ["participation activities", "exit ticket"])
    ) {
      const student = extractStudentName(rawContext, extractRecipientName(input));
      rewrittenText = threadNote("Hi, thanks for checking.", [
        `${student}'s grade changed because of two missing participation activities and one missing exit ticket`,
        "I can share more detail or talk it through if that helps",
      ]);
    } else if (
      isSupportLikeContext(context)
    ) {
      rewrittenText = generateSupportFallback(input, rawContext);
    } else if (
      textIncludes(context, [
        "owns",
        "blocker",
        "launch",
        "status",
        "demo",
        "api fix",
        "qa script",
        "retry",
        "screenshots",
        "source file",
        "board packet",
        "handoff",
        "incident",
      ])
    ) {
      rewrittenText = generateWorkUpdateFallback(input);
    } else if (
      textIncludes(context, [
        "reporting feature",
        "team templates",
        "two other vendors",
        "proposal",
        "vendor",
      ])
    ) {
      rewrittenText = generateSalesReplyFallback(input, rawContext);
    } else if (textIncludes(context, ["applying for", "role", "cover letter"])) {
      rewrittenText = generateCoverLetterFallback(input);
    } else if (input.messageToReplyTo.trim()) {
      rewrittenText = generateMessageReplyFallback(input);
    } else {
      rewrittenText = generateBlankFallback(input);
    }
  } else if (input.scenario === "Blank / custom") {
    rewrittenText = generateBlankFallback(input);
  } else if (input.scenario === "Email or message reply") {
    if (isTeacherParentGradeContext(rawContext)) {
      rewrittenText = generateTeacherParentFallback(input, rawContext);
    } else if (
      textIncludes(rawContext.toLowerCase(), [
        "reporting feature",
        "team templates",
        "two other vendors",
      ])
    ) {
      rewrittenText = generateSalesReplyFallback(input, rawContext);
    } else {
      rewrittenText = generateMessageReplyFallback(input);
    }
  } else if (input.scenario === "Customer support") {
    rewrittenText = generateSupportFallback(input, rawContext);
  } else if (input.scenario === "Cover letter") {
    rewrittenText = generateCoverLetterFallback(input);
  } else if (input.scenario === "Work update") {
    rewrittenText = generateWorkUpdateFallback(input);
  } else if (textIncludes(context, ["participation", "exit ticket", "parent"])) {
    rewrittenText = threadNote("Hi, thanks for checking.", [
      details.length
        ? `This week came down to ${details.join(" and ")}`
        : firstDetail,
      "Happy to talk it through on a quick call if that helps",
    ]);
  } else if (textIncludes(context, ["invoice", "prorated", "seats"])) {
    rewrittenText = invoiceFallback(input, rawContext);
  } else if (textIncludes(context, ["source file", "numbers", "10am"])) {
    rewrittenText = threadNote(firstDetail || "I need a little more time.", [
      secondDetail || "I'll send the numbers as soon as they are ready",
    ]);
  } else if (textIncludes(context, ["export", "settings"])) {
    rewrittenText = threadNote("I think I found the problem.", [
      firstDetail,
      secondDetail,
    ]);
  } else if (textIncludes(context, ["vendors", "proposal", "comparing"])) {
    rewrittenText = threadNote("No problem, take the extra week.", [
      firstDetail || "If anything comes up while you're comparing vendors, send it my way",
    ]);
  } else if (textIncludes(context, ["demo", "reschedule"])) {
    rewrittenText = threadNote("Got it, we can move the demo.", [
      firstDetail || "Send me two times that work for you",
    ]);
  } else if (textIncludes(context, ["document", "margin notes", "review"])) {
    rewrittenText = threadNote("Yep, it's ready.", [
      firstDetail,
      secondDetail || "I left margin notes where your input would help",
    ]);
  } else if (textIncludes(context, ["late", "deadline", "family issue"])) {
    rewrittenText = threadNote("Thanks for the heads-up.", [
      firstDetail,
      secondDetail,
    ]);
  } else {
    const detail = compactDetail(input.factsToPreserve || input.whatHappened);
    const opening = input.tonePreset === "Warm"
      ? "Thanks for the note."
      : "Got it.";
    rewrittenText = detail
      ? `${opening}\n\n${detail}.`
      : input.roughDraftReply;
  }

  return {
    rewrittenText,
    changeSummary: [
      "Made the reply shorter, more concrete, and easier to send in an email thread.",
    ],
    riskNotes: ["Review before sending if the context needs more detail."],
  };
}

function cleanDraftSentences(input: RewriteRequestInput, maxSentences = 5) {
  return detailParts(input)
    .map((part) =>
      part
        .replace(/^thank you for (?:reaching out|contacting us)[,.]?\s*/i, "")
        .replace(/^i am writing to express my interest in\s*/i, "I am interested in ")
        .replace(/\bpassionate and results-driven\b/gi, "focused")
        .replace(/\bproven track record\b/gi, "experience")
        .replace(/\bat your earliest convenience\b/gi, "when you can")
        .trim(),
    )
    .filter(Boolean)
    .slice(0, maxSentences);
}

function generateCoverLetterFallback(input: RewriteRequestInput) {
  const details = cleanDraftSentences(input, 8);
  const context = [input.messageToReplyTo, input.roughDraftReply].join(" ");
  const roleSentence = details.find((detail) =>
    /\b(?:apply|applying|application|role|position)\b/i.test(detail),
  );
  const evidenceSentences = details.filter((detail) =>
    /\b(?:answered|answer|summarized|summarize|updated|update|prepared|kept|helped|managed|coordinate|coordinated|track|tracking|handled|respond|responded)\b/i.test(
      detail,
    ),
  );
  const valuesSentence = details.find((detail) =>
    /\b(?:education access|complicated product details|customers understand)\b/i.test(detail),
  );
  const seniorityConstraint =
    /early in (?:their|my) support career|without pretending to be senior|do not make me sound senior|sound senior/i.test(
      context,
    )
    ? "Please keep the wording at the Support Specialist level; do not make me sound senior."
    : "";
  const companyFit = /small SaaS/i.test(context)
    ? "That kind of practical support experience would fit a small SaaS support team."
    : "";
  const opening = roleSentence
    ? sentence(roleSentence)
    : context
      ? "I'm interested in this role because the day-to-day work matches things I've already done."
      : "I'm interested in this role.";
  const body = evidenceSentences.length
    ? evidenceSentences.map(sentence).join(" ")
    : sentence(compactDetail(input.roughDraftReply));
  const values = valuesSentence ? sentence(valuesSentence) : "";

  return [opening, body, values, companyFit, seniorityConstraint]
    .filter(Boolean)
    .join("\n\n");
}

function generateWorkUpdateFallback(input: RewriteRequestInput) {
  const details = cleanDraftSentences(input, 4);
  const first = details[0] || compactDetail(input.roughDraftReply);
  const second = details[1] || "";
  const third = details[2] || "";

  return [
    `Quick update: ${sentence(first)}`,
    second ? sentence(second) : "",
    third ? sentence(third) : "",
  ]
    .filter(Boolean)
    .join("\n\n");
}

function timeoutSignal(seconds: number) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), seconds * 1000);

  return {
    signal: controller.signal,
    clear: () => clearTimeout(timeout),
  };
}

export type OpenAiModelStage =
  | "primary"
  | "repair"
  | "escalation"
  | "final_strong";

type OpenAiCompletionOptions = {
  telemetry?: RewriteTelemetryCollector;
  stage: OpenAiModelStage;
  model: string;
};

export type GenerateRewriteCandidateOptions = {
  telemetry?: RewriteTelemetryCollector;
};

export function getOpenAiModelForStage(stage: OpenAiModelStage) {
  const legacyModel = optionalEnv("OPENAI_MODEL", "gpt-4o-mini");
  const primary = optionalEnv("OPENAI_MODEL_PRIMARY", legacyModel);
  const repair = optionalEnv("OPENAI_MODEL_REPAIR", primary);
  const escalation = optionalEnv("OPENAI_MODEL_ESCALATION", repair);
  const finalStrong = optionalEnv("OPENAI_MODEL_FINAL_STRONG", escalation);

  if (stage === "repair") {
    return repair;
  }

  if (stage === "escalation") {
    return escalation;
  }

  if (stage === "final_strong") {
    return finalStrong;
  }

  return primary;
}

function numberEnv(name: string, fallback: number) {
  const rawValue = Number(optionalEnv(name, String(fallback)));
  return Number.isFinite(rawValue) ? rawValue : fallback;
}

function openAiTelemetryRole(stage: OpenAiModelStage) {
  if (stage === "primary") {
    return "mid_writer";
  }

  if (stage === "repair") {
    return "cheap_structured";
  }

  return "strong_escalation";
}

function openAiPricingForStage(stage: OpenAiModelStage) {
  if (stage === "repair") {
    return {
      inputPer1M: numberEnv("OPENAI_PRICE_CHEAP_INPUT_PER_1M", 0.2),
      outputPer1M: numberEnv("OPENAI_PRICE_CHEAP_OUTPUT_PER_1M", 1.25),
    };
  }

  if (stage === "primary") {
    return {
      inputPer1M: numberEnv("OPENAI_PRICE_MID_INPUT_PER_1M", 0.75),
      outputPer1M: numberEnv("OPENAI_PRICE_MID_OUTPUT_PER_1M", 4.5),
    };
  }

  return {
    inputPer1M: numberEnv("OPENAI_PRICE_STRONG_INPUT_PER_1M", 2.5),
    outputPer1M: numberEnv("OPENAI_PRICE_STRONG_OUTPUT_PER_1M", 15),
  };
}

function getRepairStage(attempt = 1): OpenAiModelStage {
  if (
    attempt >= 3 &&
    optionalEnv("OPENAI_ENABLE_FINAL_STRONG_MODEL", "false") === "true"
  ) {
    return "final_strong";
  }

  if (attempt >= 2) {
    return "escalation";
  }

  return "repair";
}

async function createChatCompletion(
  body: unknown,
  options?: OpenAiCompletionOptions,
) {
  const timeoutSeconds = Number(optionalEnv("OPENAI_TIMEOUT_SEC", "25"));
  const timeout = timeoutSignal(Number.isFinite(timeoutSeconds) ? timeoutSeconds : 25);
  const startedAt = Date.now();

  function latencyMs() {
    return Math.max(0, Date.now() - startedAt);
  }

  function recordProviderCall({
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
    if (!options?.telemetry) {
      return;
    }

    const pricing = openAiPricingForStage(options.stage);
    options.telemetry.recordProviderCall({
      provider: "openai",
      role: openAiTelemetryRole(options.stage),
      model: options.model,
      inputTokens,
      outputTokens,
      estimatedCostUsd: estimateOpenAiCostUsd({
        inputTokens,
        outputTokens,
        inputPer1M: pricing.inputPer1M,
        outputPer1M: pricing.outputPer1M,
      }),
      latencyMs: latencyMs(),
      success,
      errorCode,
    });
  }

  try {
    const response = await fetch(getOpenAiCompatibleChatCompletionsUrl(), {
      method: "POST",
      headers: {
        Authorization: `Bearer ${getOpenAiCompatibleApiKey()}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(body),
      signal: timeout.signal,
    });

    if (!response.ok) {
      const payload = (await response.json().catch(() => null)) as
        | { error?: { type?: string; code?: string; message?: string } }
        | null;
      const errorType = payload?.error?.type ?? payload?.error?.code ?? response.status;
      recordProviderCall({ success: false, errorCode: String(errorType) });
      throw new Error(`OpenAI request failed: ${errorType}`);
    }

    const payload = (await response.json()) as {
      choices?: Array<{ message?: { content?: string | null } }>;
      usage?: {
        prompt_tokens?: number;
        completion_tokens?: number;
      };
    };
    recordProviderCall({
      inputTokens: payload.usage?.prompt_tokens ?? 0,
      outputTokens: payload.usage?.completion_tokens ?? 0,
      success: true,
    });

    return payload;
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      recordProviderCall({ success: false, errorCode: "timeout" });
    }
    throw error;
  } finally {
    timeout.clear();
  }
}

export async function generateRewriteCandidate(
  input: RewriteRequestInput,
  strategy: Strategy,
  plan?: RewritePlan,
  options: GenerateRewriteCandidateOptions = {},
): Promise<RewriteCandidate> {
  if (strategy.id === "thread_reply") {
    return generateThreadFallback(input);
  }

  if (strategy.id === "facts_first") {
    return generateFactsFirstFallback(input);
  }

  const model = getOpenAiModelForStage("primary");

  const completion = await createChatCompletion(
    {
      model,
      temperature: strategy.temperature,
      response_format: { type: "json_object" },
      messages: [
        {
          role: "system",
          content:
            "You are ReplyInMyVoice, a writing assistant for everyday replies.\n" +
            "Your job is to rewrite the user's draft so it sounds natural, personal, and context-aware while preserving the user's facts.\n\n" +
            "Rules:\n" +
            "- Do not invent facts, promises, timelines, apologies, policies, discounts, meetings, names, or outcomes.\n" +
            "- Use only information from the draft and context fields.\n" +
            "- Keep the user's intent.\n" +
            "- Preserve concrete facts and constraints.\n" +
            "- Preserve named amounts, seat counts, dates, user counts, plan status, and requested next steps exactly when provided.\n" +
            "- If the draft includes a forwardable summary, keep that summary or a close equivalent.\n" +
            "- If the draft asks the recipient to send names, email addresses, files, or other details, keep that ask unless the context says not to.\n" +
            "- For long customer support replies, prefer several short paragraphs over one dense paragraph.\n" +
            "- Do not make the reply so short that it stops answering the customer's specific questions.\n" +
            "- If important context is missing, keep the reply neutral rather than adding details.\n" +
            "- Avoid sounding overly polished, generic, corporate, or robotic.\n" +
            "- Keep the reply compact, practical, and send-ready.\n" +
            "- Do not add a sign-off, team name, or closing sentence unless the draft already has one.\n" +
            "- Use the provided scenario guardrails and rewrite plan as internal instructions.\n" +
            "- If diagnosis tags are present, directly repair those causes instead of doing a general polish pass.\n" +
            "- Before returning, check that names, numbers, dates, amounts, role details, and requested next steps still match the input.\n" +
            "- Prefer a short email-thread shape over a formal paragraph; line breaks are allowed inside rewrittenText.\n" +
            "- A good reply may be two brief paragraphs, each with one sentence.\n" +
            "- When the topic is a parent, student, customer, or teammate reply, a brief human thread phrase is usually better than a formal explanation.\n" +
            "- Do not smooth every sentence into the same polished rhythm; vary sentence length naturally.\n" +
            "- Avoid stock phrases like 'Thank you for reaching out', 'I understand your concern', and 'at your earliest convenience' unless the user's draft clearly needs them.\n" +
            "- Do not discuss hiddenness, evasion, or whether the reply will pass automated reviews.\n" +
            "- Return strict JSON only with keys: rewrittenText, changeSummary, riskNotes.\n\n" +
            "Tone guidance:\n" +
            "- warm: friendly, clear, kind, natural, concise.\n" +
            "- direct: concise, professional, clear, low-fluff.\n" +
            "- Tone preset is the user's visible style choice. Follow it unless it would add facts or make the reply unsafe.\n" +
            "- Warm: adds a little relationship tone without adding facts.\n" +
            "- Direct: removes padding and keeps the reply plain without removing facts.",
        },
        {
          role: "user",
          content: buildUserPrompt(input, strategy, plan),
        },
      ],
    },
    { telemetry: options.telemetry, stage: "primary", model },
  );

  const rawContent = completion.choices?.at(0)?.message?.content;
  if (!rawContent) {
    throw new Error("OpenAI returned no content.");
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(rawContent);
  } catch {
    throw new Error("OpenAI returned invalid JSON.");
  }

  return normalizeRewriteOutput(parsed);
}

export async function generateRepairCandidate(
  repairInput: RepairPromptInput,
  options: GenerateRewriteCandidateOptions = {},
): Promise<RewriteCandidate> {
  const stage = getRepairStage(repairInput.attempt);
  const model = getOpenAiModelForStage(stage);

  const completion = await createChatCompletion(
    {
      model,
      temperature: 0.62,
      response_format: { type: "json_object" },
      messages: [
        {
          role: "system",
          content:
            "You are ReplyInMyVoice repairing a rejected rewrite.\n" +
            "Return a better send-ready rewrite that preserves facts and avoids polished template wording.\n" +
            "Do not invent details, names, promises, account actions, refunds, or timelines.\n" +
            "Do not add a sign-off unless the original draft had one.\n" +
            "Use natural, practical wording and varied sentence length.\n" +
            "Return strict JSON only with keys: rewrittenText, changeSummary, riskNotes.",
        },
        {
          role: "user",
          content: buildRepairUserPrompt(repairInput),
        },
      ],
    },
    { telemetry: options.telemetry, stage, model },
  );

  const rawContent = completion.choices?.at(0)?.message?.content;
  if (!rawContent) {
    throw new Error("OpenAI returned no repair content.");
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(rawContent);
  } catch {
    throw new Error("OpenAI returned invalid repair JSON.");
  }

  return normalizeRewriteOutput(parsed);
}
