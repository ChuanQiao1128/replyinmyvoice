import { execFileSync } from "node:child_process";
import { existsSync, mkdtempSync, readFileSync, readdirSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

const scriptPath = path.join(process.cwd(), "scripts/analyze_rewrite_quality.py");

function createTelemetryDatabase(dbPath: string, rows: "populated" | "empty") {
  const python = String.raw`
import os
import sqlite3

db_path = os.environ["DB_PATH"]
conn = sqlite3.connect(db_path)
cur = conn.cursor()
cur.executescript("""
CREATE TABLE "RewriteCostLog" (
  "id" TEXT PRIMARY KEY,
  "requestId" TEXT,
  "strategyVersion" TEXT,
  "scenario" TEXT,
  "tonePreset" TEXT,
  "status" TEXT,
  "errorCode" TEXT,
  "startedAt" TEXT,
  "finishedAt" TEXT,
  "durationMs" INTEGER,
  "draftAiLikePercent" INTEGER,
  "rewriteAiLikePercent" INTEGER,
  "changePoints" INTEGER,
  "internalStrategies" INTEGER,
  "repairCandidates" INTEGER,
  "rejectedCandidates" INTEGER,
  "usedEscalation" INTEGER,
  "openAiInputTokens" INTEGER,
  "openAiOutputTokens" INTEGER,
  "openAiCostUsd" REAL,
  "saplingCallCount" INTEGER,
  "saplingCharacters" INTEGER,
  "saplingCostUsd" REAL,
  "totalEstimatedCostUsd" REAL,
  "createdAt" TEXT
);
CREATE TABLE "RewriteProviderCall" (
  "id" TEXT PRIMARY KEY,
  "costLogId" TEXT,
  "provider" TEXT,
  "role" TEXT,
  "model" TEXT,
  "inputTokens" INTEGER,
  "outputTokens" INTEGER,
  "characters" INTEGER,
  "estimatedCostUsd" REAL,
  "latencyMs" INTEGER,
  "success" INTEGER,
  "errorCode" TEXT,
  "createdAt" TEXT
);
""")
if os.environ["ROWS"] == "populated":
    cur.executemany(
        'INSERT INTO "RewriteCostLog" VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)',
        [
            ("cost_1", "req_1", "adaptive_v1", "Customer support", "Warm", "success", None, "2026-05-20T10:00:00Z", "2026-05-20T10:00:01Z", 1000, 88, 22, -66, 3, 1, 2, 0, 1200, 350, 0.002, 2, 2400, 0.010, 0.012, "2026-05-20T10:00:00Z"),
            ("cost_2", "req_2", "adaptive_v2", "Sales", "Direct", "success", None, "2026-05-20T11:00:00Z", "2026-05-20T11:00:03Z", 3000, 60, 64, 4, 4, 2, 3, 1, 1600, 500, 0.008, 2, 4000, 0.020, 0.028, "2026-05-20T11:00:00Z"),
            ("cost_3", "req_3", "adaptive_v2", "Workplace", "Warm", "quality_failed", "naturalness_gate_failed", "2026-05-21T09:00:00Z", "2026-05-21T09:00:04Z", 4000, None, None, None, 5, 2, 5, 0, 900, 200, 0.003, 1, 1800, 0.009, 0.012, "2026-05-21T09:00:00Z"),
            ("cost_4", "req_4", "adaptive_v1", "Customer support", "Direct", "server_failed", "server_failed", "2026-05-21T10:00:00Z", "2026-05-21T10:00:02Z", 2000, None, None, None, 1, 0, 0, 0, 500, 0, 0.001, 0, 0, 0.000, 0.001, "2026-05-21T10:00:00Z"),
        ],
    )
    cur.executemany(
        'INSERT INTO "RewriteProviderCall" VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)',
        [
            ("call_1", "cost_1", "openai", "initial_rewrite", "deepseek-v4-pro", 1200, 350, None, 0.002, 800, 1, None, "2026-05-20T10:00:00Z"),
            ("call_2", "cost_1", "sapling", "final_signal", None, None, None, 2400, 0.010, 350, 1, None, "2026-05-20T10:00:01Z"),
            ("call_3", "cost_2", "openai", "strong_escalation", "deepseek-v4-pro-thinking", 1600, 500, None, 0.008, 1400, 1, None, "2026-05-20T11:00:00Z"),
            ("call_4", "cost_2", "sapling", "final_signal", None, None, None, 4000, 0.020, 400, 1, None, "2026-05-20T11:00:03Z"),
            ("call_5", "cost_3", "sapling", "final_signal", None, None, None, 1800, 0.009, 900, 0, "sapling_timeout", "2026-05-21T09:00:03Z"),
            ("call_6", "cost_4", "openai", "initial_rewrite", "deepseek-v4-pro", 500, 0, None, 0.001, 2000, 0, "openai_500", "2026-05-21T10:00:00Z"),
        ],
    )
conn.commit()
conn.close()
`;

  execFileSync("python3", ["-c", python], {
    env: { ...process.env, DB_PATH: dbPath, ROWS: rows },
  });
}

function runAnalyzer(dbPath: string, outputRoot: string) {
  const reportPath = path.join(outputRoot, "docs/report.md");
  const summaryPath = path.join(outputRoot, "exports/rewrite-quality-summary.csv");
  const chartsDir = path.join(outputRoot, "exports/charts");

  execFileSync(
    "python3",
    [
      scriptPath,
      "--since",
      "2026-05-20",
      "--until",
      "2026-05-22",
      "--output",
      reportPath,
      "--summary-csv",
      summaryPath,
      "--charts-dir",
      chartsDir,
    ],
    {
      env: { ...process.env, DATABASE_URL: `sqlite:///${dbPath}` },
      stdio: "pipe",
    },
  );

  return { reportPath, summaryPath, chartsDir };
}

describe("offline rewrite quality analysis script", () => {
  it("aggregates rewrite telemetry without emitting private identifiers", () => {
    const root = mkdtempSync(path.join(tmpdir(), "rewrite-quality-"));
    const dbPath = path.join(root, "telemetry.sqlite");
    createTelemetryDatabase(dbPath, "populated");

    const { reportPath, summaryPath, chartsDir } = runAnalyzer(dbPath, root);

    const report = readFileSync(reportPath, "utf8");
    expect(report).toContain("| Total requests | 4 |");
    expect(report).toContain("| Average signal drop | 31.0 pts |");
    expect(report).toContain("| No-regression rate | 50.0% |");
    expect(report).toContain("| Cost per successful rewrite | $0.0200 |");
    expect(report).toContain("| Escalation rate | 25.0% |");
    expect(report).toContain("adaptive_v1");
    expect(report).toContain("adaptive_v2");
    expect(report).toContain("sapling_timeout");
    expect(report).toContain("openai_500");
    expect(report).not.toContain("user_fixture_123");
    expect(report).not.toContain("customer@example.com");
    expect(report).not.toContain(dbPath);

    const summary = readFileSync(summaryPath, "utf8");
    expect(summary).toContain("metric,segment,value,period");
    expect(summary).toContain("total_requests,all,4");
    expect(summary).toContain("strategy_success_rate,adaptive_v1");

    const charts = readdirSync(chartsDir).filter((name) => name.endsWith(".png"));
    expect(charts).toHaveLength(6);
    for (const chart of charts) {
      const bytes = readFileSync(path.join(chartsDir, chart));
      expect(bytes.subarray(0, 8).toString("hex")).toBe("89504e470d0a1a0a");
    }
  });

  it("writes empty artifacts when no matching telemetry rows exist", () => {
    const root = mkdtempSync(path.join(tmpdir(), "rewrite-quality-empty-"));
    const dbPath = path.join(root, "telemetry.sqlite");
    createTelemetryDatabase(dbPath, "empty");

    const { reportPath, summaryPath, chartsDir } = runAnalyzer(dbPath, root);

    expect(readFileSync(reportPath, "utf8")).toContain("No matching rows were found");
    expect(readFileSync(summaryPath, "utf8")).toContain("total_requests,all,0");
    expect(readdirSync(chartsDir).filter((name) => name.endsWith(".png"))).toHaveLength(6);
    expect(existsSync(path.join(chartsDir, "daily_rewrite_volume.png"))).toBe(true);
  });
});
