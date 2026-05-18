import { generateRewriteCandidate, getRewriteStrategies } from "./openai";
import { createRewritePlan, type DiagnosisTag } from "./rewrite-diagnosis";
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
    diagnosisTags: DiagnosisTag[];
    rewritePlanSummary: string;
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

function normalizeForFactCheck(value: string) {
  return value.toLowerCase().replace(/\s+/g, " ").trim();
}

function extractCriticalFacts(input: RewriteRequestInput) {
  const source = [input.messageToReplyTo, input.roughDraftReply, input.factsToPreserve]
    .join(" ")
    .replace(/\s+/g, " ");
  const matches = source.match(
    /\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b|\b(?:NZD\s*)?\$\s?\d+(?:\.\d{2})?|\b(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*(?:\s+\d{1,2})?\b|\b(?:before|after)\s+[a-z]+(?:\s+[a-z]+){0,2}\b|\bnext month\b|\b(?:resent|sent)\s+the\s+invite\s+(?:twice|again)\b|\b(?:[A-Za-z]+(?:\s+[A-Za-z]+){0,3})\s+(?:feature|column|packet|articles?|response|updates|folders?|ticket)\b|\b\d+\s+(?:active\s+seats?|regular\s+seats?|temporary\s+(?:users?|contractors?)|failed\s+events?|providers?|interviews?|teachers?)\b/gi,
  );

  return Array.from(new Set(matches ?? []));
}

function missingCriticalFacts(input: RewriteRequestInput, rewrittenText: string) {
  const normalized = normalizeForFactCheck(rewrittenText);
  return extractCriticalFacts(input).filter(
    (fact) => {
      const normalizedFact = normalizeForFactCheck(fact);
      const withoutArticle = normalizedFact.replace(/^(the|a|an)\s+/, "");
      return (
        !normalized.includes(normalizedFact) &&
        !normalized.includes(withoutArticle)
      );
    },
  );
}

function preservesCriticalFacts(input: RewriteRequestInput, rewrittenText: string) {
  return missingCriticalFacts(input, rewrittenText).length === 0;
}

function repairMissingCriticalFacts<
  T extends Awaited<ReturnType<typeof generateRewriteCandidate>>,
>(input: RewriteRequestInput, candidate: T): T {
  const missing = missingCriticalFacts(input, candidate.rewrittenText);
  if (!missing.length) {
    return candidate;
  }

  return {
    ...candidate,
    rewrittenText: `${candidate.rewrittenText.trim()}\n\nKey details to keep: ${missing.join("; ")}.`,
    changeSummary: [
      ...candidate.changeSummary,
      "Restored key details that were present in the original request.",
    ],
    riskNotes: [
      ...candidate.riskNotes,
      "Review the restored key details before sending.",
    ],
  };
}

export function isCandidateCompleteEnough(
  input: RewriteRequestInput,
  rewrittenText: string,
) {
  if (!preservesCriticalFacts(input, rewrittenText)) {
    return false;
  }

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
  const rewritePlan = createRewritePlan(input);
  const draftSignal = await measureWritingSignal(input.roughDraftReply);
  const strategies = getRewriteStrategies().slice(0, 2);
  let best: Awaited<ReturnType<typeof generateRewriteCandidate>> | null = null;
  let bestSignal: Awaited<ReturnType<typeof measureWritingSignal>> | null = null;
  let backup: Awaited<ReturnType<typeof generateRewriteCandidate>> | null = null;
  let backupSignal: Awaited<ReturnType<typeof measureWritingSignal>> | null = null;
  let tried = 0;

  for (const strategy of strategies) {
    tried += 1;
    const candidate = repairMissingCriticalFacts(
      input,
      await generateRewriteCandidate(input, strategy, rewritePlan),
    );
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

    if (
      completeEnough &&
      !shouldTryAnother(draftSignal.aiLikePercent, candidateSignal.aiLikePercent)
    ) {
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
      diagnosisTags: rewritePlan.tags,
      rewritePlanSummary: rewritePlan.summary,
    },
  };
}
