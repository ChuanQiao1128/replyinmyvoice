export type LearningPromotionDecision =
  | "digest_only"
  | "docs_only"
  | "promoted_candidate"
  | "blocked";

export type LearningPromotionRecommendation =
  | "no-op"
  | "docs-only"
  | "test-needed"
  | "code-change";

export type LearningFailureType =
  | "worse_than_draft"
  | "high_final_signal"
  | "quality_gate_failure"
  | "repair_success";

export type LearningSeverity = "low" | "medium" | "high";

export type LearningSampleRow = {
  id?: string;
  scenario: string;
  tonePreset: string;
  status: string;
  draftAiLikePercent: number | null;
  rewriteAiLikePercent: number | null;
  changePoints: number | null;
  diagnosisTags: string;
  repairCandidates: number;
  rejectedCandidates: number;
  createdAt: string;
};

export type LearningFindingDraft = {
  scenario: string | null;
  failureType: LearningFailureType;
  diagnosisTags: string[];
  evidenceCount: number;
  severity: LearningSeverity;
  recommendation: string;
  promotionRecommendation: LearningPromotionRecommendation;
  sampleRefs: string[];
};

export type StrategyCandidateDraft = {
  findingIndex: number;
  title: string;
  scenario: string | null;
  proposedChangeSummary: string;
  riskLevel: LearningSeverity;
  status: "proposed";
  requiredEval: string;
  evidenceCount: number;
};

export type LearningAnalysisSummary = {
  sampleCount: number;
  measuredCount: number;
  successCount: number;
  qualityFailureCount: number;
  averageDropPoints: number | null;
  below50Count: number;
  worseThanDraftCount: number;
};

export type LearningAnalysisResult = {
  summary: LearningAnalysisSummary;
  findings: LearningFindingDraft[];
  strategyCandidates: StrategyCandidateDraft[];
  promotionDecision: LearningPromotionDecision;
};

const MIN_REPEATED_FAILURES_FOR_PROMOTION = 3;
const MIN_REPAIR_SUCCESSES_FOR_MEMORY = 2;

function parseDiagnosisTags(value: string) {
  try {
    const parsed = JSON.parse(value);
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed.filter((tag): tag is string => typeof tag === "string");
  } catch {
    return [];
  }
}

function average(values: number[]) {
  if (values.length === 0) {
    return null;
  }

  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

function uniqueTags(rows: LearningSampleRow[]) {
  return Array.from(
    new Set(rows.flatMap((row) => parseDiagnosisTags(row.diagnosisTags))),
  ).sort();
}

function sampleRefs(rows: LearningSampleRow[]) {
  return rows
    .map((row, index) => row.id ?? `${row.scenario}:${row.createdAt}:${index}`)
    .slice(0, 12);
}

function byScenario(rows: LearningSampleRow[]) {
  const grouped = new Map<string, LearningSampleRow[]>();
  for (const row of rows) {
    grouped.set(row.scenario, [...(grouped.get(row.scenario) ?? []), row]);
  }
  return grouped;
}

function isMeasured(row: LearningSampleRow) {
  return row.draftAiLikePercent !== null && row.rewriteAiLikePercent !== null;
}

function signalDrop(row: LearningSampleRow) {
  if (!isMeasured(row)) {
    return null;
  }

  return Number(row.draftAiLikePercent) - Number(row.rewriteAiLikePercent);
}

function hasPassingSignal(row: LearningSampleRow) {
  const drop = signalDrop(row);
  return (
    isMeasured(row) &&
    (Number(row.rewriteAiLikePercent) < 50 || (drop !== null && drop >= 30))
  );
}

function createFinding({
  rows,
  scenario,
  failureType,
  severity,
  recommendation,
  promotionRecommendation,
}: {
  rows: LearningSampleRow[];
  scenario: string | null;
  failureType: LearningFailureType;
  severity: LearningSeverity;
  recommendation: string;
  promotionRecommendation: LearningPromotionRecommendation;
}): LearningFindingDraft {
  return {
    scenario,
    failureType,
    diagnosisTags: uniqueTags(rows),
    evidenceCount: rows.length,
    severity,
    recommendation,
    promotionRecommendation,
    sampleRefs: sampleRefs(rows),
  };
}

function createStrategyCandidate(
  finding: LearningFindingDraft,
  findingIndex: number,
): StrategyCandidateDraft | null {
  if (finding.promotionRecommendation !== "code-change") {
    return null;
  }

  const scenarioText = finding.scenario ?? "all scenarios";

  if (finding.failureType === "worse_than_draft") {
    return {
      findingIndex,
      title: `Prevent worse-than-draft rewrite selection for ${scenarioText}`,
      scenario: finding.scenario,
      proposedChangeSummary:
        "Tighten candidate selection and targeted repair so measured rewrites that are worse than the draft cannot be returned as successful results.",
      riskLevel: finding.severity,
      status: "proposed",
      requiredEval:
        "Add a regression case using the failing sample pattern, assert final selected rewrite is not worse than the draft, and verify facts/next steps are preserved.",
      evidenceCount: finding.evidenceCount,
    };
  }

  if (finding.failureType === "high_final_signal") {
    return {
      findingIndex,
      title: `Reduce repeated high final AI-like signal for ${scenarioText}`,
      scenario: finding.scenario,
      proposedChangeSummary:
        "Add scenario-specific repair guidance for repeated high final AI-like signal while preserving required facts and useful detail.",
      riskLevel: finding.severity,
      status: "proposed",
      requiredEval:
        "Add or update scenario evaluation cases for this pattern and require at least 30 points of reduction or below-50 final signal.",
      evidenceCount: finding.evidenceCount,
    };
  }

  return null;
}

export function analyzeLearningSamples(
  samples: LearningSampleRow[],
): LearningAnalysisResult {
  const measured = samples.filter(isMeasured);
  const drops = measured
    .map(signalDrop)
    .filter((drop): drop is number => drop !== null);
  const summary: LearningAnalysisSummary = {
    sampleCount: samples.length,
    measuredCount: measured.length,
    successCount: samples.filter((row) => row.status === "success").length,
    qualityFailureCount: samples.filter(
      (row) => row.status === "quality_failed",
    ).length,
    averageDropPoints: average(drops),
    below50Count: measured.filter(
      (row) => Number(row.rewriteAiLikePercent) < 50,
    ).length,
    worseThanDraftCount: measured.filter(
      (row) => Number(row.rewriteAiLikePercent) >= Number(row.draftAiLikePercent),
    ).length,
  };

  const findings: LearningFindingDraft[] = [];

  for (const [scenario, scenarioRows] of byScenario(samples).entries()) {
    const scenarioMeasured = scenarioRows.filter(isMeasured);
    const worseRows = scenarioMeasured.filter(
      (row) =>
        Number(row.rewriteAiLikePercent) >= Number(row.draftAiLikePercent),
    );
    if (worseRows.length > 0) {
      findings.push(
        createFinding({
          rows: worseRows,
          scenario,
          failureType: "worse_than_draft",
          severity: "high",
          recommendation:
            "Add a regression case and tighten selection/repair so this pattern cannot return a worse measured rewrite.",
          promotionRecommendation: "code-change",
        }),
      );
    }

    const highFinalRows = scenarioMeasured.filter((row) => {
      const drop = signalDrop(row);
      return (
        Number(row.rewriteAiLikePercent) >= 50 &&
        (drop === null || drop < 30) &&
        Number(row.rewriteAiLikePercent) < Number(row.draftAiLikePercent)
      );
    });
    if (highFinalRows.length >= MIN_REPEATED_FAILURES_FOR_PROMOTION) {
      findings.push(
        createFinding({
          rows: highFinalRows,
          scenario,
          failureType: "high_final_signal",
          severity: "medium",
          recommendation:
            "Improve scenario guardrails or repair prompts for repeated high final AI-like signal.",
          promotionRecommendation: "code-change",
        }),
      );
    }

    const qualityFailures = scenarioRows.filter(
      (row) => row.status === "quality_failed",
    );
    if (qualityFailures.length >= MIN_REPEATED_FAILURES_FOR_PROMOTION) {
      findings.push(
        createFinding({
          rows: qualityFailures,
          scenario,
          failureType: "quality_gate_failure",
          severity: "medium",
          recommendation:
            "Add eval coverage for repeated quality-gate failures and inspect rejected candidate reasons.",
          promotionRecommendation: "test-needed",
        }),
      );
    }

    const repairedSuccessRows = scenarioMeasured.filter(
      (row) =>
        row.status === "success" &&
        row.repairCandidates > 0 &&
        row.rejectedCandidates > 0 &&
        hasPassingSignal(row),
    );
    if (repairedSuccessRows.length >= MIN_REPAIR_SUCCESSES_FOR_MEMORY) {
      findings.push(
        createFinding({
          rows: repairedSuccessRows,
          scenario,
          failureType: "repair_success",
          severity: "low",
          recommendation:
            "Record this repair pattern in strategy memory and consider regression coverage if it repeats.",
          promotionRecommendation: "docs-only",
        }),
      );
    }
  }

  const strategyCandidates = findings
    .map((finding, index) => createStrategyCandidate(finding, index))
    .filter((candidate): candidate is StrategyCandidateDraft => candidate !== null);

  let promotionDecision: LearningPromotionDecision = "digest_only";
  if (strategyCandidates.length > 0) {
    promotionDecision = "promoted_candidate";
  } else if (
    findings.some(
      (finding) =>
        finding.promotionRecommendation === "docs-only" ||
        finding.promotionRecommendation === "test-needed",
    )
  ) {
    promotionDecision = "docs_only";
  }

  return {
    summary,
    findings,
    strategyCandidates,
    promotionDecision,
  };
}
