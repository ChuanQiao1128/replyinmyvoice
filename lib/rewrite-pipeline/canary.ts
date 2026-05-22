import { getSql } from "../db";

export type RewriteCanaryState =
  | "off"
  | "monitoring"
  | "paused"
  | "ramping"
  | "complete";

export type RewriteCanaryConfig = {
  enabled: boolean;
  controlStrategyVersion: string;
  canaryStrategyVersion: string;
  initialPercent: number;
  rampPercents: number[];
  minMeasuredRewrites: number;
  minWindowHours: number;
  minDeltaPoints: number;
  lookbackHours: number;
};

export type RewriteCanarySignalSummary = {
  strategyVersion: string;
  requestCount: number;
  successCount: number;
  measuredCount: number;
  averageSignalDrop: number | null;
  p10SignalDrop: number | null;
  p50SignalDrop: number | null;
  p90SignalDrop: number | null;
  below50Rate: number | null;
  firstSeenAt: Date | null;
  lastSeenAt: Date | null;
};

export type RewriteCanaryDecision = {
  state: RewriteCanaryState;
  effectivePercent: number;
  controlStrategyVersion: string;
  canaryStrategyVersion: string;
  reason: string;
  control: RewriteCanarySignalSummary | null;
  canary: RewriteCanarySignalSummary | null;
};

export type RewriteCanaryAssignment = {
  bucket: "control" | "canary";
  strategyVersion: string;
  bucketValue: number;
  effectivePercent: number;
  reason: string;
};

export type RewriteCanarySqlClient = ReturnType<typeof getSql>;

type EnvMap = Record<string, string | undefined>;

const DEFAULT_CONTROL_STRATEGY_VERSION = "adaptive_rewrite_orchestrator";
const DEFAULT_RAMP_PERCENTS = [10, 25, 50, 100];

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
  max = Number.POSITIVE_INFINITY,
  min = 0,
  name,
}: {
  env: EnvMap;
  fallback: number;
  max?: number;
  min?: number;
  name: string;
}) {
  const rawValue = Number(envValue(env, name, String(fallback)));
  const numeric = Number.isFinite(rawValue) ? rawValue : fallback;
  return Math.min(Math.max(numeric, min), max);
}

function parseRampPercents(rawValue: string, initialPercent: number) {
  const parsed = rawValue
    .split(",")
    .map((value) => Number(value.trim()))
    .filter((value) => Number.isFinite(value) && value >= 0 && value <= 100);
  const values = parsed.length > 0 ? parsed : DEFAULT_RAMP_PERCENTS;
  const normalized = new Set([...values, initialPercent].map(Math.round));
  return [...normalized].sort((left, right) => left - right);
}

function numberValue(value: unknown) {
  if (value === null || value === undefined) {
    return null;
  }
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : null;
}

function requiredNumber(value: unknown) {
  return numberValue(value) ?? 0;
}

function dateValue(value: unknown) {
  if (!value) {
    return null;
  }
  return value instanceof Date ? value : new Date(String(value));
}

function hoursBetween(start: Date | null, end: Date) {
  if (!start) {
    return 0;
  }
  return Math.max(0, (end.getTime() - start.getTime()) / 3_600_000);
}

function nextRampPercent({
  config,
  canary,
  mature,
}: {
  config: RewriteCanaryConfig;
  canary: RewriteCanarySignalSummary;
  mature: boolean;
}) {
  if (!mature) {
    return config.initialPercent;
  }

  const initialIndex = Math.max(
    0,
    config.rampPercents.findIndex(
      (percent) => percent >= config.initialPercent,
    ),
  );
  const countWindows = Math.floor(
    canary.measuredCount / config.minMeasuredRewrites,
  );
  const completedWindows = Math.max(1, countWindows);
  const nextIndex = Math.min(
    initialIndex + completedWindows,
    config.rampPercents.length - 1,
  );
  return config.rampPercents[nextIndex] ?? config.initialPercent;
}

function stableBucketValue(key: string) {
  let hash = 2_166_136_261;
  for (let index = 0; index < key.length; index += 1) {
    hash ^= key.charCodeAt(index);
    hash = Math.imul(hash, 16_777_619);
  }
  return ((hash >>> 0) % 10_000) / 100;
}

export function getRewriteCanaryConfig(
  env: EnvMap = process.env,
  options: { controlStrategyVersion?: string } = {},
): RewriteCanaryConfig {
  const controlStrategyVersion = envValue(
    env,
    "REWRITE_STRATEGY_CONTROL_VERSION",
    options.controlStrategyVersion ?? DEFAULT_CONTROL_STRATEGY_VERSION,
  ).trim();
  const canaryStrategyVersion = envValue(
    env,
    "REWRITE_STRATEGY_CANARY_VERSION",
  ).trim();
  const initialPercent = Math.round(
    numericEnv({
      env,
      name: "REWRITE_STRATEGY_CANARY_PERCENT",
      fallback: 10,
      max: 100,
    }),
  );
  const rampPercents = parseRampPercents(
    envValue(env, "REWRITE_STRATEGY_CANARY_RAMP_PERCENTS", "10,25,50,100"),
    initialPercent,
  );
  const enabled = booleanEnv(env, "REWRITE_STRATEGY_CANARY_ENABLED") &&
    canaryStrategyVersion.length > 0 &&
    canaryStrategyVersion !== controlStrategyVersion;

  return {
    enabled,
    controlStrategyVersion,
    canaryStrategyVersion,
    initialPercent,
    rampPercents,
    minMeasuredRewrites: Math.round(
      numericEnv({
        env,
        name: "REWRITE_STRATEGY_CANARY_MIN_REWRITES",
        fallback: 200,
        min: 1,
      }),
    ),
    minWindowHours: numericEnv({
      env,
      name: "REWRITE_STRATEGY_CANARY_MIN_HOURS",
      fallback: 24,
      min: 0,
    }),
    minDeltaPoints: numericEnv({
      env,
      name: "REWRITE_STRATEGY_CANARY_MIN_DELTA_POINTS",
      fallback: 0,
      min: 0,
    }),
    lookbackHours: numericEnv({
      env,
      name: "REWRITE_STRATEGY_CANARY_LOOKBACK_HOURS",
      fallback: 168,
      min: 1,
    }),
  };
}

export function evaluateRewriteCanary({
  canary,
  config,
  control,
  now = new Date(),
}: {
  canary: RewriteCanarySignalSummary | null;
  config: RewriteCanaryConfig;
  control: RewriteCanarySignalSummary | null;
  now?: Date;
}): RewriteCanaryDecision {
  if (!config.enabled) {
    return {
      state: "off",
      effectivePercent: 0,
      controlStrategyVersion: config.controlStrategyVersion,
      canaryStrategyVersion: config.canaryStrategyVersion,
      reason: "Canary flag is off or no canary strategy version is configured.",
      control,
      canary,
    };
  }

  if (!canary || canary.measuredCount === 0) {
    return {
      state: "monitoring",
      effectivePercent: config.initialPercent,
      controlStrategyVersion: config.controlStrategyVersion,
      canaryStrategyVersion: config.canaryStrategyVersion,
      reason: "Collecting first canary signal-change samples.",
      control,
      canary,
    };
  }

  const ageHours = hoursBetween(canary.firstSeenAt, now);
  const mature = ageHours >= config.minWindowHours ||
    canary.measuredCount >= config.minMeasuredRewrites;
  if (!mature) {
    return {
      state: "monitoring",
      effectivePercent: config.initialPercent,
      controlStrategyVersion: config.controlStrategyVersion,
      canaryStrategyVersion: config.canaryStrategyVersion,
      reason: `Waiting for ${config.minWindowHours}h or ${config.minMeasuredRewrites} measured canary rewrites.`,
      control,
      canary,
    };
  }

  if (
    !control ||
    control.measuredCount === 0 ||
    control.averageSignalDrop === null ||
    canary.averageSignalDrop === null
  ) {
    return {
      state: "monitoring",
      effectivePercent: config.initialPercent,
      controlStrategyVersion: config.controlStrategyVersion,
      canaryStrategyVersion: config.canaryStrategyVersion,
      reason: "Waiting for comparable control signal-change samples.",
      control,
      canary,
    };
  }

  const delta = canary.averageSignalDrop - control.averageSignalDrop;
  if (delta < -config.minDeltaPoints) {
    return {
      state: "paused",
      effectivePercent: 0,
      controlStrategyVersion: config.controlStrategyVersion,
      canaryStrategyVersion: config.canaryStrategyVersion,
      reason: `Canary has lower average signal drop by ${Math.abs(delta).toFixed(1)} points.`,
      control,
      canary,
    };
  }

  if (delta > config.minDeltaPoints) {
    const effectivePercent = nextRampPercent({ config, canary, mature });
    return {
      state: effectivePercent >= 100 ? "complete" : "ramping",
      effectivePercent,
      controlStrategyVersion: config.controlStrategyVersion,
      canaryStrategyVersion: config.canaryStrategyVersion,
      reason: `Canary average signal drop is higher by ${delta.toFixed(1)} points.`,
      control,
      canary,
    };
  }

  return {
    state: "monitoring",
    effectivePercent: config.initialPercent,
    controlStrategyVersion: config.controlStrategyVersion,
    canaryStrategyVersion: config.canaryStrategyVersion,
    reason: "Canary and control signal-change averages are tied within the configured margin.",
    control,
    canary,
  };
}

export function selectRewriteCanaryAssignment({
  config,
  decision,
  stickinessKey,
}: {
  config: RewriteCanaryConfig;
  decision: RewriteCanaryDecision;
  stickinessKey: string;
}): RewriteCanaryAssignment {
  const effectivePercent = config.enabled ? decision.effectivePercent : 0;
  const bucketValue = stableBucketValue(stickinessKey);
  const useCanary = effectivePercent >= 100 ||
    (effectivePercent > 0 && bucketValue < effectivePercent);

  return {
    bucket: useCanary ? "canary" : "control",
    strategyVersion: useCanary
      ? config.canaryStrategyVersion
      : config.controlStrategyVersion,
    bucketValue,
    effectivePercent,
    reason: decision.reason,
  };
}

function mapSummaryRow(row: Record<string, unknown>): RewriteCanarySignalSummary {
  return {
    strategyVersion: String(row.strategyVersion),
    requestCount: requiredNumber(row.requestCount),
    successCount: requiredNumber(row.successCount),
    measuredCount: requiredNumber(row.measuredCount),
    averageSignalDrop: numberValue(row.averageSignalDrop),
    p10SignalDrop: numberValue(row.p10SignalDrop),
    p50SignalDrop: numberValue(row.p50SignalDrop),
    p90SignalDrop: numberValue(row.p90SignalDrop),
    below50Rate: numberValue(row.below50Rate),
    firstSeenAt: dateValue(row.firstSeenAt),
    lastSeenAt: dateValue(row.lastSeenAt),
  };
}

export async function getRewriteCanaryDecision({
  config = getRewriteCanaryConfig(),
  now = new Date(),
  sql,
}: {
  config?: RewriteCanaryConfig;
  now?: Date;
  sql?: RewriteCanarySqlClient;
} = {}): Promise<RewriteCanaryDecision> {
  if (!config.enabled) {
    return evaluateRewriteCanary({
      canary: null,
      config,
      control: null,
      now,
    });
  }

  const lookbackStart = new Date(
    now.getTime() - config.lookbackHours * 3_600_000,
  );
  const sqlClient = sql ?? getSql();

  try {
    const rows = (await sqlClient`
      SELECT
        "strategyVersion",
        COUNT(*)::int AS "requestCount",
        COUNT(*) FILTER (WHERE "status" = 'success')::int AS "successCount",
        COUNT(*) FILTER (WHERE "changePoints" IS NOT NULL)::int AS "measuredCount",
        AVG(-"changePoints") FILTER (WHERE "changePoints" IS NOT NULL)::float AS "averageSignalDrop",
        percentile_cont(0.1) WITHIN GROUP (ORDER BY -"changePoints") FILTER (WHERE "changePoints" IS NOT NULL)::float AS "p10SignalDrop",
        percentile_cont(0.5) WITHIN GROUP (ORDER BY -"changePoints") FILTER (WHERE "changePoints" IS NOT NULL)::float AS "p50SignalDrop",
        percentile_cont(0.9) WITHIN GROUP (ORDER BY -"changePoints") FILTER (WHERE "changePoints" IS NOT NULL)::float AS "p90SignalDrop",
        AVG(CASE WHEN "rewriteAiLikePercent" < 50 THEN 1 ELSE 0 END) FILTER (WHERE "rewriteAiLikePercent" IS NOT NULL)::float AS "below50Rate",
        MIN("createdAt") AS "firstSeenAt",
        MAX("createdAt") AS "lastSeenAt"
      FROM "RewriteCostLog"
      WHERE "createdAt" >= ${lookbackStart}
        AND "strategyVersion" IN (${config.controlStrategyVersion}, ${config.canaryStrategyVersion})
      GROUP BY "strategyVersion"
    `) as Array<Record<string, unknown>>;
    const summaries = new Map(
      rows.map((row) => {
        const mapped = mapSummaryRow(row);
        return [mapped.strategyVersion, mapped];
      }),
    );

    return evaluateRewriteCanary({
      canary: summaries.get(config.canaryStrategyVersion) ?? null,
      config,
      control: summaries.get(config.controlStrategyVersion) ?? null,
      now,
    });
  } catch (error) {
    console.warn("rewrite_canary_decision_failed", {
      message: error instanceof Error ? error.message.slice(0, 180) : "Unknown",
    });
    return evaluateRewriteCanary({
      canary: null,
      config,
      control: null,
      now,
    });
  }
}
