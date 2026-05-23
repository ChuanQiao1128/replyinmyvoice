import type { RewriteCredit, RewriteUsage, User } from "./generated/prisma/client";

import { createId, getSql, nullableDate, requiredDate } from "./db";

export const PLAN_ALLOWANCES = {
  free: 3,
  starter: 55,
  pro: 110,
} as const;

export type PlanTier = keyof typeof PLAN_ALLOWANCES;

export const FREE_REWRITE_LIMIT = PLAN_ALLOWANCES.free;
export const PAID_REWRITE_LIMIT = PLAN_ALLOWANCES.starter;
export const TESTING_REWRITE_LIMIT = 10_000;

const ACTIVE_STATUSES = new Set(["active", "trialing", "testing"]);
const PLAN_TIERS = new Set<string>(["free", "starter", "pro"]);

export class QuotaExceededError extends Error {
  constructor() {
    super("Rewrite quota exhausted");
    this.name = "QuotaExceededError";
  }
}

export type UsageSubject = Pick<
  User,
  | "id"
  | "subscriptionStatus"
  | "stripeSubscriptionId"
  | "currentPeriodEnd"
> &
  Partial<Pick<User, "planTier">>;

export type UsagePlan = {
  allowed: boolean;
  scope: "free" | "paid";
  planTier: PlanTier | "testing";
  quota: number;
  periodKey: string;
  periodStart: Date | null;
  periodEnd: Date | null;
};

export type UsageStatus = UsagePlan & {
  used: number;
  planRemaining: number;
  creditRemaining: number;
  remaining: number;
  exhausted: boolean;
};

type RewriteUsageRow = Omit<
  RewriteUsage,
  "periodStart" | "periodEnd" | "createdAt" | "updatedAt"
> & {
  periodStart: unknown;
  periodEnd: unknown;
  createdAt: unknown;
  updatedAt: unknown;
};

type RewriteCreditRow = Omit<RewriteCredit, "grantedAt" | "expiresAt"> & {
  grantedAt: unknown;
  expiresAt: unknown;
};

type QuotaConsumptionRow = {
  consumptionSource: "plan" | "credit";
  id: string;
  userId: string;
  periodKey?: string | null;
  periodStart?: unknown;
  periodEnd?: unknown;
  count?: number | null;
  createdAt?: unknown;
  updatedAt?: unknown;
  source?: string | null;
  amountGranted?: number | null;
  amountConsumed?: number | null;
  grantedAt?: unknown;
  expiresAt?: unknown;
  stripeEventId?: string | null;
};

export type QuotaConsumption =
  | {
      source: "plan";
      usage: RewriteUsage;
    }
  | {
      source: "credit";
      credit: RewriteCredit;
    };

function mapRewriteUsage(row: RewriteUsageRow): RewriteUsage {
  return {
    ...row,
    periodStart: nullableDate(row.periodStart),
    periodEnd: nullableDate(row.periodEnd),
    createdAt: requiredDate(row.createdAt),
    updatedAt: requiredDate(row.updatedAt),
  };
}

function mapRewriteCredit(row: RewriteCreditRow): RewriteCredit {
  return {
    ...row,
    grantedAt: requiredDate(row.grantedAt),
    expiresAt: nullableDate(row.expiresAt),
  };
}

function mapQuotaConsumption(row: QuotaConsumptionRow): QuotaConsumption {
  if (row.consumptionSource === "plan") {
    return {
      source: "plan",
      usage: mapRewriteUsage(row as RewriteUsageRow),
    };
  }

  return {
    source: "credit",
    credit: mapRewriteCredit(row as RewriteCreditRow),
  };
}

export function isPaidSubscriptionStatus(status: string | null | undefined) {
  return ACTIVE_STATUSES.has(status ?? "");
}

export function normalizePlanTier(tier: string | null | undefined): PlanTier {
  return PLAN_TIERS.has(tier ?? "") ? (tier as PlanTier) : "free";
}

export function resolvePlanTier(user: UsageSubject): PlanTier {
  const explicitTier = normalizePlanTier(user.planTier);
  if (explicitTier !== "free") {
    return explicitTier;
  }

  return isPaidSubscriptionStatus(user.subscriptionStatus) ? "starter" : "free";
}

export function getUsagePlan(user: UsageSubject): UsagePlan {
  if (user.subscriptionStatus === "testing") {
    return {
      allowed: true,
      scope: "paid",
      planTier: "testing",
      quota: TESTING_REWRITE_LIMIT,
      periodKey: `testing:${user.id}`,
      periodStart: null,
      periodEnd: null,
    };
  }

  const planTier = resolvePlanTier(user);

  if (planTier !== "free") {
    const subscriptionId = user.stripeSubscriptionId ?? `user_${user.id}`;
    const periodEnd = user.currentPeriodEnd ?? null;
    const periodKey = `paid:${subscriptionId}:${
      periodEnd?.toISOString() ?? "no-period"
    }`;

    return {
      allowed: true,
      scope: "paid",
      planTier,
      quota: PLAN_ALLOWANCES[planTier],
      periodKey,
      periodStart: null,
      periodEnd,
    };
  }

  return {
    allowed: true,
    scope: "free",
    planTier,
    quota: PLAN_ALLOWANCES.free,
    periodKey: "lifetime",
    periodStart: null,
    periodEnd: null,
  };
}

async function getUsageCount(userId: string, periodKey: string) {
  const sql = getSql();
  const rows = (await sql`
    /* quota:get_usage_count */
    SELECT "count"
    FROM "RewriteUsage"
    WHERE "userId" = ${userId}
      AND "periodKey" = ${periodKey}
    LIMIT 1
  `) as Array<{ count: number }>;

  return rows[0]?.count ?? 0;
}

async function getCreditRemaining(userId: string, now = new Date()) {
  const sql = getSql();
  const rows = (await sql`
    /* quota:get_credit_remaining */
    SELECT COALESCE(
      SUM(GREATEST("amountGranted" - "amountConsumed", 0)),
      0
    )::int AS "remaining"
    FROM "RewriteCredit"
    WHERE "userId" = ${userId}
      AND "amountConsumed" < "amountGranted"
      AND ("expiresAt" IS NULL OR "expiresAt" > ${now})
  `) as Array<{ remaining: number | string | null }>;

  return Number(rows[0]?.remaining ?? 0);
}

export async function getRemaining(user: UsageSubject): Promise<number> {
  const plan = getUsagePlan(user);
  const [used, creditRemaining] = await Promise.all([
    getUsageCount(user.id, plan.periodKey),
    getCreditRemaining(user.id),
  ]);

  return Math.max(plan.quota - used, 0) + creditRemaining;
}

export async function getUsageStatus(user: UsageSubject): Promise<UsageStatus> {
  const plan = getUsagePlan(user);
  const [used, creditRemaining] = await Promise.all([
    getUsageCount(user.id, plan.periodKey),
    getCreditRemaining(user.id),
  ]);
  const planRemaining = Math.max(plan.quota - used, 0);
  const remaining = planRemaining + creditRemaining;

  return {
    ...plan,
    used,
    planRemaining,
    creditRemaining,
    remaining,
    exhausted: remaining <= 0,
  };
}

export async function ensureQuotaAvailable(user: UsageSubject) {
  const status = await getUsageStatus(user);
  if (status.exhausted) {
    throw new QuotaExceededError();
  }
  return status;
}

export function canUseApi(user: UsageSubject) {
  return resolvePlanTier(user) === "pro";
}

export async function consumeOneRewrite(
  userId: string,
): Promise<QuotaConsumption> {
  const sql = getSql();
  const now = new Date();

  const [rows] = (await sql.transaction(
    (tx) => [
      tx`
        /* quota:consume_one_rewrite */
        WITH target_user AS (
          SELECT
            "id",
            "subscriptionStatus",
            "stripeSubscriptionId",
            "currentPeriodEnd",
            "planTier"
          FROM "User"
          WHERE "id" = ${userId}
          LIMIT 1
        ),
        effective_plan AS (
          SELECT
            "id" AS "userId",
            CASE
              WHEN "subscriptionStatus" = 'testing' THEN 'testing'
              WHEN "planTier" IN ('starter', 'pro') THEN "planTier"
              WHEN "subscriptionStatus" IN ('active', 'trialing') THEN 'starter'
              ELSE 'free'
            END AS "planTier",
            CASE
              WHEN "subscriptionStatus" = 'testing' THEN ${TESTING_REWRITE_LIMIT}
              WHEN "planTier" = 'pro' THEN ${PLAN_ALLOWANCES.pro}
              WHEN "planTier" = 'starter'
                OR "subscriptionStatus" IN ('active', 'trialing')
                THEN ${PLAN_ALLOWANCES.starter}
              ELSE ${PLAN_ALLOWANCES.free}
            END AS "quota",
            CASE
              WHEN "subscriptionStatus" = 'testing' THEN 'testing:' || "id"
              WHEN "planTier" IN ('starter', 'pro')
                OR "subscriptionStatus" IN ('active', 'trialing')
                THEN 'paid:' ||
                  COALESCE("stripeSubscriptionId", 'user_' || "id") ||
                  ':' ||
                  COALESCE(
                    to_char(
                      "currentPeriodEnd",
                      'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'
                    ),
                    'no-period'
                  )
              ELSE 'lifetime'
            END AS "periodKey",
            CASE
              WHEN "subscriptionStatus" = 'testing'
                OR (
                  "planTier" NOT IN ('starter', 'pro')
                  AND "subscriptionStatus" NOT IN ('active', 'trialing')
                )
                THEN NULL
              ELSE "currentPeriodEnd"
            END AS "periodEnd"
          FROM target_user
        ),
        plan_charge AS (
          INSERT INTO "RewriteUsage" (
            "id",
            "userId",
            "periodKey",
            "periodStart",
            "periodEnd",
            "count",
            "createdAt",
            "updatedAt"
          )
          SELECT
            ${createId()},
            "userId",
            "periodKey",
            NULL,
            "periodEnd",
            1,
            now(),
            now()
          FROM effective_plan
          WHERE "quota" > 0
          ON CONFLICT ("userId", "periodKey")
          DO UPDATE SET
            "count" = "RewriteUsage"."count" + 1,
            "periodStart" = EXCLUDED."periodStart",
            "periodEnd" = EXCLUDED."periodEnd",
            "updatedAt" = now()
          WHERE "RewriteUsage"."count" < (
            SELECT "quota"
            FROM effective_plan
          )
          RETURNING
            "id",
            "userId",
            "periodKey",
            "periodStart",
            "periodEnd",
            "count",
            "createdAt",
            "updatedAt"
        ),
        credit_candidate AS (
          SELECT credit."id"
          FROM "RewriteCredit" credit
          JOIN effective_plan plan ON plan."userId" = credit."userId"
          WHERE NOT EXISTS (SELECT 1 FROM plan_charge)
            AND credit."amountConsumed" < credit."amountGranted"
            AND (
              credit."expiresAt" IS NULL
              OR credit."expiresAt" > ${now}
            )
          ORDER BY
            credit."expiresAt" ASC NULLS LAST,
            credit."grantedAt" ASC,
            credit."id" ASC
          LIMIT 1
          FOR UPDATE
        ),
        credit_charge AS (
          UPDATE "RewriteCredit" credit
          SET "amountConsumed" = credit."amountConsumed" + 1
          FROM credit_candidate candidate
          WHERE credit."id" = candidate."id"
          RETURNING
            credit."id",
            credit."userId",
            credit."source",
            credit."amountGranted",
            credit."amountConsumed",
            credit."grantedAt",
            credit."expiresAt",
            credit."stripeEventId"
        )
        SELECT
          'plan' AS "consumptionSource",
          "id",
          "userId",
          "periodKey",
          "periodStart",
          "periodEnd",
          "count",
          "createdAt",
          "updatedAt",
          NULL::text AS "source",
          NULL::integer AS "amountGranted",
          NULL::integer AS "amountConsumed",
          NULL::timestamp AS "grantedAt",
          NULL::timestamp AS "expiresAt",
          NULL::text AS "stripeEventId"
        FROM plan_charge
        UNION ALL
        SELECT
          'credit' AS "consumptionSource",
          "id",
          "userId",
          NULL::text AS "periodKey",
          NULL::timestamp AS "periodStart",
          NULL::timestamp AS "periodEnd",
          NULL::integer AS "count",
          NULL::timestamp AS "createdAt",
          NULL::timestamp AS "updatedAt",
          "source",
          "amountGranted",
          "amountConsumed",
          "grantedAt",
          "expiresAt",
          "stripeEventId"
        FROM credit_charge
      `,
    ],
    { isolationLevel: "Serializable" },
  )) as [QuotaConsumptionRow[]];

  if (!rows[0]) {
    throw new QuotaExceededError();
  }

  return mapQuotaConsumption(rows[0]);
}

export async function chargeSuccessfulRewrite(
  user: UsageSubject,
): Promise<QuotaConsumption> {
  return consumeOneRewrite(user.id);
}
