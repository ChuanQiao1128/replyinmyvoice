import {
  detectUnsupportedFacts,
  missingRequiredFacts,
} from "./fact-extraction";
import type { RewriteRequestInput } from "./validation";

function missingCriticalFacts(input: RewriteRequestInput, rewrittenText: string) {
  return [
    ...missingRequiredFacts(input, rewrittenText).map(
      (fact) => `missing:${fact.normalizedText}`,
    ),
    ...detectUnsupportedFacts(input, rewrittenText).map(
      (fact) => `unsupported:${fact.normalizedText}`,
    ),
  ];
}

function preservesCriticalFacts(input: RewriteRequestInput, rewrittenText: string) {
  return missingCriticalFacts(input, rewrittenText).length === 0;
}

export function isCandidateCompleteEnough(
  input: RewriteRequestInput,
  rewrittenText: string,
) {
  if (!preservesCriticalFacts(input, rewrittenText)) {
    return false;
  }

  const draftLength = input.roughDraftReply.trim().length;
  const rewriteLength = rewrittenText.trim().length;

  if (input.scenario !== "Customer support") {
    if (draftLength >= 900) {
      return rewriteLength >= 120;
    }

    if (draftLength >= 450) {
      return rewriteLength >= 80;
    }

    return rewriteLength >= 20;
  }

  if (draftLength >= 900) {
    return rewriteLength >= Math.max(450, Math.floor(draftLength * 0.3));
  }

  if (draftLength >= 450) {
    return rewriteLength >= Math.max(220, Math.floor(draftLength * 0.3));
  }

  return rewriteLength >= 20;
}
