import { neon } from "@neondatabase/serverless";

import {
  runLearningOpsPipeline,
  type LearningOpsPipelineResult,
  type LearningOpsSqlClient,
} from "./run";

export type LearningOpsCronEnv = {
  DATABASE_URL?: string;
  LEARNINGOPS_CRON_ENABLED?: string;
};

export async function runScheduledLearningOps({
  env,
  now = new Date(),
}: {
  env: LearningOpsCronEnv;
  now?: Date;
}): Promise<LearningOpsPipelineResult | null> {
  if (env.LEARNINGOPS_CRON_ENABLED === "false") {
    console.log("learningops_cron_skipped enabled=false");
    return null;
  }

  if (!env.DATABASE_URL) {
    throw new Error("DATABASE_URL is required for scheduled LearningOps.");
  }

  const result = await runLearningOpsPipeline({
    sql: neon(env.DATABASE_URL) as LearningOpsSqlClient,
    now,
    digestPath: null,
  });

  console.log(
    [
      `learningops_cron_run=${result.runId}`,
      `status=${result.status}`,
      `samples=${result.analysis.summary.sampleCount}`,
      `findings=${result.analysis.findings.length}`,
      `candidates=${result.analysis.strategyCandidates.length}`,
    ].join(" "),
  );

  return result;
}
