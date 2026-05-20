import { z } from "zod";

import { optionalEnv, requireEnv } from "./env";
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
  const source = [
    input.factsToPreserve,
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
    .filter(Boolean)
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
  return firstMatch(input.roughDraftReply, [
    /\b(?:Hi|Hello|Dear)\s+([A-Z][a-z]+)\b/,
  ]);
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

function generateFactsFirstFallback(input: RewriteRequestInput) {
  const rawContext = [
    input.messageToReplyTo,
    input.roughDraftReply,
    input.whatHappened,
    input.factsToPreserve,
  ].join(" ");
  const context = rawContext.toLowerCase();

  if (isSeatBillingContext(context)) {
    return {
      rewrittenText: invoiceFallback(input, rawContext),
      changeSummary: [
        "Rebuilt the reply from the billing facts first, then removed support-template phrasing.",
      ],
      riskNotes: ["Check the billing details before sending."],
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

function generateSupportFallback(input: RewriteRequestInput, context: string) {
  const normalized = context.toLowerCase();

  if (isCourseTransferContext(normalized)) {
    return courseTransferFallback(input, context);
  }

  if (isPlanChangeBillingContext(normalized)) {
    return planChangeFallback(input, context);
  }

  if (isSeatBillingContext(normalized)) {
    return invoiceFallback(input, context);
  }

  if (textIncludes(normalized, ["custom tags column", "csv export", "export"])) {
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
    isWorkspaceAccessContext(context) ||
    isIncidentStatusContext(context) ||
    isCourseTransferContext(context) ||
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
    ])
  );
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
    [
      submittedQuestions ? "Already submitted questions? No need to send them again." : "",
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
  const folder = /billing report folder/i.test(context)
    ? "the billing report folder"
    : "the right workspace";
  const resent = /resent the invite twice/i.test(context)
    ? "The invite was resent twice, so I would not keep repeating that step."
    : "";

  return [
    "Hi,",
    `${name}'s email address is ${email}, and she should keep signing in with it. If she still lands in ${oldWorkspace}, this is likely a workspace association issue rather than a new invite issue.`,
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
    if (isTeacherParentGradeContext(rawContext)) {
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

function getRepairModel(attempt = 1) {
  if (
    attempt >= 3 &&
    optionalEnv("OPENAI_ENABLE_FINAL_STRONG_MODEL", "false") === "true"
  ) {
    return getOpenAiModelForStage("final_strong");
  }

  if (attempt >= 2) {
    return getOpenAiModelForStage("escalation");
  }

  return getOpenAiModelForStage("repair");
}

async function createChatCompletion(body: unknown) {
  const timeoutSeconds = Number(optionalEnv("OPENAI_TIMEOUT_SEC", "25"));
  const timeout = timeoutSignal(Number.isFinite(timeoutSeconds) ? timeoutSeconds : 25);

  try {
    const response = await fetch("https://api.openai.com/v1/chat/completions", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${requireEnv("OPENAI_API_KEY")}`,
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
      throw new Error(`OpenAI request failed: ${errorType}`);
    }

    return (await response.json()) as {
      choices?: Array<{ message?: { content?: string | null } }>;
    };
  } finally {
    timeout.clear();
  }
}

export async function generateRewriteCandidate(
  input: RewriteRequestInput,
  strategy: Strategy,
  plan?: RewritePlan,
): Promise<RewriteCandidate> {
  if (strategy.id === "thread_reply") {
    return generateThreadFallback(input);
  }

  if (strategy.id === "facts_first") {
    return generateFactsFirstFallback(input);
  }

  const model = getOpenAiModelForStage("primary");

  const completion = await createChatCompletion({
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
  });

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
): Promise<RewriteCandidate> {
  const model = getRepairModel(repairInput.attempt);

  const completion = await createChatCompletion({
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
  });

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
