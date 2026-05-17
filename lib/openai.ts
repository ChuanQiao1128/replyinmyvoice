import OpenAI from "openai";
import { z } from "zod";

import { optionalEnv, requireEnv } from "./env";
import type { RewriteRequestInput } from "./validation";

const rewriteOutputSchema = z.object({
  rewrittenText: z.string().min(1),
  changeSummary: z.array(z.string()).min(1).max(6),
  riskNotes: z.array(z.string()).min(1).max(6),
});

export type RewriteCandidate = z.infer<typeof rewriteOutputSchema>;

type Strategy = {
  id: "grounded" | "texture";
  temperature: number;
  instruction: string;
};

const STRATEGIES: Strategy[] = [
  {
    id: "grounded",
    temperature: 0.45,
    instruction:
      "Keep the reply close to the user's facts. Remove generic phrasing, stiff openings, and overly polished transitions. Make it sound like a practical message someone would actually send.",
  },
  {
    id: "texture",
    temperature: 0.65,
    instruction:
      "Rewrite with more natural rhythm. Vary sentence length, keep useful imperfections, and add only context-specific phrasing supported by the provided fields.",
  },
];

let openAIClient: OpenAI | null = null;

function getOpenAIClient() {
  if (!openAIClient) {
    const timeoutSeconds = Number(optionalEnv("OPENAI_TIMEOUT_SEC", "25"));
    openAIClient = new OpenAI({
      apiKey: requireEnv("OPENAI_API_KEY"),
      timeout: (Number.isFinite(timeoutSeconds) ? timeoutSeconds : 25) * 1000,
    });
  }

  return openAIClient;
}

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

export async function generateRewriteCandidate(
  input: RewriteRequestInput,
  strategy: Strategy,
): Promise<RewriteCandidate> {
  const client = getOpenAIClient();
  const model = optionalEnv("OPENAI_MODEL", "gpt-4o-mini");

  const completion = await client.chat.completions.create({
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

  const rawContent = completion.choices.at(0)?.message.content;
  if (!rawContent) {
    throw new Error("OpenAI returned no content.");
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(rawContent);
  } catch {
    throw new Error("OpenAI returned invalid JSON.");
  }

  return rewriteOutputSchema.parse(parsed);
}
