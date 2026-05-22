import { execFileSync } from "node:child_process";
import { existsSync, mkdtempSync, readFileSync, readdirSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

const scriptPath = path.join(process.cwd(), "scripts/analyze_rewrite_quality.py");
const fixturesDir = path.join(process.cwd(), "tests/fixtures/rewrite-quality-analysis");
const schemaFixturePath = path.join(fixturesDir, "schema.sql");
const populatedFixturePath = path.join(fixturesDir, "populated.sql");
const emptyFixturePath = path.join(fixturesDir, "empty.sql");

const privateFixtureValues = [
  "user_fixture_123",
  "customer@example.com",
  "postgresql://telemetry.invalid/rewrite_quality",
  "PRIVATE-RAW-MESSAGE",
  "PRIVATE-DRAFT-REPLY",
  "PRIVATE-FINAL-REWRITE",
];

function createTelemetryDatabase(dbPath: string, rows: "populated" | "empty") {
  const python = String.raw`
import os
from pathlib import Path
import sqlite3

db_path = os.environ["DB_PATH"]
schema_path = Path(os.environ["SCHEMA_FIXTURE_PATH"])
data_path = Path(os.environ["DATA_FIXTURE_PATH"])
conn = sqlite3.connect(db_path)
cur = conn.cursor()
cur.executescript(schema_path.read_text())
cur.executescript(data_path.read_text())
conn.commit()
conn.close()
`;

  execFileSync("python3", ["-c", python], {
    env: {
      ...process.env,
      DB_PATH: dbPath,
      SCHEMA_FIXTURE_PATH: schemaFixturePath,
      DATA_FIXTURE_PATH: rows === "populated" ? populatedFixturePath : emptyFixturePath,
    },
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
    expect(report).toContain("| Successful rewrites | 2 |");
    expect(report).toContain("| Quality failures | 1 |");
    expect(report).toContain("| Server failures | 1 |");
    expect(report).toContain("| Average signal drop | 31.0 pts |");
    expect(report).toContain("| Below 50% signal rate | 50.0% |");
    expect(report).toContain("| No-regression rate | 50.0% |");
    expect(report).toContain("| Signal unavailable rows | 2 |");
    expect(report).toContain("| Cost per successful rewrite | $0.0200 |");
    expect(report).toContain("| Escalation rate | 25.0% |");
    expect(report).toContain("adaptive_v1");
    expect(report).toContain("adaptive_v2");
    expect(report).toContain("naturalness_gate_failed");
    expect(report).toContain("server_failed");
    expect(report).toContain("sapling_timeout");
    expect(report).toContain("openai_500");
    expect(report).not.toContain(dbPath);
    for (const privateValue of privateFixtureValues) {
      expect(report).not.toContain(privateValue);
    }

    const summary = readFileSync(summaryPath, "utf8");
    expect(summary).toContain("metric,segment,value,period");
    expect(summary).toContain("total_requests,all,4");
    expect(summary).toContain("signal_unavailable_count,all,2");
    expect(summary).toContain("strategy_success_rate,adaptive_v1,50");
    expect(summary).toContain("strategy_success_rate,adaptive_v2,50");
    expect(summary).toContain("strategy_average_signal_drop,adaptive_v1,66");
    expect(summary).toContain("strategy_average_signal_drop,adaptive_v2,-4");
    expect(summary).toContain("provider_error_count,openai:openai_500,1");
    expect(summary).toContain("provider_error_count,sapling:sapling_timeout,1");
    expect(summary).not.toContain(dbPath);
    for (const privateValue of privateFixtureValues) {
      expect(summary).not.toContain(privateValue);
    }

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
