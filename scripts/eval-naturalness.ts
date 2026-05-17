import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";

import { rewriteWithOptimization } from "../lib/rewrite";
import { rewriteRequestSchema } from "../lib/validation";

type Sample = {
  id: string;
  messageToReplyTo?: string;
  roughDraftReply: string;
  audience?: string;
  purpose?: string;
  whatHappened?: string;
  factsToPreserve?: string;
  tone: "warm" | "direct";
};

function minutesFromEnv(name: string, fallback: number) {
  const value = Number(process.env[name]);
  return Number.isFinite(value) && value > 0 ? value : fallback;
}

function points(value: number | null) {
  return value === null ? "unavailable" : `${value}%`;
}

const startedAt = Date.now();
const maxMinutes = minutesFromEnv("EVAL_MAX_WALLCLOCK_MINUTES", 60);
const maxIterations = Math.min(
  minutesFromEnv("EVAL_MAX_PROMPT_ITERATIONS", 5),
  5,
);
const strategyRoundsUsed = Math.min(
  minutesFromEnv("EVAL_STRATEGY_ROUNDS_USED", 1),
  maxIterations,
);

const samplesPath = path.join(process.cwd(), "evals", "samples.json");
const outputPath = path.join(process.cwd(), "docs", "optimization-notes.md");

const samples = JSON.parse(await readFile(samplesPath, "utf8")) as Sample[];
const selectedSamples = samples.slice(0, 12);

const rows = [];

for (const sample of selectedSamples) {
  const elapsedMinutes = (Date.now() - startedAt) / 60000;
  if (elapsedMinutes >= maxMinutes) {
    break;
  }

  const input = rewriteRequestSchema.parse(sample);
  const result = await rewriteWithOptimization(input);
  rows.push({
    id: sample.id,
    tone: sample.tone,
    draft: result.naturalness.draftAiLikePercent,
    rewrite: result.naturalness.rewriteAiLikePercent,
    delta: result.naturalness.changePoints,
    strategies: result.optimization.internalStrategiesTried,
    notes: result.riskNotes.join("; "),
  });
}

const measured = rows.filter(
  (row) => row.draft !== null && row.rewrite !== null && row.delta !== null,
);
const averageDrop = measured.length
  ? Math.round(
      measured.reduce((total, row) => total + Math.abs(row.delta ?? 0), 0) /
        measured.length,
    )
  : null;
const belowFifty = measured.filter((row) => (row.rewrite ?? 100) < 50).length;
const targetMet =
  measured.length === rows.length &&
  averageDrop !== null &&
  averageDrop >= 30 &&
  belowFifty >= Math.ceil(measured.length / 2);

const lines = [
  "# Optimization Notes",
  "",
  `Date: ${new Date().toISOString()}`,
  `Samples evaluated: ${rows.length}`,
  `Evaluation strategy rounds used: ${strategyRoundsUsed} of max ${maxIterations}`,
  `Average absolute signal drop: ${
    averageDrop === null ? "unavailable" : `${averageDrop} pts`
  }`,
  `Rewrites below 50% AI-like signal: ${belowFifty}/${measured.length}`,
  `Internal target met: ${targetMet ? "yes" : "no"}`,
  "",
  "If the target is not met, the current production strategy is still kept because the evaluation budget is bounded. Future prompt work should focus on preserving concrete facts while reducing generic openings, uniform sentence rhythm, and overly polished closings.",
  "",
  "| Sample | Tone | Draft | Rewrite | Change | Strategies | Notes |",
  "| --- | --- | ---: | ---: | ---: | ---: | --- |",
  ...rows.map((row) =>
    [
      row.id,
      row.tone,
      points(row.draft),
      points(row.rewrite),
      row.delta === null ? "unavailable" : `${row.delta} pts`,
      String(row.strategies),
      row.notes.replace(/\|/g, "/"),
    ].join(" | "),
  ),
  "",
];

await writeFile(outputPath, lines.join("\n"));
console.log(`Wrote ${outputPath}`);
