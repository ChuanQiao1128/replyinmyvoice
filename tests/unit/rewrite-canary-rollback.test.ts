import { describe, expect, it, vi } from "vitest";

import {
  findRewriteCanaryRegressions,
  runRewriteCanaryRollbackMonitor,
  type RewriteCanaryRollbackSqlClient,
} from "../../lib/rewrite-pipeline/canary-rollback";

type SqlStatement = {
  text: string;
  values: unknown[];
};

function createSqlClient() {
  const statements: SqlStatement[] = [];
  const sql = (async (strings: TemplateStringsArray, ...values: unknown[]) => {
    const text = strings.join("?");
    statements.push({ text, values });

    if (
      text.includes('FROM "RewriteCostLog"') &&
      text.includes('ROW_NUMBER()')
    ) {
      return [
        {
          canaryStrategyVersion: "support-policy-v2",
          controlStrategyVersion: "adaptive_rewrite_orchestrator",
          scenario: "customer_support",
          windowRewrites: 50,
          rollingAverageSignalDrop: 28.2,
          baselineAverageSignalDrop: 31.4,
          regressionPoints: 3.2,
          windowStartedAt: new Date("2026-05-22T09:00:00.000Z"),
          windowEndedAt: new Date("2026-05-22T12:00:00.000Z"),
        },
      ];
    }

    if (text.includes('INSERT INTO "RewriteCanaryRollback"')) {
      return [{ id: "rollback_1" }];
    }

    return [];
  }) as RewriteCanaryRollbackSqlClient;

  return { sql, statements };
}

describe("rewrite canary rollback monitor", () => {
  it("finds scenario regressions from a 50-rewrite rolling window", async () => {
    const { sql, statements } = createSqlClient();

    const regressions = await findRewriteCanaryRegressions({
      sql,
      canaryStrategyVersion: "support-policy-v2",
      controlStrategyVersion: "adaptive_rewrite_orchestrator",
      minWindowRewrites: 50,
      regressionThresholdPoints: 3,
    });

    expect(regressions).toEqual([
      expect.objectContaining({
        scenario: "customer_support",
        windowRewrites: 50,
        regressionPoints: 3.2,
      }),
    ]);
    const query = statements[0]?.text ?? "";
    expect(query).toContain("ROW_NUMBER()");
    expect(query).toContain("rn <= ?");
  });

  it("records rollback first, then sends admin email and opens a follow-up issue", async () => {
    const { sql, statements } = createSqlClient();
    const sendAdminEmail = vi.fn().mockResolvedValue({ status: "sent" });
    const openGitHubIssue = vi.fn().mockResolvedValue({
      status: "opened",
      issueUrl: "https://github.com/ChuanQiao1128/replyinmyvoice/issues/321",
    });

    const result = await runRewriteCanaryRollbackMonitor({
      sql,
      env: {
        REWRITE_STRATEGY_CANARY_ENABLED: "true",
        REWRITE_STRATEGY_CANARY_VERSION: "support-policy-v2",
        REWRITE_STRATEGY_CONTROL_VERSION: "adaptive_rewrite_orchestrator",
        ADMIN_EMAILS: "admin@example.com",
        LEARNINGOPS_GITHUB_REPO: "ChuanQiao1128/replyinmyvoice",
      },
      now: new Date("2026-05-22T12:15:00.000Z"),
      sendAdminEmail,
      openGitHubIssue,
    });

    expect(result.triggered).toBe(true);
    expect(result.rollbackId).toBe("rollback_1");
    expect(sendAdminEmail).toHaveBeenCalledWith(
      expect.objectContaining({
        to: "admin@example.com",
        scenario: "customer_support",
        regressionPoints: 3.2,
      }),
    );
    expect(openGitHubIssue).toHaveBeenCalledWith(
      expect.objectContaining({
        repo: "ChuanQiao1128/replyinmyvoice",
        scenario: "customer_support",
      }),
    );
    expect(
      statements.some((statement) =>
        statement.text.includes('INSERT INTO "RewriteCanaryRollback"'),
      ),
    ).toBe(true);
  });

  it("keeps the rollback active when outbound alerts fail", async () => {
    const { sql } = createSqlClient();
    const result = await runRewriteCanaryRollbackMonitor({
      sql,
      env: {
        REWRITE_STRATEGY_CANARY_ENABLED: "true",
        REWRITE_STRATEGY_CANARY_VERSION: "support-policy-v2",
      },
      sendAdminEmail: vi.fn().mockRejectedValue(new Error("email failed")),
      openGitHubIssue: vi.fn().mockRejectedValue(new Error("issue failed")),
    });

    expect(result.triggered).toBe(true);
    expect(result.adminEmailStatus).toBe("failed");
    expect(result.githubIssueStatus).toBe("failed");
    expect(result.errorMessage).toContain("email failed");
  });
});
