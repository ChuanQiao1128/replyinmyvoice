import type { DiagnosisTag } from "./rewrite-diagnosis";
import type { RequiredFact } from "./fact-extraction";
import type { ReviewedFact } from "./rewrite-pipeline/fact-ledger";
import type {
  ExtractedFacts,
  RewriteAttemptLedgerEntry,
} from "./rewrite-pipeline/types";
import type { SignalQualityResult } from "./rewrite-quality-gate";
import type { SignalLabel } from "./writing-signal";

export type RewriteFactDiagnostics = {
  extractedFacts: ExtractedFacts;
  reviewedFacts: ExtractedFacts;
  addedAnchors: RequiredFact[];
  rejectedFacts: ReviewedFact[];
};

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
    factDiagnostics?: RewriteFactDiagnostics;
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
