import fs from "node:fs";
import path from "node:path";

import { neon } from "@neondatabase/serverless";

type LearningRow = {
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

function loadLocalEnv() {
  const envPath = path.join(process.cwd(), ".env.local");
  if (!fs.existsSync(envPath)) {
    return;
  }

  for (const line of fs.readFileSync(envPath, "utf8").split(/\r?\n/)) {
    if (!line || line.trim().startsWith("#")) {
      continue;
    }
    const index = line.indexOf("=");
    if (index === -1) {
      continue;
    }
    const key = line.slice(0, index).trim();
    let value = line.slice(index + 1).trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }
    process.env[key] = process.env[key] ?? value;
  }
}

function parseTags(value: string) {
  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed)
      ? parsed.filter((tag): tag is string => typeof tag === "string")
      : [];
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

function round(value: number | null) {
  return value === null ? "n/a" : String(Math.round(value));
}

function markdownTable(rows: string[][]) {
  if (rows.length === 0) {
    return "_No rows._";
  }

  const header = rows[0];
  const separator = header.map(() => "---");
  return [header, separator, ...rows.slice(1)]
    .map((row) => `| ${row.join(" | ")} |`)
    .join("\n");
}

function buildDigest(rows: LearningRow[]) {
  const measured = rows.filter(
    (row) =>
      row.draftAiLikePercent !== null && row.rewriteAiLikePercent !== null,
  );
  const successful = rows.filter((row) => row.status === "success");
  const qualityFailed = rows.filter((row) => row.status === "quality_failed");
  const drops = measured.map(
    (row) => Number(row.draftAiLikePercent) - Number(row.rewriteAiLikePercent),
  );
  const below50 = measured.filter(
    (row) => Number(row.rewriteAiLikePercent) < 50,
  ).length;
  const worse = measured.filter(
    (row) => Number(row.rewriteAiLikePercent) >= Number(row.draftAiLikePercent),
  ).length;

  const byScenario = new Map<string, LearningRow[]>();
  const byTag = new Map<string, LearningRow[]>();

  for (const row of rows) {
    byScenario.set(row.scenario, [...(byScenario.get(row.scenario) ?? []), row]);
    for (const tag of parseTags(row.diagnosisTags)) {
      byTag.set(tag, [...(byTag.get(tag) ?? []), row]);
    }
  }

  const scenarioRows = [
    ["Scenario", "Samples", "Avg drop", "Below 50%", "Quality fails"],
    ...Array.from(byScenario.entries()).map(([scenario, scenarioSamples]) => {
      const scenarioMeasured = scenarioSamples.filter(
        (row) =>
          row.draftAiLikePercent !== null && row.rewriteAiLikePercent !== null,
      );
      return [
        scenario,
        String(scenarioSamples.length),
        round(
          average(
            scenarioMeasured.map(
              (row) =>
                Number(row.draftAiLikePercent) -
                Number(row.rewriteAiLikePercent),
            ),
          ),
        ),
        `${scenarioMeasured.filter((row) => Number(row.rewriteAiLikePercent) < 50).length}/${scenarioMeasured.length}`,
        String(
          scenarioSamples.filter((row) => row.status === "quality_failed")
            .length,
        ),
      ];
    }),
  ];

  const tagRows = [
    ["Diagnosis tag", "Samples", "Avg drop", "Quality fails"],
    ...Array.from(byTag.entries())
      .sort((a, b) => b[1].length - a[1].length)
      .slice(0, 12)
      .map(([tag, tagSamples]) => {
        const tagMeasured = tagSamples.filter(
          (row) =>
            row.draftAiLikePercent !== null && row.rewriteAiLikePercent !== null,
        );
        return [
          tag,
          String(tagSamples.length),
          round(
            average(
              tagMeasured.map(
                (row) =>
                  Number(row.draftAiLikePercent) -
                  Number(row.rewriteAiLikePercent),
              ),
            ),
          ),
          String(
            tagSamples.filter((row) => row.status === "quality_failed").length,
          ),
        ];
      }),
  ];

  const recommendations: string[] = [];
  if (worse > 0) {
    recommendations.push(
      `Investigate ${worse} measured sample(s) where rewrite signal was not lower than draft signal.`,
    );
  }
  if (qualityFailed.length > 0) {
    recommendations.push(
      `Review ${qualityFailed.length} quality-gate failure(s) and add eval cases for repeated failure patterns.`,
    );
  }
  for (const [scenario, scenarioSamples] of byScenario.entries()) {
    const scenarioMeasured = scenarioSamples.filter(
      (row) =>
        row.draftAiLikePercent !== null && row.rewriteAiLikePercent !== null,
    );
    if (
      scenarioMeasured.length >= 3 &&
      scenarioMeasured.filter((row) => Number(row.rewriteAiLikePercent) < 50)
        .length <
        Math.ceil(scenarioMeasured.length * 0.7)
    ) {
      recommendations.push(
        `Improve scenario guardrails for ${scenario}; fewer than 70% of measured samples are below 50%.`,
      );
    }
  }
  if (recommendations.length === 0) {
    recommendations.push(
      "No urgent production-prompt change recommended from the current sample set.",
    );
  }

  const generatedAt = new Date().toISOString();

  return `# Rewrite Memory Digest

Generated: ${generatedAt}

This digest is generated from internally stored rewrite learning samples. It intentionally summarizes patterns and does not print user-submitted text.

## Summary

- Samples scanned: ${rows.length}
- Successful rewrites: ${successful.length}
- Quality-gate failures: ${qualityFailed.length}
- Measured samples: ${measured.length}
- Average signal drop: ${round(average(drops))} pts
- Rewrites below 50% AI-like signal: ${below50}/${measured.length}
- Measured rewrites worse than draft: ${worse}/${measured.length}

## By Scenario

${markdownTable(scenarioRows)}

## By Diagnosis Tag

${markdownTable(tagRows)}

## Recommendations

${recommendations.map((item) => `- ${item}`).join("\n")}

## Promotion Rule

Any recommendation must be promoted through:

1. update \`docs/rewrite-strategy-memory.md\`
2. add or update an evaluation case
3. add a deterministic test where possible
4. update prompt guardrails, repair logic, or fallback rules
5. rerun tests and scenario evaluation
`;
}

async function main() {
  loadLocalEnv();
  const databaseUrl = process.env.DATABASE_URL;
  if (!databaseUrl) {
    throw new Error("DATABASE_URL is required to run the Rewrite Memory Agent.");
  }

  const sql = neon(databaseUrl);
  const rows = (await sql`
    SELECT
      "scenario",
      "tonePreset",
      "status",
      "draftAiLikePercent",
      "rewriteAiLikePercent",
      "changePoints",
      "diagnosisTags",
      "repairCandidates",
      "rejectedCandidates",
      "createdAt"
    FROM "RewriteLearningSample"
    ORDER BY "createdAt" DESC
    LIMIT 500
  `) as LearningRow[];

  const digest = buildDigest(rows);
  const outputPath = path.join(process.cwd(), "docs/rewrite-memory-digest.md");
  fs.writeFileSync(outputPath, digest);
  console.log(`Wrote ${path.relative(process.cwd(), outputPath)}`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});
