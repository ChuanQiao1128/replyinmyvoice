import { describe, expect, it } from "vitest";

import {
  runLearningOpsPipeline,
  learningWindowStart,
  type LearningOpsSqlClient,
} from "../../lib/learningops/run";
import type { LearningSampleRow } from "../../lib/learningops";

type SqlStatement = {
  text: string;
  values: unknown[];
};

function sample(overrides: Partial<LearningSampleRow>): LearningSampleRow {
  return {
    id: "sample_1",
    scenario: "customer_support",
    tonePreset: "Warm",
    status: "quality_failed",
    draftAiLikePercent: 92,
    rewriteAiLikePercent: 72,
    changePoints: -20,
    diagnosisTags: JSON.stringify(["support_template_voice"]),
    repairCandidates: 0,
    rejectedCandidates: 1,
    createdAt: "2026-05-22T00:00:00.000Z",
    ...overrides,
  };
}

function createSqlClient({
  rows,
  failOnRead = false,
}: {
  rows: LearningSampleRow[];
  failOnRead?: boolean;
}) {
  const statements: SqlStatement[] = [];
  const sql = (async (strings: TemplateStringsArray, ...values: unknown[]) => {
    const text = strings.join("?");
    statements.push({ text, values });

    if (text.includes('FROM "RewriteLearningSample"')) {
      if (failOnRead) {
        throw new Error("sample read failed");
      }
      return rows;
    }

    return [];
  }) as LearningOpsSqlClient;

  return { sql, statements };
}

describe("LearningOps scheduled pipeline", () => {
  it("computes the seven-day learning sample window", () => {
    expect(
      learningWindowStart(new Date("2026-05-22T12:00:00.000Z")).toISOString(),
    ).toBe("2026-05-15T12:00:00.000Z");
  });

  it("reads recent samples, records promoted status, and drafts a promotion task", async () => {
    const { sql, statements } = createSqlClient({
      rows: [
        sample({ id: "case_1" }),
        sample({ id: "case_2" }),
      ],
    });
    const digests: string[] = [];
    const promotionTasks: string[] = [];

    const result = await runLearningOpsPipeline({
      sql,
      now: new Date("2026-05-22T12:00:00.000Z"),
      writeDigest: async (_path, content) => {
        digests.push(content);
      },
      writePromotionTask: async (content) => {
        promotionTasks.push(content);
      },
    });

    expect(result.status).toBe("promoted");
    expect(result.analysis.summary.sampleCount).toBe(2);
    expect(result.insertedCandidates).toHaveLength(1);
    expect(digests[0]).toContain("Promotion decision: promoted");
    expect(promotionTasks[0]).toContain("# LearningOps Strategy Promotion Task");

    const readStatement = statements.find((statement) =>
      statement.text.includes('FROM "RewriteLearningSample"'),
    );
    expect(readStatement?.text).toContain('WHERE "createdAt" >=');
    expect(readStatement?.values[0]).toEqual(
      new Date("2026-05-15T12:00:00.000Z"),
    );
  });

  it("marks an already-created run as blocked when sample reading fails", async () => {
    const { sql, statements } = createSqlClient({
      rows: [],
      failOnRead: true,
    });

    const result = await runLearningOpsPipeline({
      sql,
      now: new Date("2026-05-22T12:00:00.000Z"),
    });

    expect(result.status).toBe("blocked");
    expect(result.errorMessage).toBe("sample read failed");
    expect(
      statements.some(
        (statement) =>
          statement.text.includes('UPDATE "LearningRun"') &&
          statement.values.includes("blocked"),
      ),
    ).toBe(true);
  });
});
