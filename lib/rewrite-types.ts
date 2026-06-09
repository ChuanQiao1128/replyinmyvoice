import type { SignalLabel } from "./writing-signal";

export type ExtractedFacts = {
  facts: string[];
  criticalFacts: string[];
  supportingFacts: string[];
  optionalFacts: string[];
  constraints: string[];
  sourceAnchors: string[];
};

export type RequiredFact = {
  text: string;
  normalizedText: string;
  category: string;
  source: string;
};

export type ReviewedFact = {
  text: string;
  normalizedText: string;
  accepted: boolean;
  reason: string;
};

export type RewriteAttemptLedgerEntry = {
  attempt: number;
  stage: string;
  strategy: string;
  outcome: string;
  reason: string;
};

type SignalQualityStatus =
  | "passed"
  | "signal_unavailable"
  | "signal_not_improved"
  | "still_above_threshold"
  | "failed";

type DiagnosisTag = string;

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
      status: SignalQualityStatus;
      rejected: boolean;
      reason: string;
    }>;
  };
};
