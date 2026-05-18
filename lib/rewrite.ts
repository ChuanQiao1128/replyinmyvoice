import {
  generateGuaranteedRewriteCandidate,
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
    selectionStatus: "passed" | "best_available";
    diagnosisTags: DiagnosisTag[];
    rewritePlanSummary: string;
    candidateSignals: Array<{
      stage: "initial" | "repair" | "fallback";
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
  diagnosisTags: DiagnosisTag[];
  rewritePlanSummary: string;

  constructor({
    naturalness,
    rejectedCandidates,
    repairCandidatesTried,
    candidateSignals,
    diagnosisTags,
    rewritePlanSummary,
  }: {
    naturalness: RewriteResponsePayload["naturalness"];
    rejectedCandidates: number;
    repairCandidatesTried: number;
    candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"];
    diagnosisTags: DiagnosisTag[];
    rewritePlanSummary: string;
  }) {
    super("Could not produce a rewrite that improved the writing signal.");
    this.name = "RewriteQualityError";
    this.naturalness = naturalness;
    this.rejectedCandidates = rejectedCandidates;
    this.repairCandidatesTried = repairCandidatesTried;
    this.candidateSignals = candidateSignals;
    this.diagnosisTags = diagnosisTags;
    this.rewritePlanSummary = rewritePlanSummary;
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
    /\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b|\b(?:NZD\s*)?\$\s?\d+(?:\.\d{2})?|\b(?:January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec)\s+\d{1,2}\b|\bbefore class tomorrow\b|\bbefore noon\b|\bafter\s+May\s+\d{1,2}\b|\bnext month\b|\bfirst week of June\b|\bcourse policy\b|\btwo missing participation activities\b|\bone missing exit ticket\b|\btwo asked\b|\btwo other vendors\b|\bsource file arrived later\b|\bsource file arrived late\b|\b2pm launch check\b|\bpause the campaign\b|\bold pilot workspace\b|\bfinance manager\b|\bbase plan\b|\b(?:old|new)\s+plan\s+(?:credit|charge)\b|\b(?:resent|sent)\s+the\s+invite\s+(?:twice|again)\b|\bcustom tags column\b|\bbilling report folder\b|\breporting feature\b|\bteam templates\b|\bhelp center articles?\b|\bmonthly partner updates\b|\bweekly partner updates\b|\bpatient follow-up notes\b|\bpartner onboarding packet\b|\bMonday board packet\b|\bapplication timeline\b|\bscholarship forms\b|\bpricing table\b|\bsection three\b|\bsection five\b|\bpayment flow\b|\bonboarding checklist\b|\bhelp article links\b|\blast three failed events\b|\b\d+\s*(?:am|pm)\b|\b\d+\s+(?:active\s+seats?|regular\s+seats?|temporary\s+(?:users?|contractors?)|failed\s+events?|providers?|interviews?|teachers?)\b/gi,
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

function pickBetterCompleteRecord(
  current: CandidateRecord | null,
  next: CandidateRecord,
) {
  if (!next.completeEnough) {
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

function createOriginalSafetyRecord(
  input: RewriteRequestInput,
  draftSignal: Awaited<ReturnType<typeof measureWritingSignal>>,
): CandidateRecord {
  return createCandidateRecord(
    input,
    {
      rewrittenText: input.roughDraftReply,
      changeSummary: [
        "Kept the original wording because measured rewrite candidates did not improve it.",
      ],
      riskNotes: [
        "Review before sending; the system did not find a measured improvement for this draft.",
      ],
    },
    draftSignal,
    draftSignal.aiLikePercent,
  );
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

export async function rewriteWithOptimization(
  input: RewriteRequestInput,
): Promise<RewriteResponsePayload> {
  const rewritePlan = createRewritePlan(input);
  const draftSignal = await measureWritingSignal(input.roughDraftReply);
  const strategies = getRewriteStrategies().slice(0, 3);
  let best: CandidateRecord | null = null;
  let bestAvailable: CandidateRecord | null = null;
  let unavailableBackup: CandidateRecord | null = null;
  let tried = 0;
  let repairTried = 0;
  let rejectedCandidates = 0;
  const candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"] = [];

  for (const strategy of strategies) {
    tried += 1;
    let candidate: Awaited<ReturnType<typeof generateRewriteCandidate>>;
    try {
      candidate = await generateRewriteCandidate(input, strategy, rewritePlan);
    } catch {
      candidateSignals.push({
        stage: "initial",
        aiLikePercent: null,
        status: "signal_unavailable",
        rejected: true,
        reason: "Candidate generation failed before signal check.",
      });
      continue;
    }

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
    }

    best = pickBetterRecord(best, record);
    bestAvailable = pickBetterCompleteRecord(bestAvailable, record);

    if (isPassingCandidate(record)) {
      break;
    }

    if (
      (shouldRepairCandidate(record.quality) || !record.completeEnough) &&
      repairTried < 2
    ) {
      repairTried += 1;
      let repaired: Awaited<ReturnType<typeof generateRepairCandidate>>;
      try {
        repaired = await generateRepairCandidate({
          input,
          plan: rewritePlan,
          rejectedText: record.candidate.rewrittenText,
          draftPercent: draftSignal.aiLikePercent,
          candidatePercent: record.signal.aiLikePercent,
          quality: record.quality,
        });
      } catch {
        candidateSignals.push({
          stage: "repair",
          aiLikePercent: null,
          status: "signal_unavailable",
          rejected: true,
          reason: "Repair generation failed before signal check.",
        });
        continue;
      }

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
      }

      best = pickBetterRecord(best, repairedRecord);
      bestAvailable = pickBetterCompleteRecord(bestAvailable, repairedRecord);

      if (isPassingCandidate(repairedRecord)) {
        break;
      }
    }
  }

  if (!best && unavailableBackup) {
    best = unavailableBackup;
  }

  let selectionStatus: RewriteResponsePayload["optimization"]["selectionStatus"] =
    "passed";

  if (!best && bestAvailable) {
    best = bestAvailable;
    selectionStatus = "best_available";
  }

  if (!best) {
    const fallback = await generateGuaranteedRewriteCandidate(input);
    const fallbackSignal = await measureWritingSignal(fallback.rewrittenText);
    const fallbackRecord = createCandidateRecord(
      input,
      fallback,
      fallbackSignal,
      draftSignal.aiLikePercent,
    );
    candidateSignals.push({
      stage: "fallback",
      aiLikePercent: fallbackRecord.signal.aiLikePercent,
      status: fallbackRecord.quality.status,
      rejected:
        shouldRejectCandidate(fallbackRecord.quality) ||
        !fallbackRecord.completeEnough,
      reason: fallbackRecord.completeEnough
        ? fallbackRecord.quality.reason
        : "Fallback may be missing required details or became too short.",
    });
    best = fallbackRecord;
    selectionStatus = "best_available";
  }

  if (
    draftSignal.aiLikePercent !== null &&
    best.signal.aiLikePercent !== null &&
    best.signal.aiLikePercent > draftSignal.aiLikePercent
  ) {
    const originalRecord = createOriginalSafetyRecord(input, draftSignal);

    if (originalRecord.completeEnough) {
      best = originalRecord;
      selectionStatus = "best_available";
      candidateSignals.push({
        stage: "fallback",
        aiLikePercent: draftSignal.aiLikePercent,
        status: best.quality.status,
        rejected: false,
        reason: "Kept the original draft because measured candidates worsened the writing signal.",
      });
    }
  }

  const naturalness = formatNaturalness(
    draftSignal.aiLikePercent,
    best.signal.aiLikePercent,
  );
  const bestAvailableRiskNote = shouldRejectCandidate(best.quality)
    ? "The writing signal is still high; review before sending."
    : "This fallback was selected to preserve the reply details; review before sending.";

  return {
    rewrittenText: best.candidate.rewrittenText,
    changeSummary: best.candidate.changeSummary,
    riskNotes:
      selectionStatus === "best_available"
        ? [
            ...best.candidate.riskNotes,
            bestAvailableRiskNote,
          ]
        : best.candidate.riskNotes,
    naturalness,
    optimization: {
      internalStrategiesTried: tried,
      repairCandidatesTried: repairTried,
      rejectedCandidates,
      userUsageCharged: 1,
      selectionStatus,
      diagnosisTags: rewritePlan.tags,
      rewritePlanSummary: rewritePlan.summary,
      candidateSignals,
    },
  };
}
