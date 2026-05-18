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
    candidateSignals: Array<{
      stage: "initial" | "repair";
      aiLikePercent: number | null;
      status: SignalQualityResult["status"];
      rejected: boolean;
      reason: string;
    }>;
  };
};

export class RewriteQualityError extends Error {
  naturalness: RewriteResponsePayload["naturalness"];
  rejectedCandidates: number;
  repairCandidatesTried: number;
  candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"];

  constructor({
    naturalness,
    rejectedCandidates,
    repairCandidatesTried,
    candidateSignals,
  }: {
    naturalness: RewriteResponsePayload["naturalness"];
    rejectedCandidates: number;
    repairCandidatesTried: number;
    candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"];
  }) {
    super("Could not produce a rewrite that improved the writing signal.");
    this.name = "RewriteQualityError";
    this.naturalness = naturalness;
    this.rejectedCandidates = rejectedCandidates;
    this.repairCandidatesTried = repairCandidatesTried;
    this.candidateSignals = candidateSignals;
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
    /\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b|\b(?:NZD\s*)?\$\s?\d+(?:\.\d{2})?|\b(?:January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)\s+\d{1,2}\b|\bbefore class tomorrow\b|\bbefore noon\b|\bafter\s+May\s+\d{1,2}\b|\bnext month\b|\bfinance manager\b|\bbase plan\b|\b(?:old|new)\s+plan\s+(?:credit|charge)\b|\b(?:resent|sent)\s+the\s+invite\s+(?:twice|again)\b|\bcustom tags column\b|\bbilling report folder\b|\breporting feature\b|\bteam templates\b|\bhelp center articles?\b|\bmonthly partner updates\b|\bpatient follow-up notes\b|\bpartner onboarding packet\b|\bMonday board packet\b|\bapplication timeline\b|\bscholarship forms\b|\bpricing table\b|\bsection three\b|\bsection five\b|\bpayment flow\b|\bonboarding checklist\b|\blast three failed events\b|\b\d+\s*(?:am|pm)\b|\b\d+\s+(?:active\s+seats?|regular\s+seats?|temporary\s+(?:users?|contractors?)|failed\s+events?|providers?|interviews?|teachers?)\b/gi,
  );
  const exactMonthMatches = source.match(
    /\b(?:January|February|March|April|May|June|July|August|September|October|November|December)\b/g,
  );

  return Array.from(new Set([...(matches ?? []), ...(exactMonthMatches ?? [])]));
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
  const candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"] = [];

  for (const strategy of strategies) {
    tried += 1;
    const candidate = await generateRewriteCandidate(input, strategy, rewritePlan);
    const candidateSignal = await measureWritingSignal(candidate.rewrittenText);
    const record = createCandidateRecord(
      input,
      candidate,
      candidateSignal,
      draftSignal.aiLikePercent,
    );
    candidateSignals.push({
      stage: "initial",
      aiLikePercent: record.signal.aiLikePercent,
      status: record.quality.status,
      rejected: shouldRejectCandidate(record.quality) || !record.completeEnough,
      reason: record.completeEnough
        ? record.quality.reason
        : "Rewrite dropped required details or became too short.",
    });

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

    if (
      (shouldRepairCandidate(record.quality) || !record.completeEnough) &&
      repairTried < 2
    ) {
      repairTried += 1;
      const repaired = await generateRepairCandidate({
        input,
        plan: rewritePlan,
        rejectedText: record.candidate.rewrittenText,
        draftPercent: draftSignal.aiLikePercent,
        candidatePercent: record.signal.aiLikePercent,
        quality: record.quality,
      });
      const repairedSignal = await measureWritingSignal(repaired.rewrittenText);
      const repairedRecord = createCandidateRecord(
        input,
        repaired,
        repairedSignal,
        draftSignal.aiLikePercent,
      );
      candidateSignals.push({
        stage: "repair",
        aiLikePercent: repairedRecord.signal.aiLikePercent,
        status: repairedRecord.quality.status,
        rejected:
          shouldRejectCandidate(repairedRecord.quality) ||
          !repairedRecord.completeEnough,
        reason: repairedRecord.completeEnough
          ? repairedRecord.quality.reason
          : "Repair dropped required details or became too short.",
      });

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
      candidateSignals,
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
      candidateSignals,
    },
  };
}
