import fs from "node:fs";
import path from "node:path";

import { neon } from "@neondatabase/serverless";

import {
  runLearningOpsPipeline,
  type LearningOpsSqlClient,
} from "../lib/learningops/run";

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

async function main() {
  loadLocalEnv();
  const databaseUrl = process.env.DATABASE_URL;
  if (!databaseUrl) {
    throw new Error("DATABASE_URL is required to run LearningOps.");
  }

  const sql = neon(databaseUrl) as LearningOpsSqlClient;
  const result = await runLearningOpsPipeline({
    sql,
    writeDigest: async (digestPath, content) => {
      fs.writeFileSync(path.join(process.cwd(), digestPath), content);
    },
    writePromotionTask: async (content, taskPath) => {
      fs.writeFileSync(path.join(process.cwd(), taskPath), content);
    },
  });

  console.log(
    [
      `LearningOps run ${result.runId}`,
      `samples=${result.analysis.summary.sampleCount}`,
      `findings=${result.analysis.findings.length}`,
      `candidates=${result.analysis.strategyCandidates.length}`,
      `status=${result.status}`,
      "promotion_task=plans/learningops-promotion-task.md",
    ].join(" "),
  );

  if (result.status === "blocked") {
    process.exitCode = 1;
  }
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : "LearningOps failed");
  process.exit(1);
});
