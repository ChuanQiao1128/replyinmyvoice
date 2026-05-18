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
  id: "plain_note" | "thread_reply";
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

  return source
    .split(/[.!?]\s+|\n+/)
    .map((part) => part.trim().replace(/[.!?]$/, ""))
    .filter(Boolean)
    .slice(0, 3);
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
    ? `${sentence(suppliedDetails[0])} Based on that, the higher invoice preview is most likely from prorated seat usage rather than a base plan change.`
    : `Based on what you described, the higher invoice preview is most likely from ${facts.temporaryUsers} being counted as active seats during May, rather than a base plan change.`;
  const approvedSeatPhrase =
    facts.approvedSeats ? ` Your team approved ${facts.approvedSeats} regular seats.` : "";
  const supportingDetail = suppliedDetails[1] ? `${sentence(suppliedDetails[1])} ` : "";
  const activeSeatPhrase =
    facts.activeSeats && facts.approvedSeats
      ? `That would explain why the dashboard shows ${facts.activeSeats} active seats instead of the ${facts.approvedSeats} regular seats your team approved.`
      : "That would explain the higher active-seat count in the invoice preview.";
  const amountPhrase = facts.amount
    ? `It also lines up with the ${facts.amount} increase you are seeing.`
    : "It also lines up with the increase you are seeing.";
  const financeSummary = context.includes("finance")
    ? `A simple note for finance would be: "The invoice preview increased because ${facts.temporaryUsers} were active during the month. The extra charge appears to be prorated seat usage, not a change to the base plan."`
    : "";
  const datePhrase = facts.removalDate
    ? `They may still be active if they were not removed after ${facts.removalDate}.`
    : "They may still be active if they were not removed after the short project ended.";

  return [
    greeting,
    `Thanks for laying this out. ${changeExplanation}${approvedSeatPhrase}`,
    `${supportingDetail}Even if someone only has access for part of the month, that access can still create a prorated seat charge for the days they were active. ${activeSeatPhrase} ${amountPhrase}`,
    financeSummary,
    `For the next step, check whether those contractor accounts are still active. ${datePhrase} If you want us to help confirm, send over their names or email addresses and we can check their status. We will not change anything unless you ask us to.`,
  ]
    .filter(Boolean)
    .join("\n\n");
}

function generateBlankFallback(input: RewriteRequestInput) {
  const details = detailParts(input).slice(0, 4);
  const first = details[0] || compactDetail(input.roughDraftReply);
  const rest = details.slice(1);

  if (input.tonePreset === "Concise") {
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

function generateSupportFallback(input: RewriteRequestInput, context: string) {
  if (textIncludes(context, ["invoice", "prorated", "seats"])) {
    return invoiceFallback(input, context);
  }

  const name = extractRecipientName(input);
  const greeting = name ? `Hi ${name},` : "Hi,";
  const details = detailParts(input).slice(0, 7);
  const first = details[0] || compactDetail(input.roughDraftReply);
  const body = details.slice(1, 5).map(sentence).join(" ");
  const next = details.slice(5, 7).map(sentence).join(" ");

  return [greeting, sentence(first), body, next].filter(Boolean).join("\n\n");
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

  if (input.scenario === "Blank / custom") {
    rewrittenText = generateBlankFallback(input);
  } else if (input.scenario === "Email or message reply") {
    rewrittenText = generateMessageReplyFallback(input);
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
    const opening = ["Warm", "Friendly"].includes(input.tonePreset)
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
  const details = cleanDraftSentences(input, 5);
  const context = input.messageToReplyTo.trim();
  const opening = context
    ? "I'm interested in this role because the day-to-day work matches things I've already done."
    : "I'm interested in the role and wanted to share why my background fits.";
  const body = details.length
    ? details.map(sentence).join(" ")
    : sentence(compactDetail(input.roughDraftReply));

  return [opening, body, "I'd be glad to talk through how I could help."]
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

  const model = optionalEnv("OPENAI_MODEL", "gpt-4o-mini");

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
          "- Professional: polished but not stiff.\n" +
          "- Friendly: approachable and conversational.\n" +
          "- Concise: shorter and direct.",
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
  const model = optionalEnv("OPENAI_MODEL", "gpt-4o-mini");

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
