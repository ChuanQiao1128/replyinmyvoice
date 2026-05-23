import type Stripe from "stripe";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { getSql } from "../../lib/db";
import {
  applySubscriptionToUser,
  grantCheckoutSessionRewriteCredit,
  grantFirstMonthBonusCredit,
} from "../../lib/stripe";
import {
  findUserByClerkId,
  findUserByStripeCustomerId,
} from "../../lib/users";

vi.mock("../../lib/db", async () => {
  const actual = await vi.importActual<typeof import("../../lib/db")>(
    "../../lib/db",
  );
  let id = 0;

  return {
    ...actual,
    createId: vi.fn(() => `credit_test_${++id}`),
    getSql: vi.fn(),
  };
});

vi.mock("../../lib/users", async () => {
  const actual = await vi.importActual<typeof import("../../lib/users")>(
    "../../lib/users",
  );

  return {
    ...actual,
    findUserByClerkId: vi.fn(),
    findUserByStripeCustomerId: vi.fn(),
    mapUser: vi.fn((row: unknown) => row),
  };
});

type FakeUser = {
  id: string;
  clerkUserId: string;
  entraUserId: string | null;
  email: string | null;
  stripeCustomerId: string | null;
  stripeSubscriptionId: string | null;
  stripePriceId: string | null;
  subscriptionStatus: string;
  currentPeriodEnd: Date | null;
  planTier: string;
  referralCode: string | null;
  referredByUserId: string | null;
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
  stripeEventId: string;
};

type FakeDb = {
  users: FakeUser[];
  credits: FakeCredit[];
};

function fakeUser(overrides: Partial<FakeUser> = {}): FakeUser {
  return {
    id: "user_123",
    clerkUserId: "clerk_123",
    entraUserId: null,
    email: "casey@example.com",
    stripeCustomerId: "cus_test",
    stripeSubscriptionId: null,
    stripePriceId: null,
    subscriptionStatus: "inactive",
    currentPeriodEnd: null,
    planTier: "free",
    referralCode: null,
    referredByUserId: null,
    createdAt: new Date("2026-05-01T00:00:00.000Z"),
    updatedAt: new Date("2026-05-01T00:00:00.000Z"),
    ...overrides,
  };
}

function subscription(priceId: string, overrides: Partial<Stripe.Subscription> = {}) {
  return {
    id: "sub_test",
    customer: "cus_test",
    status: "active",
    metadata: {
      clerkUserId: "clerk_123",
      userId: "user_123",
    },
    items: {
      data: [
        {
          price: {
            id: priceId,
          },
          current_period_end: 1780012800,
        },
      ],
    },
    ...overrides,
  } as Stripe.Subscription;
}

function checkoutSession(priceId: string): Stripe.Checkout.Session {
  return {
    id: "cs_test",
    mode: "payment",
    customer: "cus_test",
    client_reference_id: "clerk_123",
    metadata: {
      clerkUserId: "clerk_123",
      userId: "user_123",
      priceId,
      checkoutSku: "exam_pass",
    },
  } as unknown as Stripe.Checkout.Session;
}

function firstInvoice(overrides: Partial<Stripe.Invoice> = {}) {
  return {
    id: "in_first",
    billing_reason: "subscription_create",
    subscription: "sub_test",
    ...overrides,
  } as Stripe.Invoice;
}

function installDb(db: FakeDb) {
  const sql = vi.fn(
    async (strings: TemplateStringsArray, ...values: unknown[]) => {
      const text = strings.join("?");

      if (text.includes("stripe:apply_subscription_to_user")) {
        const [
          customerId,
          subscriptionId,
          priceId,
          status,
          currentPeriodEnd,
          planTier,
          userId,
        ] = values as [string, string, string, string, Date, string, string];
        const user = db.users.find((candidate) => candidate.id === userId);
        if (!user) {
          return [];
        }

        Object.assign(user, {
          stripeCustomerId: customerId,
          stripeSubscriptionId: subscriptionId,
          stripePriceId: priceId,
          subscriptionStatus: status,
          currentPeriodEnd,
          planTier,
          updatedAt: new Date("2026-05-24T00:00:00.000Z"),
        });
        return [user];
      }

      if (text.includes("stripe:grant_rewrite_credit")) {
        const [
          existingStripeEventId,
          id,
          userId,
          source,
          amountGranted,
          expiresAt,
          insertedStripeEventId,
        ] = values as [
          string,
          string,
          string,
          string,
          number,
          Date | null,
          string,
        ];
        const existing = db.credits.find(
          (credit) => credit.stripeEventId === existingStripeEventId,
        );
        if (existing) {
          return [{ wasInserted: false, ...existing }];
        }

        const credit = {
          id,
          userId,
          source,
          amountGranted,
          amountConsumed: 0,
          grantedAt: new Date("2026-05-24T00:00:00.000Z"),
          expiresAt,
          stripeEventId: insertedStripeEventId,
        };
        db.credits.push(credit);
        return [{ wasInserted: true, ...credit }];
      }

      throw new Error(`Unexpected SQL: ${text}`);
    },
  );

  vi.mocked(getSql).mockReturnValue(sql as unknown as ReturnType<typeof getSql>);
}

const managedPriceEnv = [
  "STRIPE_PRICE_STARTER",
  "STRIPE_PRICE_PRO",
  "STRIPE_PRICE_EXAM_PASS",
  "STRIPE_PRICE_TOPUP",
] as const;
const previousEnv: Partial<Record<(typeof managedPriceEnv)[number], string | undefined>> = {};

beforeEach(() => {
  for (const key of managedPriceEnv) {
    previousEnv[key] = process.env[key];
  }
  process.env.STRIPE_PRICE_STARTER = "price_starter_test";
  process.env.STRIPE_PRICE_PRO = "price_pro_test";
  process.env.STRIPE_PRICE_EXAM_PASS = "price_exam_test";
  process.env.STRIPE_PRICE_TOPUP = "price_topup_test";
});

afterEach(() => {
  vi.restoreAllMocks();
  for (const key of managedPriceEnv) {
    const previous = previousEnv[key];
    if (previous === undefined) {
      delete process.env[key];
    } else {
      process.env[key] = previous;
    }
  }
});

describe("Stripe webhook subscription updates", () => {
  it("sets planTier from the subscription price map", async () => {
    const user = fakeUser();
    installDb({ users: [user], credits: [] });
    vi.mocked(findUserByClerkId).mockResolvedValue(user);

    await applySubscriptionToUser(subscription("price_pro_test"));

    expect(user).toMatchObject({
      stripeSubscriptionId: "sub_test",
      stripePriceId: "price_pro_test",
      subscriptionStatus: "active",
      planTier: "pro",
    });
  });
});

describe("Stripe webhook credit grants", () => {
  it("grants one-time payment credits once when a webhook event replays", async () => {
    const db = { users: [fakeUser()], credits: [] };
    installDb(db);

    const now = new Date("2026-05-24T00:00:00.000Z");
    await expect(
      grantCheckoutSessionRewriteCredit(
        checkoutSession("price_exam_test"),
        "evt_payment_1",
        now,
      ),
    ).resolves.toMatchObject({ granted: true });
    await expect(
      grantCheckoutSessionRewriteCredit(
        checkoutSession("price_exam_test"),
        "evt_payment_1",
        now,
      ),
    ).resolves.toMatchObject({ granted: false });

    expect(db.credits).toHaveLength(1);
    expect(db.credits[0]).toMatchObject({
      userId: "user_123",
      source: "exam_pass",
      amountGranted: 25,
      expiresAt: new Date("2026-05-31T00:00:00.000Z"),
      stripeEventId: "evt_payment_1",
    });
  });

  it("grants first-month bonus once for the first subscription invoice", async () => {
    const db = { users: [fakeUser()], credits: [] };
    installDb(db);
    vi.mocked(findUserByClerkId).mockResolvedValue(db.users[0]);
    vi.mocked(findUserByStripeCustomerId).mockResolvedValue(db.users[0]);

    const now = new Date("2026-05-24T00:00:00.000Z");
    const proSubscription = subscription("price_pro_test");

    await expect(
      grantFirstMonthBonusCredit({
        invoice: firstInvoice(),
        subscription: proSubscription,
        now,
      }),
    ).resolves.toMatchObject({ granted: true });
    await expect(
      grantFirstMonthBonusCredit({
        invoice: firstInvoice(),
        subscription: proSubscription,
        now,
      }),
    ).resolves.toMatchObject({ granted: false });

    expect(db.credits).toHaveLength(1);
    expect(db.credits[0]).toMatchObject({
      userId: "user_123",
      source: "first_month_bonus",
      amountGranted: 15,
      expiresAt: null,
      stripeEventId: "first_month_bonus:in_first",
    });
  });
});
