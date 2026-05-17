import type { RewriteUsage, User } from "@prisma/client";

export const FREE_REWRITE_LIMIT = 3;
export const PAID_REWRITE_LIMIT = 100;

const ACTIVE_STATUSES = new Set(["active", "trialing"]);

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

export function isPaidSubscriptionStatus(status: string | null | undefined) {
  return ACTIVE_STATUSES.has(status ?? "");
}

export function getUsagePlan(user: UsageSubject): UsagePlan {
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
  const { prisma } = await import("./db");
  const usage = await prisma.rewriteUsage.findUnique({
    where: {
      userId_periodKey: {
        userId,
        periodKey,
      },
    },
  });

  return usage?.count ?? 0;
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
  const { prisma } = await import("./db");

  return prisma.$transaction(async (tx) => {
    await tx.rewriteUsage.upsert({
      where: {
        userId_periodKey: {
          userId: user.id,
          periodKey: plan.periodKey,
        },
      },
      create: {
        userId: user.id,
        periodKey: plan.periodKey,
        periodStart: plan.periodStart,
        periodEnd: plan.periodEnd,
        count: 0,
      },
      update: {
        periodStart: plan.periodStart,
        periodEnd: plan.periodEnd,
      },
    });

    const increment = await tx.rewriteUsage.updateMany({
      where: {
        userId: user.id,
        periodKey: plan.periodKey,
        count: {
          lt: plan.quota,
        },
      },
      data: {
        count: {
          increment: 1,
        },
        periodStart: plan.periodStart,
        periodEnd: plan.periodEnd,
      },
    });

    if (increment.count !== 1) {
      throw new QuotaExceededError();
    }

    const usage = await tx.rewriteUsage.findUniqueOrThrow({
      where: {
        userId_periodKey: {
          userId: user.id,
          periodKey: plan.periodKey,
        },
      },
    });

    return usage;
  });
}
