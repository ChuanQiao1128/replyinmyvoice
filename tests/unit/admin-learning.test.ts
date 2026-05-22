import { describe, expect, it } from "vitest";

import {
  getRecentLearningRuns,
  parseStrategyCandidateReviewStatus,
  updateStrategyCandidateStatus,
  type AdminLearningSqlClient,
} from "../../lib/admin/learning";

type SqlStatement = {
  text: string;
  values: unknown[];
};

function createSqlClient(rows: Array<Record<string, unknown>> = []) {
  const statements: SqlStatement[] = [];
  const sql = (async (strings: TemplateStringsArray, ...values: unknown[]) => {
    const text = strings.join("?");
    statements.push({ text, values });

    if (text.includes('FROM "LearningRun"')) {
      return rows;
    }

    if (text.includes('UPDATE "StrategyCandidate"')) {
      return [{ id: values[1], status: values[0] }];
    }

    return [];
  }) as AdminLearningSqlClient;

  return { sql, statements };
}

describe("admin learning dashboard data", () => {
  it("groups recent runs with findings, evidence refs, and candidates", async () => {
    const { sql } = createSqlClient([
      {
        id: "run_1",
        startedAt: "2026-05-22T09:00:00.000Z",
        finishedAt: "2026-05-22T09:05:00.000Z",
        status: "promoted",
        sampleCount: 12,
        measuredCount: 10,
        findingCount: 1,
        candidateCount: 1,
        promotionDecision: "promoted",
        validationStatus: "passed",
        digestPath: "docs/rewrite-memory-digest.md",
        errorMessage: null,
        findingId: "finding_1",
        findingScenario: "customer_support",
        commonTone: "Warm",
        primaryDiagnosisTag: "support_template_voice",
        failureType: "quality_gate_failure",
        diagnosisTags: JSON.stringify([
          "support_template_voice",
          "sentence_per_paragraph",
        ]),
        findingEvidenceCount: 4,
        severity: "medium",
        recommendation: "Group policy facts into a send-ready reply.",
        promotionRecommendation: "code-change",
        sampleRefs: JSON.stringify(["case_1", "case_2"]),
        findingCreatedAt: "2026-05-22T09:01:00.000Z",
        candidateId: "cand_1",
        title: "Patch support policy structure",
        candidateScenario: "customer_support",
        patchTarget: "generation_prompt",
        patchAction: "tighten",
        patchText: "Group status, options, and confirmation constraints.",
        proposedChangeSummary: "Adds support-policy structure guidance.",
        riskLevel: "medium",
        candidateStatus: "proposed",
        requiredRegressionTest: "Add a support policy regression case.",
        requiredEval: "Run focused customer-support eval.",
        candidateEvidenceCount: 4,
        linkedCommitHash: "abc123",
        candidateCreatedAt: "2026-05-22T09:02:00.000Z",
      },
    ]);

    const runs = await getRecentLearningRuns({ sql });

    expect(runs).toHaveLength(1);
    expect(runs[0]).toMatchObject({
      id: "run_1",
      status: "promoted",
      findingCount: 1,
      candidateCount: 1,
    });
    expect(runs[0]?.findings[0]).toMatchObject({
      id: "finding_1",
      scenario: "customer_support",
      diagnosisTags: ["sentence_per_paragraph", "support_template_voice"],
      sampleRefs: ["case_1", "case_2"],
    });
    expect(runs[0]?.findings[0]?.candidates[0]).toMatchObject({
      id: "cand_1",
      status: "proposed",
      patchTarget: "generation_prompt",
      linkedCommitHash: "abc123",
    });
  });

  it("labels linked GitHub pull requests for candidate review", async () => {
    const pullRequestUrl =
      "https://github.com/ChuanQiao1128/replyinmyvoice/pull/184";
    const { sql } = createSqlClient([
      {
        id: "run_pr",
        startedAt: "2026-05-22T09:00:00.000Z",
        finishedAt: null,
        status: "promoted",
        sampleCount: 1,
        measuredCount: 1,
        findingCount: 1,
        candidateCount: 1,
        promotionDecision: "promoted",
        validationStatus: "passed",
        digestPath: null,
        errorMessage: null,
        findingId: "finding_pr",
        findingScenario: "customer_support",
        commonTone: "Warm",
        primaryDiagnosisTag: "support_template_voice",
        failureType: "diagnosis_tag_cluster",
        diagnosisTags: JSON.stringify(["support_template_voice"]),
        findingEvidenceCount: 2,
        severity: "medium",
        recommendation: "Review repeated failed samples.",
        promotionRecommendation: "code-change",
        sampleRefs: JSON.stringify(["case_pr"]),
        findingCreatedAt: "2026-05-22T09:01:00.000Z",
        candidateId: "cand_pr",
        title: "Patch support policy structure",
        candidateScenario: "customer_support",
        patchTarget: "generation_prompt",
        patchAction: "add_guardrail",
        patchText: "Group related facts.",
        proposedChangeSummary: "Adds support-policy structure guidance.",
        riskLevel: "medium",
        candidateStatus: "approved",
        requiredRegressionTest: "Add a support policy regression case.",
        requiredEval: "Run focused customer-support eval.",
        candidateEvidenceCount: 2,
        linkedCommitHash: pullRequestUrl,
        candidateCreatedAt: "2026-05-22T09:02:00.000Z",
      },
    ]);

    const runs = await getRecentLearningRuns({ sql });

    expect(runs[0]?.findings[0]?.candidates[0]).toMatchObject({
      linkedWorkHref: pullRequestUrl,
      linkedWorkLabel: "Pull request",
    });
  });

  it("only accepts admin review statuses for candidate updates", () => {
    expect(parseStrategyCandidateReviewStatus("approved")).toBe("approved");
    expect(parseStrategyCandidateReviewStatus("needs_revision")).toBe(
      "needs_revision",
    );
    expect(parseStrategyCandidateReviewStatus("rejected")).toBe("rejected");
    expect(parseStrategyCandidateReviewStatus("proposed")).toBeNull();
    expect(parseStrategyCandidateReviewStatus("promotable")).toBeNull();
  });

  it("updates the candidate status with a scoped SQL statement", async () => {
    const { sql, statements } = createSqlClient();

    const result = await updateStrategyCandidateStatus({
      sql,
      candidateId: "cand_1",
      status: "approved",
    });

    expect(result).toEqual({ id: "cand_1", status: "approved" });
    expect(statements[0]?.text).toContain('UPDATE "StrategyCandidate"');
    expect(statements[0]?.text).toContain('WHERE "id" =');
    expect(statements[0]?.values).toEqual(["approved", "cand_1"]);
  });

  it("rejects blank candidate ids before writing", async () => {
    const { sql, statements } = createSqlClient();

    await expect(
      updateStrategyCandidateStatus({
        sql,
        candidateId: " ",
        status: "rejected",
      }),
    ).rejects.toThrow("Strategy candidate id is required");
    expect(statements).toHaveLength(0);
  });
});
