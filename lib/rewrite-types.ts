import type { DiagnosisTag } from "./rewrite-diagnosis";
import type { RewriteAttemptLedgerEntry } from "./rewrite-pipeline/types";
import type { SignalQualityResult } from "./rewrite-quality-gate";
import type { SignalLabel } from "./writing-signal";

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
    attemptLedger?: RewriteAttemptLedgerEntry[];
    candidateSignals: Array<{
      stage: "initial" | "targeted_repair" | "repair" | "fallback";
      aiLikePercent: number | null;
      status: SignalQualityResult["status"];
      rejected: boolean;
      reason: string;
    }>;
  };
};
