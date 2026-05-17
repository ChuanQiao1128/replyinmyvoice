import { z } from "zod";

export const rewriteRequestSchema = z
  .object({
    messageToReplyTo: z.string().max(5000).optional().default(""),
    roughDraftReply: z.string().min(10).max(5000),
    audience: z.string().max(300).optional().default(""),
    purpose: z.string().max(500).optional().default(""),
    whatHappened: z.string().max(1000).optional().default(""),
    factsToPreserve: z.string().max(1000).optional().default(""),
    tone: z.enum(["warm", "direct"]),
  })
  .superRefine((value, ctx) => {
    const combinedLength =
      value.messageToReplyTo.length +
      value.roughDraftReply.length +
      value.audience.length +
      value.purpose.length +
      value.whatHappened.length +
      value.factsToPreserve.length;

    if (combinedLength > 10000) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Combined request length must be 10000 characters or less.",
        path: ["roughDraftReply"],
      });
    }
  });

export type RewriteRequestInput = z.infer<typeof rewriteRequestSchema>;
