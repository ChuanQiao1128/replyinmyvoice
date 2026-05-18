import type { RewriteUsage, User } from "./generated/prisma/client";

import { createId, getSql, nullableDate, requiredDate } from "./db";

export const FREE_REWRITE_LIMIT = 3;
export const PAID_REWRITE_LIMIT = 100;
export const TESTING_REWRITE_LIMIT = 10_000;

const ACTIVE_STATUSES = new Set(["active", "trialing", "testing"]);

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
>;

export type UsagePlan = {
  allowed: boolean;
  scope: "free" | "paid";
  quota: number;
  periodKey: string;
  periodStart: Date | null;
  periodEnd: Date | null;
};

export type UsageStatus = UsagePlan & {
  used: number;
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

function mapRewriteUsage(row: RewriteUsageRow): RewriteUsage {
  return {
    ...row,
    periodStart: nullableDate(row.periodStart),
    periodEnd: nullableDate(row.periodEnd),
    createdAt: requiredDate(row.createdAt),
    updatedAt: requiredDate(row.updatedAt),
  };
}

export function isPaidSubscriptionStatus(status: string | null | undefined) {
  return ACTIVE_STATUSES.has(status ?? "");
}

export function getUsagePlan(user: UsageSubject): UsagePlan {
  if (user.subscriptionStatus === "testing") {
    return {
      allowed: true,
      scope: "paid",
      quota: TESTING_REWRITE_LIMIT,
      periodKey: `testing:${user.id}`,
      periodStart: null,
      periodEnd: null,
    };
  }

  if (isPaidSubscriptionStatus(user.subscriptionStatus)) {
    const subscriptionId = user.stripeSubscriptionId ?? `user_${user.id}`;
    const periodEnd = user.currentPeriodEnd ?? null;
    const periodKey = `paid:${subscriptionId}:${
      periodEnd?.toISOString() ?? "no-period"
    }`;

    return {
      allowed: true,
      scope: "paid",
      quota: PAID_REWRITE_LIMIT,
      periodKey,
      periodStart: null,
      periodEnd,
    };
  }

  return {
    allowed: true,
    scope: "free",
    quota: FREE_REWRITE_LIMIT,
    periodKey: "lifetime",
    periodStart: null,
    periodEnd: null,
  };
}

async function getUsageCount(userId: string, periodKey: string) {
  const sql = getSql();
  const rows = (await sql`
    SELECT "count"
    FROM "RewriteUsage"
    WHERE "userId" = ${userId}
      AND "periodKey" = ${periodKey}
    LIMIT 1
  `) as Array<{ count: number }>;

  return rows[0]?.count ?? 0;
}

export async function getUsageStatus(user: UsageSubject): Promise<UsageStatus> {
  const plan = getUsagePlan(user);
  const used = await getUsageCount(user.id, plan.periodKey);
  const remaining = Math.max(plan.quota - used, 0);

  return {
    ...plan,
    used,
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

export async function chargeSuccessfulRewrite(
  user: UsageSubject,
): Promise<RewriteUsage> {
  const plan = getUsagePlan(user);
  const sql = getSql();

  const rows = (await sql`
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
    VALUES (
      ${createId()},
      ${user.id},
      ${plan.periodKey},
      ${plan.periodStart},
      ${plan.periodEnd},
      1,
      now(),
      now()
    )
    ON CONFLICT ("userId", "periodKey")
    DO UPDATE SET
      "count" = "RewriteUsage"."count" + 1,
      "periodStart" = EXCLUDED."periodStart",
      "periodEnd" = EXCLUDED."periodEnd",
      "updatedAt" = now()
    WHERE "RewriteUsage"."count" < ${plan.quota}
    RETURNING *
  `) as RewriteUsageRow[];

  if (!rows[0]) {
    throw new QuotaExceededError();
  }

  return mapRewriteUsage(rows[0]);
}
