import { missingRequiredFacts } from "./fact-extraction";
import type { RewriteRequestInput } from "./validation";

export function isCandidateCompleteEnough(
  input: RewriteRequestInput,
  rewrittenText: string,
) {
  return missingRequiredFacts(input, rewrittenText).length === 0;
}
