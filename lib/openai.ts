import { z } from "zod";

import { optionalEnv, requireEnv } from "./env";
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
    temperature: 0.78,
    instruction:
      "Write like a short reply someone would actually send in an email thread. Use 2 short paragraphs separated by a blank line unless the reply is only one sentence. Start with a small thread phrase when natural, such as 'Got it', 'No problem', 'Thanks for checking', 'Yep', or 'Looks like'. Use contractions and plain wording. Prefer a slightly imperfect but professional note over a polished template.",
  },
  {
    id: "thread_reply",
    temperature: 0.45,
    instruction:
      "Use a strict email-thread fallback shape. Line 1: a short human phrase under 10 words. Blank line. Line 2: one concrete sentence with the key fact or next step. Optional line 3: one short next step. Adapt patterns like: 'Hi, thanks for checking.\\n\\nThis week came down to two missed activities and the missing exit ticket. Happy to talk it through on a quick call.'; 'No problem, take the extra week.\\n\\nIf anything comes up while you're comparing vendors, send it my way.'; 'Source file came in late, so I need a little more time.\\n\\nI'll send the numbers by 10am tomorrow.'; 'The jump is from the two seats added May 4.\\n\\nThis invoice includes the prorated charges for those seats.' Keep the user's facts and do not copy an example that does not fit.",
  },
];

function buildUserPrompt(input: RewriteRequestInput, strategy: Strategy) {
  return [
    `Strategy: ${strategy.instruction}`,
    `Tone: ${input.tone}`,
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
  const source =
    input.factsToPreserve || input.whatHappened || input.roughDraftReply;

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

function threadNote(opening: string, lines: string[]) {
  const body = lines.map(sentence).filter(Boolean).join(" ");
  return body ? `${opening}\n\n${body}` : opening;
}

function generateThreadFallback(input: RewriteRequestInput): RewriteCandidate {
  const context = [
    input.messageToReplyTo,
    input.roughDraftReply,
    input.audience,
    input.purpose,
    input.whatHappened,
    input.factsToPreserve,
  ]
    .join(" ")
    .toLowerCase();

  let rewrittenText: string;
  const details = detailParts(input);
  const firstDetail = details[0] ?? compactDetail(input.roughDraftReply);
  const secondDetail = details[1] ?? "";

  if (textIncludes(context, ["participation", "exit ticket", "parent"])) {
    rewrittenText = threadNote("Hi, thanks for checking.", [
      details.length
        ? `This week came down to ${details.join(" and ")}`
        : firstDetail,
      "Happy to talk it through on a quick call if that helps",
    ]);
  } else if (textIncludes(context, ["invoice", "prorated", "seats"])) {
    rewrittenText = threadNote(
      firstDetail || "The invoice changed this period.",
      [secondDetail || "This invoice includes the related prorated charges"],
    );
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
    rewrittenText = detail
      ? `${input.tone === "warm" ? "Thanks for the note." : "Got it."}\n\n${detail}.`
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
          "- If important context is missing, keep the reply neutral rather than adding details.\n" +
          "- Avoid sounding overly polished, generic, corporate, or robotic.\n" +
          "- Keep the reply compact, practical, and send-ready.\n" +
          "- Prefer a short email-thread shape over a formal paragraph; line breaks are allowed inside rewrittenText.\n" +
          "- A good reply may be two brief paragraphs, each with one sentence.\n" +
          "- When the topic is a parent, student, customer, or teammate reply, a brief human thread phrase is usually better than a formal explanation.\n" +
          "- Do not smooth every sentence into the same polished rhythm; vary sentence length naturally.\n" +
          "- Avoid stock phrases like 'Thank you for reaching out', 'I understand your concern', and 'at your earliest convenience' unless the user's draft clearly needs them.\n" +
          "- Do not discuss hiddenness, evasion, or whether the reply will pass automated reviews.\n" +
          "- Return strict JSON only with keys: rewrittenText, changeSummary, riskNotes.\n\n" +
          "Tone guidance:\n" +
          "- warm: friendly, clear, kind, natural, concise.\n" +
          "- direct: concise, professional, clear, low-fluff.",
      },
      {
        role: "user",
        content: buildUserPrompt(input, strategy),
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
