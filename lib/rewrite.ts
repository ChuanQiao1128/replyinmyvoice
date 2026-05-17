import { generateRewriteCandidate, getRewriteStrategies } from "./openai";
import type { RewriteRequestInput } from "./validation";
import {
  formatNaturalness,
  measureWritingSignal,
  type SignalLabel,
} from "./writing-signal";

export type RewriteResponsePayload = {
  rewrittenText: string;
  changeSummary: string[];
  riskNotes: string[];
  naturalness: {
    draftAiLikePercent: number | null;
    rewriteAiLikePercent: number | null;
    changePoints: number | null;
    label: SignalLabel;
  };
  optimization: {
    internalStrategiesTried: number;
    userUsageCharged: 1;
  };
};

function shouldTryAnother(
  draftPercent: number | null,
  rewritePercent: number | null,
) {
  if (draftPercent === null || rewritePercent === null) {
    return false;
  }

  return rewritePercent > 50 || rewritePercent > draftPercent - 30;
}

function isBetterCandidate(
  currentPercent: number | null,
  nextPercent: number | null,
) {
  if (currentPercent === null) {
    return nextPercent !== null;
  }

  if (nextPercent === null) {
    return false;
  }

  return nextPercent < currentPercent;
}

export function isCandidateCompleteEnough(
  input: RewriteRequestInput,
  rewrittenText: string,
) {
  const draftLength = input.roughDraftReply.trim().length;

  if (draftLength >= 900) {
    return rewrittenText.trim().length >= Math.max(450, Math.floor(draftLength * 0.3));
  }

  if (draftLength >= 450) {
    return rewrittenText.trim().length >= Math.max(220, Math.floor(draftLength * 0.3));
  }

  return rewrittenText.trim().length >= 20;
}

export async function rewriteWithOptimization(
  input: RewriteRequestInput,
): Promise<RewriteResponsePayload> {
  const draftSignal = await measureWritingSignal(input.roughDraftReply);
  const strategies = getRewriteStrategies().slice(0, 2);
  let best: Awaited<ReturnType<typeof generateRewriteCandidate>> | null = null;
  let bestSignal: Awaited<ReturnType<typeof measureWritingSignal>> | null = null;
  let backup: Awaited<ReturnType<typeof generateRewriteCandidate>> | null = null;
  let backupSignal: Awaited<ReturnType<typeof measureWritingSignal>> | null = null;
  let tried = 0;

  for (const strategy of strategies) {
    tried += 1;
    const candidate = await generateRewriteCandidate(input, strategy);
    const candidateSignal = await measureWritingSignal(candidate.rewrittenText);
    const completeEnough = isCandidateCompleteEnough(input, candidate.rewrittenText);

    if (
      !backup ||
      isBetterCandidate(backupSignal?.aiLikePercent ?? null, candidateSignal.aiLikePercent)
    ) {
      backup = candidate;
      backupSignal = candidateSignal;
    }

    if (
      completeEnough &&
      (!best ||
      isBetterCandidate(bestSignal?.aiLikePercent ?? null, candidateSignal.aiLikePercent)
      )
    ) {
      best = candidate;
      bestSignal = candidateSignal;
    }

    if (!shouldTryAnother(draftSignal.aiLikePercent, candidateSignal.aiLikePercent)) {
      break;
    }
  }

  if (!best) {
    best = backup;
    bestSignal = backupSignal;
  }

  if (!best) {
    throw new Error("No rewrite candidate was produced.");
  }

  const naturalness = formatNaturalness(
    draftSignal.aiLikePercent,
    bestSignal?.aiLikePercent ?? null,
  );

  return {
    rewrittenText: best.rewrittenText,
    changeSummary: best.changeSummary,
    riskNotes: best.riskNotes,
    naturalness,
    optimization: {
      internalStrategiesTried: tried,
      userUsageCharged: 1,
    },
  };
}
