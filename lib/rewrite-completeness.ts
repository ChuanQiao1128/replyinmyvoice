import type { RewriteRequestInput } from "./validation";

export function isCandidateCompleteEnough(
  input: RewriteRequestInput,
  rewrittenText: string,
) {
  const sourceText = [
    input.messageToReplyTo,
    input.roughDraftReply,
    input.audience,
    input.purpose,
    input.whatHappened,
    input.factsToPreserve,
  ]
    .filter(Boolean)
    .join(" ")
    .trim();

  return sourceText.length > 0 && rewrittenText.trim().length > 0;
}
