import { z } from "zod";

import { rewriteInputLimits } from "./rewrite-limits";
import { scenarioOptions, tonePresetOptions } from "./rewrite-presets";

export const rewriteRequestSchema = z
  .object({
    scenario: z.enum(scenarioOptions).optional().default("General reply"),
    messageToReplyTo: z
      .string()
      .max(rewriteInputLimits.messageToReplyTo)
      .optional()
      .default(""),
    roughDraftReply: z
      .string()
      .min(10)
      .max(rewriteInputLimits.roughDraftReply),
    audience: z.string().max(rewriteInputLimits.audience).optional().default(""),
    purpose: z.string().max(rewriteInputLimits.purpose).optional().default(""),
    whatHappened: z
      .string()
      .max(rewriteInputLimits.whatHappened)
      .optional()
      .default(""),
    factsToPreserve: z
      .string()
      .max(rewriteInputLimits.factsToPreserve)
      .optional()
      .default(""),
    tone: z.enum(["warm", "direct"]),
    tonePreset: z.enum(tonePresetOptions).optional().default("Warm"),
  })
  .superRefine((value, ctx) => {
    const combinedLength =
      value.messageToReplyTo.length +
      value.roughDraftReply.length +
      value.audience.length +
      value.purpose.length +
      value.whatHappened.length +
      value.factsToPreserve.length;

    if (combinedLength > rewriteInputLimits.combined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: `Combined request length must be ${rewriteInputLimits.combined} characters or less.`,
        path: ["roughDraftReply"],
      });
    }
  });

export type RewriteRequestInput = z.infer<typeof rewriteRequestSchema>;
