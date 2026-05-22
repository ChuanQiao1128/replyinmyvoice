import { getSql } from "../db";

export type RewriteCanaryRollbackSqlClient = (
  strings: TemplateStringsArray,
  ...values: unknown[]
) => Promise<unknown>;

export type RewriteCanaryRegression = {
  canaryStrategyVersion: string;
  controlStrategyVersion: string;
  scenario: string;
  windowRewrites: number;
  rollingAverageSignalDrop: number;
  baselineAverageSignalDrop: number;
  regressionPoints: number;
  windowStartedAt: Date | null;
  windowEndedAt: Date | null;
};

export type RewriteCanaryRollbackSummary = RewriteCanaryRegression & {
  id: string;
  state: string;
  adminEmailStatus: string;
  githubIssueStatus: string;
  githubIssueUrl: string | null;
  createdAt: Date;
  resolvedAt: Date | null;
};

export type CanaryRollbackAdminEmail = RewriteCanaryRegression & {
  to: string;
  rollbackId: string;
  subject: string;
  text: string;
};

export type CanaryRollbackGitHubIssue = RewriteCanaryRegression & {
  repo: string;
  rollbackId: string;
  title: string;
  body: string;
  labels: string[];
};

export type OutboundResult = {
  status: "sent" | "opened" | "skipped" | "failed";
  issueUrl?: string | null;
  reason?: string;
};

export type RewriteCanaryRollbackMonitorResult = {
  triggered: boolean;
  reason: string;
  rollbackId: string | null;
  canaryStrategyVersion: string;
  scenario: string | null;
  adminEmailStatus: "pending" | "sent" | "skipped" | "failed";
  githubIssueStatus: "pending" | "opened" | "skipped" | "failed";
  githubIssueUrl: string | null;
  errorMessage: string | null;
};

type EnvMap = Record<string, string | undefined>;

const DEFAULT_CONTROL_STRATEGY_VERSION = "adaptive_rewrite_orchestrator";
const DEFAULT_ROLLBACK_WINDOW = 50;
const DEFAULT_REGRESSION_THRESHOLD_POINTS = 3;

function envValue(env: EnvMap, name: string, fallback = "") {
  const value = env[name];
  return value === undefined || value === "" ? fallback : value;
}

function booleanEnv(env: EnvMap, name: string, fallback = false) {
  const value = envValue(env, name, fallback ? "true" : "false")
    .trim()
    .toLowerCase();
  return ["1", "true", "yes", "on"].includes(value);
}

function numericEnv({
  env,
  fallback,
  min = 0,
  name,
}: {
  env: EnvMap;
  fallback: number;
  min?: number;
  name: string;
}) {
  const rawValue = Number(envValue(env, name, String(fallback)));
  const numeric = Number.isFinite(rawValue) ? rawValue : fallback;
  return Math.max(numeric, min);
}

function numberValue(value: unknown) {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : 0;
}

function nullableDate(value: unknown) {
  if (!value) {
    return null;
  }
  const date = value instanceof Date ? value : new Date(String(value));
  return Number.isNaN(date.getTime()) ? null : date;
}

function requiredDate(value: unknown) {
  return nullableDate(value) ?? new Date(0);
}

function firstEmail(value: string | undefined) {
  return (value ?? "")
    .split(",")
    .map((item) => item.trim())
    .find((item) => item.length > 0) ?? "";
}

function safeErrorMessage(error: unknown) {
  return (error instanceof Error ? error.message : "Unknown error").slice(0, 400);
}

function mapRegressionRow(row: Record<string, unknown>): RewriteCanaryRegression {
  return {
    canaryStrategyVersion: String(row.canaryStrategyVersion),
    controlStrategyVersion: String(row.controlStrategyVersion),
    scenario: String(row.scenario),
    windowRewrites: numberValue(row.windowRewrites),
    rollingAverageSignalDrop: numberValue(row.rollingAverageSignalDrop),
    baselineAverageSignalDrop: numberValue(row.baselineAverageSignalDrop),
    regressionPoints: numberValue(row.regressionPoints),
    windowStartedAt: nullableDate(row.windowStartedAt),
    windowEndedAt: nullableDate(row.windowEndedAt),
  };
}

function mapRollbackRow(row: Record<string, unknown>): RewriteCanaryRollbackSummary {
  return {
    ...mapRegressionRow(row),
    id: String(row.id),
    state: String(row.state ?? "open"),
    adminEmailStatus: String(row.adminEmailStatus ?? "pending"),
    githubIssueStatus: String(row.githubIssueStatus ?? "pending"),
    githubIssueUrl: row.githubIssueUrl ? String(row.githubIssueUrl) : null,
    createdAt: requiredDate(row.createdAt),
    resolvedAt: nullableDate(row.resolvedAt),
  };
}

function blankMonitorResult(
  reason: string,
  canaryStrategyVersion = "",
): RewriteCanaryRollbackMonitorResult {
  return {
    triggered: false,
    reason,
    rollbackId: null,
    canaryStrategyVersion,
    scenario: null,
    adminEmailStatus: "skipped",
    githubIssueStatus: "skipped",
    githubIssueUrl: null,
    errorMessage: null,
  };
}

function buildAdminEmail({
  regression,
  rollbackId,
  to,
}: {
  regression: RewriteCanaryRegression;
  rollbackId: string;
  to: string;
}): CanaryRollbackAdminEmail {
  const subject = `[Reply In My Voice] Canary rollback: ${regression.canaryStrategyVersion}`;
  const text = [
    `Rewrite strategy canary rollback was triggered for ${regression.canaryStrategyVersion}.`,
    "",
    `Scenario: ${regression.scenario}`,
    `Regression: ${regression.regressionPoints.toFixed(1)} signal-drop points`,
    `Canary rolling average: ${regression.rollingAverageSignalDrop.toFixed(1)}`,
    `Control baseline average: ${regression.baselineAverageSignalDrop.toFixed(1)}`,
    `Window rewrites: ${regression.windowRewrites}`,
    `Rollback id: ${rollbackId}`,
    "",
    "The persisted rollback state forces canary traffic to 0 until this row is resolved.",
  ].join("\n");

  return {
    ...regression,
    rollbackId,
    subject,
    text,
    to,
  };
}

function buildGitHubIssue({
  regression,
  repo,
  rollbackId,
}: {
  regression: RewriteCanaryRegression;
  repo: string;
  rollbackId: string;
}): CanaryRollbackGitHubIssue {
  const title = `Investigate rewrite strategy rollback for ${regression.scenario}`;
  const body = [
    "A promoted rewrite strategy canary was automatically rolled back.",
    "",
    `- Rollback id: ${rollbackId}`,
    `- Canary strategy: ${regression.canaryStrategyVersion}`,
    `- Control strategy: ${regression.controlStrategyVersion}`,
    `- Scenario: ${regression.scenario}`,
    `- 50-rewrite rolling average signal drop: ${regression.rollingAverageSignalDrop.toFixed(1)}`,
    `- Control baseline signal drop: ${regression.baselineAverageSignalDrop.toFixed(1)}`,
    `- Regression: ${regression.regressionPoints.toFixed(1)} points`,
    "",
    "Follow up by reviewing the affected scenario, adding or updating regression coverage, and resolving the rollback row only after a fixed strategy is ready for canary traffic.",
  ].join("\n");

  return {
    ...regression,
    rollbackId,
    repo,
    title,
    body,
    labels: ["learningops", "rewrite-quality", "canary-rollback"],
  };
}

export async function getActiveRewriteCanaryRollback({
  canaryStrategyVersion,
  sql = getSql() as RewriteCanaryRollbackSqlClient,
}: {
  canaryStrategyVersion: string;
  sql?: RewriteCanaryRollbackSqlClient;
}): Promise<RewriteCanaryRollbackSummary | null> {
  if (!canaryStrategyVersion) {
    return null;
  }

  try {
    const rows = (await sql`
      SELECT
        "id",
        "canaryStrategyVersion",
        "controlStrategyVersion",
        "scenario",
        "state",
        "windowRewrites",
        "rollingAverageSignalDrop",
        "baselineAverageSignalDrop",
        "regressionPoints",
        "windowStartedAt",
        "windowEndedAt",
        "adminEmailStatus",
        "githubIssueStatus",
        "githubIssueUrl",
        "createdAt",
        "resolvedAt"
      FROM "RewriteCanaryRollback"
      WHERE "canaryStrategyVersion" = ${canaryStrategyVersion}
        AND "resolvedAt" IS NULL
      ORDER BY "createdAt" DESC
      LIMIT 1
    `) as Array<Record<string, unknown>>;

    return rows[0] ? mapRollbackRow(rows[0]) : null;
  } catch (error) {
    console.warn("rewrite_canary_rollback_read_failed", {
      message: safeErrorMessage(error),
    });
    return null;
  }
}

export async function findRewriteCanaryRegressions({
  canaryStrategyVersion,
  controlStrategyVersion,
  lookbackHours = 168,
  minWindowRewrites = DEFAULT_ROLLBACK_WINDOW,
  regressionThresholdPoints = DEFAULT_REGRESSION_THRESHOLD_POINTS,
  now = new Date(),
  sql = getSql() as RewriteCanaryRollbackSqlClient,
}: {
  canaryStrategyVersion: string;
  controlStrategyVersion: string;
  lookbackHours?: number;
  minWindowRewrites?: number;
  regressionThresholdPoints?: number;
  now?: Date;
  sql?: RewriteCanaryRollbackSqlClient;
}): Promise<RewriteCanaryRegression[]> {
  const lookbackStart = new Date(now.getTime() - lookbackHours * 3_600_000);
  const rows = (await sql`
    WITH ranked_canary AS (
      SELECT
        "scenario",
        (-"changePoints")::float AS "signalDrop",
        "createdAt",
        ROW_NUMBER() OVER (
          PARTITION BY "scenario"
          ORDER BY "createdAt" DESC
        ) AS rn
      FROM "RewriteCostLog"
      WHERE "strategyVersion" = ${canaryStrategyVersion}
        AND "status" = 'success'
        AND "changePoints" IS NOT NULL
        AND "createdAt" >= ${lookbackStart}
    ),
    canary_window AS (
      SELECT
        "scenario",
        COUNT(*)::int AS "windowRewrites",
        AVG("signalDrop")::float AS "rollingAverageSignalDrop",
        MIN("createdAt") AS "windowStartedAt",
        MAX("createdAt") AS "windowEndedAt"
      FROM ranked_canary
      WHERE rn <= ${minWindowRewrites}
      GROUP BY "scenario"
      HAVING COUNT(*) >= ${minWindowRewrites}
    ),
    control_summary AS (
      SELECT
        "scenario",
        AVG(-"changePoints")::float AS "baselineAverageSignalDrop"
      FROM "RewriteCostLog"
      WHERE "strategyVersion" = ${controlStrategyVersion}
        AND "status" = 'success'
        AND "changePoints" IS NOT NULL
        AND "createdAt" >= ${lookbackStart}
      GROUP BY "scenario"
    )
    SELECT
      ${canaryStrategyVersion} AS "canaryStrategyVersion",
      ${controlStrategyVersion} AS "controlStrategyVersion",
      c."scenario",
      c."windowRewrites",
      c."rollingAverageSignalDrop",
      s."baselineAverageSignalDrop",
      (s."baselineAverageSignalDrop" - c."rollingAverageSignalDrop")::float AS "regressionPoints",
      c."windowStartedAt",
      c."windowEndedAt"
    FROM canary_window c
    JOIN control_summary s ON s."scenario" = c."scenario"
    LEFT JOIN "RewriteCanaryRollback" active
      ON active."canaryStrategyVersion" = ${canaryStrategyVersion}
      AND active."scenario" = c."scenario"
      AND active."resolvedAt" IS NULL
    WHERE active."id" IS NULL
      AND s."baselineAverageSignalDrop" - c."rollingAverageSignalDrop" >= ${regressionThresholdPoints}
    ORDER BY "regressionPoints" DESC
  `) as Array<Record<string, unknown>>;

  return rows.map(mapRegressionRow);
}

async function recordRewriteCanaryRollback({
  now,
  regression,
  sql,
}: {
  now: Date;
  regression: RewriteCanaryRegression;
  sql: RewriteCanaryRollbackSqlClient;
}) {
  const rows = (await sql`
    INSERT INTO "RewriteCanaryRollback" (
      "id",
      "canaryStrategyVersion",
      "controlStrategyVersion",
      "scenario",
      "state",
      "windowRewrites",
      "rollingAverageSignalDrop",
      "baselineAverageSignalDrop",
      "regressionPoints",
      "windowStartedAt",
      "windowEndedAt",
      "adminEmailStatus",
      "githubIssueStatus",
      "createdAt",
      "lastCheckedAt"
    )
    VALUES (
      ${crypto.randomUUID()},
      ${regression.canaryStrategyVersion},
      ${regression.controlStrategyVersion},
      ${regression.scenario},
      'open',
      ${regression.windowRewrites},
      ${regression.rollingAverageSignalDrop},
      ${regression.baselineAverageSignalDrop},
      ${regression.regressionPoints},
      ${regression.windowStartedAt},
      ${regression.windowEndedAt},
      'pending',
      'pending',
      ${now},
      ${now}
    )
    ON CONFLICT ("canaryStrategyVersion", "scenario")
      WHERE "resolvedAt" IS NULL
    DO UPDATE SET
      "windowRewrites" = EXCLUDED."windowRewrites",
      "rollingAverageSignalDrop" = EXCLUDED."rollingAverageSignalDrop",
      "baselineAverageSignalDrop" = EXCLUDED."baselineAverageSignalDrop",
      "regressionPoints" = EXCLUDED."regressionPoints",
      "windowStartedAt" = EXCLUDED."windowStartedAt",
      "windowEndedAt" = EXCLUDED."windowEndedAt",
      "lastCheckedAt" = EXCLUDED."lastCheckedAt"
    RETURNING "id"
  `) as Array<Record<string, unknown>>;

  return String(rows[0]?.id ?? "");
}

async function updateRollbackSideEffects({
  adminEmailStatus,
  errorMessage,
  githubIssueStatus,
  githubIssueUrl,
  now,
  rollbackId,
  sql,
}: {
  adminEmailStatus: RewriteCanaryRollbackMonitorResult["adminEmailStatus"];
  errorMessage: string | null;
  githubIssueStatus: RewriteCanaryRollbackMonitorResult["githubIssueStatus"];
  githubIssueUrl: string | null;
  now: Date;
  rollbackId: string;
  sql: RewriteCanaryRollbackSqlClient;
}) {
  await sql`
    UPDATE "RewriteCanaryRollback"
    SET
      "adminEmailStatus" = ${adminEmailStatus},
      "adminEmailSentAt" = CASE
        WHEN ${adminEmailStatus} = 'sent' THEN ${now}
        ELSE "adminEmailSentAt"
      END,
      "githubIssueStatus" = ${githubIssueStatus},
      "githubIssueUrl" = ${githubIssueUrl},
      "githubIssueOpenedAt" = CASE
        WHEN ${githubIssueStatus} = 'opened' THEN ${now}
        ELSE "githubIssueOpenedAt"
      END,
      "lastError" = ${errorMessage},
      "lastCheckedAt" = ${now}
    WHERE "id" = ${rollbackId}
  `;
}

export async function sendCanaryRollbackAdminEmail(
  message: CanaryRollbackAdminEmail,
  env: EnvMap = process.env,
): Promise<OutboundResult> {
  const apiKey = envValue(env, "RESEND_API_KEY").trim();
  const from = envValue(env, "LEARNINGOPS_ALERT_EMAIL_FROM").trim();
  if (!apiKey || !from || !message.to) {
    return { status: "skipped", reason: "email_not_configured" };
  }

  const response = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      from,
      to: [message.to],
      subject: message.subject,
      text: message.text,
    }),
  });

  if (!response.ok) {
    throw new Error(`Email provider returned ${response.status}`);
  }

  return { status: "sent" };
}

export async function openCanaryRollbackGitHubIssue(
  issue: CanaryRollbackGitHubIssue,
  env: EnvMap = process.env,
): Promise<OutboundResult> {
  const token = envValue(env, "LEARNINGOPS_GITHUB_TOKEN").trim();
  if (!token || !issue.repo) {
    return { status: "skipped", reason: "github_not_configured" };
  }

  const response = await fetch(`https://api.github.com/repos/${issue.repo}/issues`, {
    method: "POST",
    headers: {
      Accept: "application/vnd.github+json",
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
      "X-GitHub-Api-Version": "2022-11-28",
    },
    body: JSON.stringify({
      title: issue.title,
      body: issue.body,
      labels: issue.labels,
    }),
  });

  if (!response.ok) {
    throw new Error(`GitHub issue API returned ${response.status}`);
  }

  const json = await response.json() as { html_url?: unknown };
  return {
    status: "opened",
    issueUrl: typeof json.html_url === "string" ? json.html_url : null,
  };
}

export async function runRewriteCanaryRollbackMonitor({
  env = process.env,
  now = new Date(),
  openGitHubIssue,
  sendAdminEmail,
  sql = getSql() as RewriteCanaryRollbackSqlClient,
}: {
  env?: EnvMap;
  now?: Date;
  openGitHubIssue?: (issue: CanaryRollbackGitHubIssue) => Promise<OutboundResult>;
  sendAdminEmail?: (message: CanaryRollbackAdminEmail) => Promise<OutboundResult>;
  sql?: RewriteCanaryRollbackSqlClient;
} = {}): Promise<RewriteCanaryRollbackMonitorResult> {
  const canaryStrategyVersion = envValue(env, "REWRITE_STRATEGY_CANARY_VERSION").trim();
  const controlStrategyVersion = envValue(
    env,
    "REWRITE_STRATEGY_CONTROL_VERSION",
    DEFAULT_CONTROL_STRATEGY_VERSION,
  ).trim();
  const enabled = booleanEnv(env, "REWRITE_STRATEGY_CANARY_ENABLED") &&
    canaryStrategyVersion.length > 0 &&
    canaryStrategyVersion !== controlStrategyVersion;

  if (!enabled) {
    return blankMonitorResult(
      "Canary flag is off or no canary strategy version is configured.",
      canaryStrategyVersion,
    );
  }

  const minWindowRewrites = Math.round(
    numericEnv({
      env,
      name: "REWRITE_STRATEGY_CANARY_ROLLBACK_WINDOW",
      fallback: DEFAULT_ROLLBACK_WINDOW,
      min: 1,
    }),
  );
  const regressionThresholdPoints = numericEnv({
    env,
    name: "REWRITE_STRATEGY_CANARY_ROLLBACK_THRESHOLD_POINTS",
    fallback: DEFAULT_REGRESSION_THRESHOLD_POINTS,
    min: 0,
  });
  const lookbackHours = numericEnv({
    env,
    name: "REWRITE_STRATEGY_CANARY_LOOKBACK_HOURS",
    fallback: 168,
    min: 1,
  });

  let regressions: RewriteCanaryRegression[];
  try {
    regressions = await findRewriteCanaryRegressions({
      sql,
      canaryStrategyVersion,
      controlStrategyVersion,
      lookbackHours,
      minWindowRewrites,
      regressionThresholdPoints,
      now,
    });
  } catch (error) {
    return {
      ...blankMonitorResult("Canary rollback monitor failed.", canaryStrategyVersion),
      errorMessage: safeErrorMessage(error),
    };
  }

  const regression = regressions[0];
  if (!regression) {
    return blankMonitorResult(
      "No scenario crossed the canary rollback threshold.",
      canaryStrategyVersion,
    );
  }

  let rollbackId: string;
  try {
    rollbackId = await recordRewriteCanaryRollback({ sql, now, regression });
  } catch (error) {
    return {
      ...blankMonitorResult("Canary rollback could not be persisted.", canaryStrategyVersion),
      errorMessage: safeErrorMessage(error),
      scenario: regression.scenario,
    };
  }

  const emailTo = envValue(
    env,
    "LEARNINGOPS_ALERT_EMAIL_TO",
    firstEmail(env.ADMIN_EMAILS),
  ).trim();
  let adminEmailStatus: RewriteCanaryRollbackMonitorResult["adminEmailStatus"] =
    "pending";
  let githubIssueStatus: RewriteCanaryRollbackMonitorResult["githubIssueStatus"] =
    "pending";
  let githubIssueUrl: string | null = null;
  const errors: string[] = [];

  try {
    const result = await (sendAdminEmail ??
      ((message) => sendCanaryRollbackAdminEmail(message, env)))(
        buildAdminEmail({ regression, rollbackId, to: emailTo }),
      );
    adminEmailStatus = result.status === "sent" ? "sent" : "skipped";
  } catch (error) {
    adminEmailStatus = "failed";
    errors.push(safeErrorMessage(error));
  }

  try {
    const repo = envValue(
      env,
      "LEARNINGOPS_GITHUB_REPO",
      "ChuanQiao1128/replyinmyvoice",
    ).trim();
    const result = await (openGitHubIssue ??
      ((issue) => openCanaryRollbackGitHubIssue(issue, env)))(
        buildGitHubIssue({ regression, repo, rollbackId }),
      );
    githubIssueStatus = result.status === "opened" ? "opened" : "skipped";
    githubIssueUrl = result.issueUrl ?? null;
  } catch (error) {
    githubIssueStatus = "failed";
    errors.push(safeErrorMessage(error));
  }

  const errorMessage = errors.length > 0 ? errors.join("; ") : null;
  await updateRollbackSideEffects({
    adminEmailStatus,
    errorMessage,
    githubIssueStatus,
    githubIssueUrl,
    now,
    rollbackId,
    sql,
  });

  return {
    triggered: true,
    reason: `Canary rolled back for ${regression.scenario}; regression ${regression.regressionPoints.toFixed(1)} points over ${regression.windowRewrites} rewrites.`,
    rollbackId,
    canaryStrategyVersion,
    scenario: regression.scenario,
    adminEmailStatus,
    githubIssueStatus,
    githubIssueUrl,
    errorMessage,
  };
}
