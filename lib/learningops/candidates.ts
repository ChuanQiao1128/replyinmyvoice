import type { LearningFindingDraft, StrategyCandidateDraft } from "../learningops";

type ClusterPatch = Pick<
  StrategyCandidateDraft,
  "patchTarget" | "patchAction" | "patchText"
>;

const PROMOTABLE_CLUSTER_MIN_EVIDENCE = 2;

function scenarioLabel(scenario: string | null | undefined) {
  return scenario ?? "all scenarios";
}

function primaryTag(finding: LearningFindingDraft) {
  return (
    finding.primaryDiagnosisTag ??
    finding.diagnosisTags.find((tag) => tag.trim().length > 0) ??
    "untagged"
  );
}

function requiredRegressionTest(finding: LearningFindingDraft) {
  const tag = primaryTag(finding);
  const scenario = scenarioLabel(finding.scenario);
  const tags =
    finding.diagnosisTags.length > 0
      ? ` with diagnosis tags ${finding.diagnosisTags.join(", ")}`
      : "";
  const samples =
    finding.sampleRefs.length > 0
      ? ` using sample refs ${finding.sampleRefs.slice(0, 5).join(", ")}`
      : "";

  return `Add a regression case for ${scenario}/${tag}${tags}${samples}; assert the selected rewrite preserves required facts, avoids the diagnosed pattern, and keeps the reply send-ready.`;
}

function clusterPatchFor(finding: LearningFindingDraft): ClusterPatch {
  const tag = primaryTag(finding);
  const scenario = scenarioLabel(finding.scenario);

  if (
    tag === "quote_or_list_risk" ||
    tag === "broken_numbered_list" ||
    tag === "broken_quote_boundary"
  ) {
    return {
      patchTarget: "strategy_router",
      patchAction: "route",
      patchText: `Route ${scenario} findings tagged ${tag} to the quote/list-safe rewrite strategy; preserve quoted boundaries and keep list markers attached to their items.`,
    };
  }

  if (tag === "fact_loss" || tag === "unsupported_fact") {
    return {
      patchTarget: "quality_gate",
      patchAction: "tighten",
      patchText: `Tighten fact gates for ${scenario} findings tagged ${tag}; reject candidates that drop critical facts or add promises, names, timelines, discounts, or outcomes not supplied by the user.`,
    };
  }

  if (tag === "sentence_per_paragraph" || tag === "line_split_paraphrase") {
    return {
      patchTarget: "generation_prompt",
      patchAction: "add_guardrail",
      patchText: `Add to the ${scenario} generation prompt for ${tag}: rebuild from the reviewed fact ledger instead of splitting the draft line by line; group related facts into natural paragraphs.`,
    };
  }

  if (tag === "corporate_polish" || tag === "stock_opening") {
    return {
      patchTarget: "repair_prompt",
      patchAction: "add_guardrail",
      patchText: `Add to the ${scenario} repair prompt for ${tag}: remove stock openings and corporate polish while keeping concrete facts, next steps, and the user's chosen tone.`,
    };
  }

  return {
    patchTarget: "repair_prompt",
    patchAction: "add_guardrail",
    patchText: `Add to the ${scenario} repair prompt for ${tag}: avoid balanced 4-paragraph structure and support-template rhythm; combine related facts naturally without losing required details.`,
  };
}

function clusterCandidate(
  finding: LearningFindingDraft,
  findingIndex: number,
): StrategyCandidateDraft | null {
  if (
    finding.evidenceCount < PROMOTABLE_CLUSTER_MIN_EVIDENCE ||
    finding.promotionRecommendation === "no-op"
  ) {
    return null;
  }

  const tag = primaryTag(finding);
  const scenario = scenarioLabel(finding.scenario);
  const patch = clusterPatchFor(finding);
  const regressionTest = requiredRegressionTest(finding);

  return {
    findingIndex,
    title: `Patch ${scenario} strategy for ${tag}`,
    scenario: finding.scenario,
    ...patch,
    proposedChangeSummary: `${patch.patchAction} ${patch.patchTarget} for ${scenario}: ${patch.patchText}`,
    riskLevel: finding.severity,
    status: "proposed",
    requiredRegressionTest: regressionTest,
    requiredEval: regressionTest,
    evidenceCount: finding.evidenceCount,
  };
}

export function proposeStrategyCandidate(
  finding: LearningFindingDraft,
  findingIndex: number,
): StrategyCandidateDraft | null {
  if (finding.failureType === "diagnosis_tag_cluster") {
    return clusterCandidate(finding, findingIndex);
  }

  if (finding.promotionRecommendation !== "code-change") {
    return null;
  }

  const scenario = scenarioLabel(finding.scenario);

  if (finding.failureType === "worse_than_draft") {
    const requiredEval =
      "Add a regression case using the failing sample pattern, assert final selected rewrite is not worse than the draft, and verify facts/next steps are preserved.";

    return {
      findingIndex,
      title: `Prevent worse-than-draft rewrite selection for ${scenario}`,
      scenario: finding.scenario,
      patchTarget: "quality_gate",
      patchAction: "tighten",
      patchText:
        "Tighten candidate selection and targeted repair so measured rewrites that are worse than the draft cannot be returned as successful results.",
      proposedChangeSummary:
        "Tighten candidate selection and targeted repair so measured rewrites that are worse than the draft cannot be returned as successful results.",
      riskLevel: finding.severity,
      status: "proposed",
      requiredRegressionTest: requiredEval,
      requiredEval,
      evidenceCount: finding.evidenceCount,
    };
  }

  if (finding.failureType === "high_final_signal") {
    const requiredEval =
      "Add or update scenario evaluation cases for this pattern and require at least 30 points of reduction or below-50 final signal.";

    return {
      findingIndex,
      title: `Reduce repeated high final AI-like signal for ${scenario}`,
      scenario: finding.scenario,
      patchTarget: "repair_prompt",
      patchAction: "add_guardrail",
      patchText:
        "Add scenario-specific repair guidance for repeated high final AI-like signal while preserving required facts and useful detail.",
      proposedChangeSummary:
        "Add scenario-specific repair guidance for repeated high final AI-like signal while preserving required facts and useful detail.",
      riskLevel: finding.severity,
      status: "proposed",
      requiredRegressionTest: requiredEval,
      requiredEval,
      evidenceCount: finding.evidenceCount,
    };
  }

  return null;
}
