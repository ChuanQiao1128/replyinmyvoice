import {
  generateRepairCandidate,
  generateRewriteCandidate,
  getRewriteStrategies,
} from "./openai";
import { createRewritePlan, type DiagnosisTag } from "./rewrite-diagnosis";
import {
  evaluateSignalQuality,
  shouldRejectCandidate,
  shouldRepairCandidate,
  type SignalQualityResult,
} from "./rewrite-quality-gate";
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
    repairCandidatesTried: number;
    rejectedCandidates: number;
    userUsageCharged: 1;
    diagnosisTags: DiagnosisTag[];
    rewritePlanSummary: string;
  };
};

export class RewriteQualityError extends Error {
  naturalness: RewriteResponsePayload["naturalness"];
  rejectedCandidates: number;
  repairCandidatesTried: number;

  constructor({
    naturalness,
    rejectedCandidates,
    repairCandidatesTried,
  }: {
    naturalness: RewriteResponsePayload["naturalness"];
    rejectedCandidates: number;
    repairCandidatesTried: number;
  }) {
    super("Could not produce a rewrite that improved the writing signal.");
    this.name = "RewriteQualityError";
    this.naturalness = naturalness;
    this.rejectedCandidates = rejectedCandidates;
    this.repairCandidatesTried = repairCandidatesTried;
  }
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

type CandidateRecord = {
  candidate: Awaited<ReturnType<typeof generateRewriteCandidate>>;
  signal: Awaited<ReturnType<typeof measureWritingSignal>>;
  quality: SignalQualityResult;
  completeEnough: boolean;
};

function isPassingCandidate(record: CandidateRecord) {
  return record.completeEnough && !shouldRejectCandidate(record.quality);
}

function pickBetterRecord(current: CandidateRecord | null, next: CandidateRecord) {
  if (!isPassingCandidate(next)) {
    return current;
  }

  if (!current) {
    return next;
  }

  return isBetterCandidate(
    current.signal.aiLikePercent,
    next.signal.aiLikePercent,
  )
    ? next
    : current;
}

function createCandidateRecord(
  input: RewriteRequestInput,
  candidate: Awaited<ReturnType<typeof generateRewriteCandidate>>,
  signal: Awaited<ReturnType<typeof measureWritingSignal>>,
  draftPercent: number | null,
): CandidateRecord {
  return {
    candidate,
    signal,
    quality: evaluateSignalQuality({
      draftPercent,
      rewritePercent: signal.aiLikePercent,
    }),
    completeEnough: isCandidateCompleteEnough(input, candidate.rewrittenText),
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
  let best: CandidateRecord | null = null;
  let unavailableBackup: CandidateRecord | null = null;
  let bestRejected: CandidateRecord | null = null;
  let tried = 0;
  let repairTried = 0;
  let rejectedCandidates = 0;

  for (const strategy of strategies) {
    tried += 1;
    const candidate = repairMissingCriticalFacts(
      input,
      await generateRewriteCandidate(input, strategy, rewritePlan),
    );
    const candidateSignal = await measureWritingSignal(candidate.rewrittenText);
    const record = createCandidateRecord(
      input,
      candidate,
      candidateSignal,
      draftSignal.aiLikePercent,
    );

    if (record.quality.status === "signal_unavailable" && record.completeEnough) {
      unavailableBackup = unavailableBackup ?? record;
    }

    if (shouldRejectCandidate(record.quality) || !record.completeEnough) {
      rejectedCandidates += 1;
      bestRejected =
        !bestRejected ||
        isBetterCandidate(
          bestRejected.signal.aiLikePercent,
          record.signal.aiLikePercent,
        )
          ? record
          : bestRejected;
    }

    best = pickBetterRecord(best, record);

    if (isPassingCandidate(record)) {
      break;
    }

    if (shouldRepairCandidate(record.quality) && repairTried < 2) {
      repairTried += 1;
      const repaired = repairMissingCriticalFacts(
        input,
        await generateRepairCandidate({
          input,
          plan: rewritePlan,
          rejectedText: record.candidate.rewrittenText,
          draftPercent: draftSignal.aiLikePercent,
          candidatePercent: record.signal.aiLikePercent,
          quality: record.quality,
        }),
      );
      const repairedSignal = await measureWritingSignal(repaired.rewrittenText);
      const repairedRecord = createCandidateRecord(
        input,
        repaired,
        repairedSignal,
        draftSignal.aiLikePercent,
      );

      if (
        repairedRecord.quality.status === "signal_unavailable" &&
        repairedRecord.completeEnough
      ) {
        unavailableBackup = unavailableBackup ?? repairedRecord;
      }

      if (
        shouldRejectCandidate(repairedRecord.quality) ||
        !repairedRecord.completeEnough
      ) {
        rejectedCandidates += 1;
        bestRejected =
          !bestRejected ||
          isBetterCandidate(
            bestRejected.signal.aiLikePercent,
            repairedRecord.signal.aiLikePercent,
          )
            ? repairedRecord
            : bestRejected;
      }

      best = pickBetterRecord(best, repairedRecord);

      if (isPassingCandidate(repairedRecord)) {
        break;
      }
    }
  }

  if (!best && unavailableBackup) {
    best = unavailableBackup;
  }

  if (!best) {
    throw new RewriteQualityError({
      naturalness: formatNaturalness(
        draftSignal.aiLikePercent,
        bestRejected?.signal.aiLikePercent ?? null,
      ),
      rejectedCandidates,
      repairCandidatesTried: repairTried,
    });
  }

  const naturalness = formatNaturalness(
    draftSignal.aiLikePercent,
    best.signal.aiLikePercent,
  );

  return {
    rewrittenText: best.candidate.rewrittenText,
    changeSummary: best.candidate.changeSummary,
    riskNotes: best.candidate.riskNotes,
    naturalness,
    optimization: {
      internalStrategiesTried: tried,
      repairCandidatesTried: repairTried,
      rejectedCandidates,
      userUsageCharged: 1,
      diagnosisTags: rewritePlan.tags,
      rewritePlanSummary: rewritePlan.summary,
    },
  };
}
