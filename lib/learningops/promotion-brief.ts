import type { StrategyCandidateDraft } from "../learningops";

type StrategyPatchTarget = StrategyCandidateDraft["patchTarget"];

export type StrategyCandidatePromotionOptions = {
  candidateId?: string;
  findingId?: string;
  issueId?: string;
  runId?: string;
};

export type StrategyCandidatePromotionPolicy = {
  pullRequestMode: "draft_only";
  autoMergeAllowed: false;
  productionHotLoadAllowed: false;
  includeRawLearningSamples: false;
};

export type StrategyCandidatePromotionBrief = {
  summary: string;
  policy: StrategyCandidatePromotionPolicy;
  candidate: {
    id: string | null;
    findingId: string | null;
    runId: string | null;
    issueId: string | null;
    title: string;
    scenario: string | null;
    patchTarget: StrategyPatchTarget;
    patchAction: StrategyCandidateDraft["patchAction"];
    patchText: string;
    proposedChangeSummary: string;
    riskLevel: StrategyCandidateDraft["riskLevel"];
    status: string;
    requiredRegressionTest: string;
    requiredEval: string;
    evidenceCount: number;
  };
  filesToInspect: string[];
  requiredChanges: string[];
  validationCommands: string[];
  safetyConstraints: string[];
};

type StrategyCandidateLike = Omit<StrategyCandidateDraft, "status"> & {
  status: string;
};

const promotionPolicy: StrategyCandidatePromotionPolicy = {
  pullRequestMode: "draft_only",
  autoMergeAllowed: false,
  productionHotLoadAllowed: false,
  includeRawLearningSamples: false,
};

const validationCommands = [
  "npm run lint",
  "npm run typecheck",
  "npm run test",
];

const targetFiles: Record<StrategyPatchTarget, string[]> = {
  generation_prompt: [
    "lib/rewrite-pipeline/model.ts",
    "lib/rewrite-pipeline/style-cards.ts",
    "lib/rewrite-pipeline/strategy-catalog.ts",
  ],
  repair_prompt: [
    "lib/rewrite-pipeline/model.ts",
    "lib/rewrite-pipeline/targeted-repair.ts",
    "lib/rewrite-pipeline/strategy-catalog.ts",
  ],
  strategy_router: [
    "lib/rewrite-pipeline/strategy-router.ts",
    "lib/rewrite-pipeline/input-analyzer.ts",
    "lib/rewrite-pipeline/strategy-catalog.ts",
  ],
  quality_gate: [
    "lib/rewrite-quality-gate.ts",
    "lib/rewrite-pipeline/checks.ts",
    "lib/rewrite-pipeline/pipeline.ts",
  ],
};

const targetTests: Record<StrategyPatchTarget, string[]> = {
  generation_prompt: [
    "tests/unit/rewrite-pipeline.test.ts",
    "tests/unit/rewrite-pipeline-model.test.ts",
  ],
  repair_prompt: [
    "tests/unit/rewrite-targeted-repair.test.ts",
    "tests/unit/rewrite-pipeline.test.ts",
  ],
  strategy_router: [
    "tests/unit/rewrite-strategy-router.test.ts",
    "tests/unit/rewrite-input-analyzer.test.ts",
  ],
  quality_gate: [
    "tests/unit/rewrite-quality-gate.test.ts",
    "tests/unit/rewrite-api-quality.test.ts",
  ],
};

function unique(items: string[]) {
  return Array.from(new Set(items));
}

function valueOrNull(value?: string) {
  return value?.trim() ? value.trim() : null;
}

function candidateId(options: StrategyCandidatePromotionOptions) {
  return valueOrNull(options.candidateId);
}

export function isPromotableStrategyCandidate(
  candidate: Pick<
    StrategyCandidateLike,
    | "evidenceCount"
    | "patchText"
    | "requiredRegressionTest"
    | "requiredEval"
    | "status"
  >,
) {
  return (
    ["approved", "promotable", "proposed"].includes(candidate.status) &&
    candidate.evidenceCount > 0 &&
    candidate.patchText.trim().length > 0 &&
    candidate.requiredRegressionTest.trim().length > 0 &&
    candidate.requiredEval.trim().length > 0
  );
}

export function buildStrategyCandidatePromotionBrief(
  candidate: StrategyCandidateLike,
  options: StrategyCandidatePromotionOptions = {},
): StrategyCandidatePromotionBrief {
  const id = candidateId(options);

  return {
    summary: `Promote StrategyCandidate ${id ?? "(unpersisted)"}: ${candidate.title}`,
    policy: promotionPolicy,
    candidate: {
      id,
      findingId: valueOrNull(options.findingId),
      runId: valueOrNull(options.runId),
      issueId: valueOrNull(options.issueId),
      title: candidate.title,
      scenario: candidate.scenario,
      patchTarget: candidate.patchTarget,
      patchAction: candidate.patchAction,
      patchText: candidate.patchText,
      proposedChangeSummary: candidate.proposedChangeSummary,
      riskLevel: candidate.riskLevel,
      status: candidate.status,
      requiredRegressionTest: candidate.requiredRegressionTest,
      requiredEval: candidate.requiredEval,
      evidenceCount: candidate.evidenceCount,
    },
    filesToInspect: unique([
      ...targetFiles[candidate.patchTarget],
      ...targetTests[candidate.patchTarget],
      "docs/rewrite-strategy-memory.md",
    ]),
    requiredChanges: [
      "Update the narrowest repair prompt, strategy guidance, or scenario guardrail that implements the candidate patch.",
      "Add or update a regression test that proves the candidate pattern is fixed.",
      "Update docs/rewrite-strategy-memory.md with the reusable lesson and validation evidence.",
      "Keep production strategy code-based; do not add a database-read prompt rule.",
      "Open a draft pull request for human review and stop there.",
    ],
    validationCommands,
    safetyConstraints: [
      "Do not print or copy raw learning sample content.",
      "Do not read or modify .env.local, .dev.vars, globalapikey/, or provider credentials.",
      "Do not modify Stripe pricing, webhooks, live-mode flags, or launch flags.",
      "Do not deploy.",
      "Do not merge the pull request automatically.",
    ],
  };
}

function bulletList(items: string[]) {
  return items.map((item) => `- ${item}`).join("\n");
}

export function buildStrategyCandidatePromotionTask(
  candidate: StrategyCandidateLike,
  options: StrategyCandidatePromotionOptions = {},
) {
  const brief = buildStrategyCandidatePromotionBrief(candidate, options);

  return [
    "# LearningOps Strategy Promotion Task",
    "",
    `Candidate id: ${brief.candidate.id ?? "(unpersisted)"}`,
    brief.candidate.findingId
      ? `Finding id: ${brief.candidate.findingId}`
      : "Finding id: (not provided)",
    brief.candidate.runId
      ? `Learning run: ${brief.candidate.runId}`
      : "Learning run: (not provided)",
    brief.candidate.issueId
      ? `Issue: ${brief.candidate.issueId}`
      : "Issue: (not provided)",
    "PR mode: draft only; do not merge automatically.",
    "",
    "## Goal",
    "",
    brief.summary,
    "",
    "## Candidate",
    "",
    `- Title: ${brief.candidate.title}`,
    `- Scenario: ${brief.candidate.scenario ?? "all scenarios"}`,
    `- Patch target: ${brief.candidate.patchTarget}`,
    `- Patch action: ${brief.candidate.patchAction}`,
    `- Risk level: ${brief.candidate.riskLevel}`,
    `- Evidence count: ${brief.candidate.evidenceCount}`,
    `- Status: ${brief.candidate.status}`,
    "",
    "## Proposed Change",
    "",
    brief.candidate.patchText,
    "",
    "## Required Regression Test",
    "",
    brief.candidate.requiredRegressionTest,
    "",
    "## Required Evaluation",
    "",
    brief.candidate.requiredEval,
    "",
    "## Files To Inspect",
    "",
    bulletList(brief.filesToInspect),
    "",
    "## Required Changes",
    "",
    bulletList(brief.requiredChanges),
    "",
    "## Validation Commands",
    "",
    bulletList(brief.validationCommands),
    "",
    "## Safety Constraints",
    "",
    bulletList(brief.safetyConstraints),
    "",
  ].join("\n");
}
