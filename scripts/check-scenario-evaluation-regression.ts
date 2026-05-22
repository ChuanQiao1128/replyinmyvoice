import { readFileSync } from "node:fs";
import { parseArgs } from "node:util";

import { compareScenarioEvaluationResults } from "../lib/scenario-evaluation-regression";

const { values } = parseArgs({
  options: {
    base: {
      type: "string",
    },
    candidate: {
      type: "string",
    },
  },
});

if (!values.base || !values.candidate) {
  console.error(
    "Usage: tsx scripts/check-scenario-evaluation-regression.ts --base <main-results.md> --candidate <candidate-results.md>",
  );
  process.exit(2);
}

try {
  const result = compareScenarioEvaluationResults({
    baseMarkdown: readFileSync(values.base, "utf8"),
    candidateMarkdown: readFileSync(values.candidate, "utf8"),
  });

  if (result.passed) {
    console.log(
      [
        "Scenario evaluation regression check passed.",
        `Average signal reduction drop: ${result.summary.averageSignalReductionDrop.toFixed(
          2,
        )} points.`,
        `Below-50 rate drop: ${result.summary.below50Drop.toFixed(2)} points.`,
      ].join(" "),
    );
    process.exit(0);
  }

  console.error("Scenario evaluation regression check failed:");
  for (const failure of result.failures) {
    console.error(`- ${failure}`);
  }
  process.exit(1);
} catch (error) {
  const message = error instanceof Error ? error.message : String(error);
  console.error(`Scenario evaluation regression check could not run: ${message}`);
  process.exit(1);
}
