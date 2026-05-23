import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { getSql } from "../../lib/db";
import {
  canUseApi,
  consumeOneRewrite,
  getRemaining,
  getUsagePlan,
  PLAN_ALLOWANCES,
  QuotaExceededError,
  type UsageSubject,
} from "../../lib/quota";

vi.mock("../../lib/db", async () => {
  const actual = await vi.importActual<typeof import("../../lib/db")>(
    "../../lib/db",
  );
  let id = 0;

  return {
    ...actual,
    createId: vi.fn(() => `quota_test_${++id}`),
    getSql: vi.fn(),
  };
});

type FakeUser = UsageSubject;

type FakeUsage = {
  id: string;
  userId: string;
  periodKey: string;
  periodStart: Date | null;
  periodEnd: Date | null;
  count: number;
  createdAt: Date;
  updatedAt: Date;
};

type FakeCredit = {
  id: string;
  userId: string;
  source: string;
  amountGranted: number;
  amountConsumed: number;
  grantedAt: Date;
  expiresAt: Date | null;
  stripeEventId: string | null;
};

type FakeQuotaDb = {
  users: FakeUser[];
  usages?: FakeUsage[];
  credits?: FakeCredit[];
};

type CapturedQuery = {
  text: string;
  values: unknown[];
};

function usageRow(
  overrides: Partial<FakeUsage> & Pick<FakeUsage, "userId" | "periodKey">,
): FakeUsage {
  return {
    id: "usage_1",
    count: 0,
    periodStart: null,
    periodEnd: null,
    createdAt: new Date("2026-05-01T00:00:00.000Z"),
    updatedAt: new Date("2026-05-01T00:00:00.000Z"),
    ...overrides,
  };
}

function creditRow(overrides: Partial<FakeCredit> & Pick<FakeCredit, "id" | "userId">): FakeCredit {
  return {
    source: "top_up",
    amountGranted: 1,
    amountConsumed: 0,
    grantedAt: new Date("2026-05-01T00:00:00.000Z"),
    expiresAt: null,
    stripeEventId: null,
    ...overrides,
  };
}

function user(overrides: Partial<FakeUser> & Pick<FakeUser, "id">): FakeUser {
  return {
    subscriptionStatus: "inactive",
    stripeSubscriptionId: null,
    currentPeriodEnd: null,
    planTier: "free",
    ...overrides,
  };
}

function availableCreditUnits(db: Required<FakeQuotaDb>, userId: string, now: Date) {
  return db.credits
    .filter((credit) => credit.userId === userId)
    .filter((credit) => credit.amountConsumed < credit.amountGranted)
    .filter((credit) => credit.expiresAt === null || credit.expiresAt > now)
    .reduce(
      (total, credit) => total + credit.amountGranted - credit.amountConsumed,
      0,
    );
}

function nextConsumableCredit(
  db: Required<FakeQuotaDb>,
  userId: string,
  now: Date,
) {
  return db.credits
    .filter((credit) => credit.userId === userId)
    .filter((credit) => credit.amountConsumed < credit.amountGranted)
    .filter((credit) => credit.expiresAt === null || credit.expiresAt > now)
    .sort((left, right) => {
      if (left.expiresAt && right.expiresAt) {
        const expiryDelta = left.expiresAt.getTime() - right.expiresAt.getTime();
        if (expiryDelta !== 0) {
          return expiryDelta;
        }
      } else if (left.expiresAt) {
        return -1;
      } else if (right.expiresAt) {
        return 1;
      }

      const grantDelta = left.grantedAt.getTime() - right.grantedAt.getTime();
      return grantDelta === 0 ? left.id.localeCompare(right.id) : grantDelta;
    })[0];
}

function installQuotaDb(input: FakeQuotaDb) {
  const db: Required<FakeQuotaDb> = {
    users: input.users,
    usages: input.usages ?? [],
    credits: input.credits ?? [],
  };

  const execute = (query: CapturedQuery) => {
    if (query.text.includes("quota:get_usage_count")) {
      const [userId, periodKey] = query.values as [string, string];
      const usage = db.usages.find(
        (candidate) =>
          candidate.userId === userId && candidate.periodKey === periodKey,
      );
      return Promise.resolve(usage ? [{ count: usage.count }] : []);
    }

    if (query.text.includes("quota:get_credit_remaining")) {
      const [userId, now] = query.values as [string, Date];
      return Promise.resolve([
        { remaining: availableCreditUnits(db, userId, now) },
      ]);
    }

    if (query.text.includes("quota:consume_one_rewrite")) {
      const [userId] = query.values as [string];
      const now = query.values.find(
        (value): value is Date => value instanceof Date,
      );
      if (!now) {
        throw new Error("Expected quota consumption query to bind current time");
      }
      const targetUser = db.users.find((candidate) => candidate.id === userId);
      if (!targetUser) {
        return Promise.resolve([]);
      }

      const plan = getUsagePlan(targetUser);
      let usage = db.usages.find(
        (candidate) =>
          candidate.userId === userId && candidate.periodKey === plan.periodKey,
      );

      if (usage && usage.count < plan.quota) {
        usage.count += 1;
        usage.updatedAt = now;
        return Promise.resolve([{ consumptionSource: "plan", ...usage }]);
      }

      if (!usage && plan.quota > 0) {
        usage = usageRow({
          id: "usage_new",
          userId,
          periodKey: plan.periodKey,
          periodStart: plan.periodStart,
          periodEnd: plan.periodEnd,
          count: 1,
          createdAt: now,
          updatedAt: now,
        });
        db.usages.push(usage);
        return Promise.resolve([{ consumptionSource: "plan", ...usage }]);
      }

      const credit = nextConsumableCredit(db, userId, now);
      if (!credit) {
        return Promise.resolve([]);
      }

      credit.amountConsumed += 1;
      return Promise.resolve([{ consumptionSource: "credit", ...credit }]);
    }

    throw new Error(`Unexpected quota SQL: ${query.text}`);
  };

  const tag = (strings: TemplateStringsArray, ...values: unknown[]) =>
    execute({ text: strings.join("?"), values });
  const txTag = (strings: TemplateStringsArray, ...values: unknown[]) => ({
    text: strings.join("?"),
    values,
  });
  const sql = Object.assign(vi.fn(tag), {
    transaction: vi.fn(async (queriesOrFn: unknown) => {
      const queries =
        typeof queriesOrFn === "function"
          ? (queriesOrFn as (sql: typeof txTag) => CapturedQuery[])(txTag)
          : (queriesOrFn as CapturedQuery[]);
      return Promise.all(queries.map(execute));
    }),
  });

  vi.mocked(getSql).mockReturnValue(sql as unknown as ReturnType<typeof getSql>);
  return db;
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-05-24T00:00:00.000Z"));
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("getUsagePlan", () => {
  it("uses lifetime quota for inactive free users", () => {
    const plan = getUsagePlan(
      user({
        id: "user_1",
      }),
    );

    expect(plan).toMatchObject({
      allowed: true,
      planTier: "free",
      quota: PLAN_ALLOWANCES.free,
      periodKey: "lifetime",
      scope: "free",
    });
  });

  it("falls active paid subscriptions without a plan tier back to starter quota", () => {
    const periodEnd = new Date("2026-06-17T00:00:00.000Z");
    const plan = getUsagePlan(
      user({
        id: "user_1",
        planTier: "free",
        subscriptionStatus: "active",
        stripeSubscriptionId: "sub_123",
        currentPeriodEnd: periodEnd,
      }),
    );

    expect(plan).toMatchObject({
      allowed: true,
      planTier: "starter",
      quota: PLAN_ALLOWANCES.starter,
      periodKey: "paid:sub_123:2026-06-17T00:00:00.000Z",
      scope: "paid",
      periodEnd,
    });
  });

  it("uses pro quota when the user plan tier is pro", () => {
    const periodEnd = new Date("2026-06-17T00:00:00.000Z");
    const plan = getUsagePlan(
      user({
        id: "user_1",
        planTier: "pro",
        subscriptionStatus: "active",
        stripeSubscriptionId: "sub_123",
        currentPeriodEnd: periodEnd,
      }),
    );

    expect(plan.planTier).toBe("pro");
    expect(plan.quota).toBe(PLAN_ALLOWANCES.pro);
  });

  it("allows internal testing accounts to run a high rewrite quota", () => {
    const plan = getUsagePlan(
      user({
        id: "user_test",
        subscriptionStatus: "testing",
      }),
    );

    expect(plan.scope).toBe("paid");
    expect(plan.quota).toBe(10_000);
    expect(plan.periodKey).toBe("testing:user_test");
  });
});

describe("quota consumption", () => {
  it("free users consume 3 lifetime rewrites then exhaust", async () => {
    const freeUser = user({ id: "user_free" });
    const db = installQuotaDb({ users: [freeUser] });

    await expect(getRemaining(freeUser)).resolves.toBe(3);
    await expect(consumeOneRewrite(freeUser.id)).resolves.toMatchObject({
      source: "plan",
    });
    await consumeOneRewrite(freeUser.id);
    await consumeOneRewrite(freeUser.id);

    expect(db.usages).toHaveLength(1);
    expect(db.usages[0]).toMatchObject({
      periodKey: "lifetime",
      count: 3,
    });
    await expect(getRemaining(freeUser)).resolves.toBe(0);
    await expect(consumeOneRewrite(freeUser.id)).rejects.toBeInstanceOf(
      QuotaExceededError,
    );
  });

  it("starter users consume 55 monthly rewrites then exhaust", async () => {
    const starterUser = user({
      id: "user_starter",
      planTier: "starter",
      stripeSubscriptionId: "sub_starter",
      currentPeriodEnd: new Date("2026-06-24T00:00:00.000Z"),
    });
    const db = installQuotaDb({ users: [starterUser] });

    await expect(getRemaining(starterUser)).resolves.toBe(55);
    for (let index = 0; index < 55; index += 1) {
      await consumeOneRewrite(starterUser.id);
    }

    expect(db.usages[0]).toMatchObject({
      periodKey: "paid:sub_starter:2026-06-24T00:00:00.000Z",
      count: 55,
    });
    await expect(getRemaining(starterUser)).resolves.toBe(0);
    await expect(consumeOneRewrite(starterUser.id)).rejects.toBeInstanceOf(
      QuotaExceededError,
    );
  });

  it("consumes plan allowance before credit ledger units", async () => {
    const freeUser = user({ id: "user_plan_first" });
    const db = installQuotaDb({
      users: [freeUser],
      usages: [
        usageRow({
          userId: freeUser.id,
          periodKey: "lifetime",
          count: 2,
        }),
      ],
      credits: [
        creditRow({
          id: "credit_topup",
          userId: freeUser.id,
          amountGranted: 10,
        }),
      ],
    });

    await expect(getRemaining(freeUser)).resolves.toBe(11);
    await expect(consumeOneRewrite(freeUser.id)).resolves.toMatchObject({
      source: "plan",
    });

    expect(db.usages[0].count).toBe(3);
    expect(db.credits[0].amountConsumed).toBe(0);
  });

  it("consumes the soonest-expiring credit after plan allowance is exhausted", async () => {
    const freeUser = user({ id: "user_credits" });
    const db = installQuotaDb({
      users: [freeUser],
      usages: [
        usageRow({
          userId: freeUser.id,
          periodKey: "lifetime",
          count: 3,
        }),
      ],
      credits: [
        creditRow({
          id: "credit_later",
          userId: freeUser.id,
          expiresAt: new Date("2026-06-10T00:00:00.000Z"),
        }),
        creditRow({
          id: "credit_earlier",
          userId: freeUser.id,
          expiresAt: new Date("2026-05-30T00:00:00.000Z"),
        }),
        creditRow({
          id: "credit_no_expiry",
          userId: freeUser.id,
          expiresAt: null,
        }),
      ],
    });

    await expect(consumeOneRewrite(freeUser.id)).resolves.toMatchObject({
      source: "credit",
      credit: {
        id: "credit_earlier",
      },
    });

    expect(
      db.credits.find((credit) => credit.id === "credit_earlier")
        ?.amountConsumed,
    ).toBe(1);
    expect(
      db.credits.find((credit) => credit.id === "credit_later")
        ?.amountConsumed,
    ).toBe(0);
    expect(
      db.credits.find((credit) => credit.id === "credit_no_expiry")
        ?.amountConsumed,
    ).toBe(0);
  });

  it("excludes expired credits from remaining quota and consumption", async () => {
    const freeUser = user({ id: "user_expired" });
    const db = installQuotaDb({
      users: [freeUser],
      usages: [
        usageRow({
          userId: freeUser.id,
          periodKey: "lifetime",
          count: 3,
        }),
      ],
      credits: [
        creditRow({
          id: "credit_expired",
          userId: freeUser.id,
          amountGranted: 10,
          expiresAt: new Date("2026-05-23T00:00:00.000Z"),
        }),
        creditRow({
          id: "credit_available",
          userId: freeUser.id,
          amountGranted: 1,
          expiresAt: new Date("2026-05-25T00:00:00.000Z"),
        }),
      ],
    });

    await expect(getRemaining(freeUser)).resolves.toBe(1);
    await consumeOneRewrite(freeUser.id);

    expect(
      db.credits.find((credit) => credit.id === "credit_expired")
        ?.amountConsumed,
    ).toBe(0);
    expect(
      db.credits.find((credit) => credit.id === "credit_available")
        ?.amountConsumed,
    ).toBe(1);
    await expect(consumeOneRewrite(freeUser.id)).rejects.toBeInstanceOf(
      QuotaExceededError,
    );
  });

  it("combines remaining monthly allowance and available credits", async () => {
    const starterUser = user({
      id: "user_mixed",
      planTier: "starter",
      stripeSubscriptionId: "sub_mixed",
      currentPeriodEnd: new Date("2026-06-24T00:00:00.000Z"),
    });
    const db = installQuotaDb({
      users: [starterUser],
      usages: [
        usageRow({
          userId: starterUser.id,
          periodKey: "paid:sub_mixed:2026-06-24T00:00:00.000Z",
          count: 54,
        }),
      ],
      credits: [
        creditRow({
          id: "credit_bonus",
          userId: starterUser.id,
          amountGranted: 4,
        }),
      ],
    });

    await expect(getRemaining(starterUser)).resolves.toBe(5);
    await expect(consumeOneRewrite(starterUser.id)).resolves.toMatchObject({
      source: "plan",
    });
    await expect(consumeOneRewrite(starterUser.id)).resolves.toMatchObject({
      source: "credit",
      credit: {
        id: "credit_bonus",
      },
    });

    expect(db.usages[0].count).toBe(55);
    expect(db.credits[0].amountConsumed).toBe(1);
  });
});

describe("canUseApi", () => {
  it.each([
    ["free", false],
    ["starter", false],
    ["pro", true],
  ] as const)("returns %s API access as %s", (planTier, expected) => {
    expect(canUseApi(user({ id: `user_${planTier}`, planTier }))).toBe(
      expected,
    );
  });
});
