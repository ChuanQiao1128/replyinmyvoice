import { neon } from "@neondatabase/serverless";

import {
  runLearningOpsPipeline,
  type LearningOpsPipelineResult,
  type LearningOpsSqlClient,
} from "./run";
import { runRewriteCanaryRollbackMonitor } from "../rewrite-pipeline/canary-rollback";

export type LearningOpsCronEnv = {
  DATABASE_URL?: string;
  LEARNINGOPS_CRON_ENABLED?: string;
  LEARNINGOPS_ALERT_EMAIL_FROM?: string;
  LEARNINGOPS_ALERT_EMAIL_TO?: string;
  LEARNINGOPS_GITHUB_REPO?: string;
  LEARNINGOPS_GITHUB_TOKEN?: string;
  RESEND_API_KEY?: string;
  REWRITE_STRATEGY_CANARY_ENABLED?: string;
  REWRITE_STRATEGY_CANARY_LOOKBACK_HOURS?: string;
  REWRITE_STRATEGY_CANARY_ROLLBACK_THRESHOLD_POINTS?: string;
  REWRITE_STRATEGY_CANARY_ROLLBACK_WINDOW?: string;
  REWRITE_STRATEGY_CANARY_VERSION?: string;
  REWRITE_STRATEGY_CONTROL_VERSION?: string;
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

  const sql = neon(env.DATABASE_URL) as LearningOpsSqlClient;
  const result = await runLearningOpsPipeline({
    sql,
    now,
    digestPath: null,
  });
  const rollback = await runRewriteCanaryRollbackMonitor({
    sql,
    env,
    now,
  });

  console.log(
    [
      `learningops_cron_run=${result.runId}`,
      `status=${result.status}`,
      `samples=${result.analysis.summary.sampleCount}`,
      `findings=${result.analysis.findings.length}`,
      `candidates=${result.analysis.strategyCandidates.length}`,
      `canary_rollback=${rollback.triggered ? "triggered" : "none"}`,
    ].join(" "),
  );

  return result;
}
