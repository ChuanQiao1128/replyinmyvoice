import Stripe from "stripe";

import { createId, getSql, requiredDate } from "./db";
import { getAppUrl, optionalEnv, requireEnv } from "./env";
import {
  findUserByClerkId,
  findUserByStripeCustomerId,
  mapUser,
} from "./users";

let stripeClient: Stripe | null = null;

export function getStripe(): Stripe {
  if (!stripeClient) {
    stripeClient = new Stripe(requireEnv("STRIPE_SECRET_KEY"));
  }

  return stripeClient;
}

export function getStripePriceId(): string {
  return requireEnv("STRIPE_PRICE_ID");
}

export type StripeCheckoutSku = "starter" | "pro" | "exam_pass" | "top_up";
export type StripePlanTier = "starter" | "pro";
export type StripeCreditSource = "exam_pass" | "top_up" | "first_month_bonus";

export type StripeCreditGrant = {
  source: StripeCreditSource;
  amountGranted: number;
  expiresAt: Date | null;
};

type CheckoutSkuConfig = {
  envName: string;
  mode: "payment" | "subscription";
  planTier?: StripePlanTier;
  credit?: {
    source: Extract<StripeCreditSource, "exam_pass" | "top_up">;
    amountGranted: number;
    expiresInDays: number | null;
  };
};

const CHECKOUT_SKU_CONFIG = {
  starter: {
    envName: "STRIPE_PRICE_STARTER",
    mode: "subscription",
    planTier: "starter",
  },
  pro: {
    envName: "STRIPE_PRICE_PRO",
    mode: "subscription",
    planTier: "pro",
  },
  exam_pass: {
    envName: "STRIPE_PRICE_EXAM_PASS",
    mode: "payment",
    credit: {
      source: "exam_pass",
      amountGranted: 25,
      expiresInDays: 7,
    },
  },
  top_up: {
    envName: "STRIPE_PRICE_TOPUP",
    mode: "payment",
    credit: {
      source: "top_up",
      amountGranted: 10,
      expiresInDays: null,
    },
  },
} satisfies Record<StripeCheckoutSku, CheckoutSkuConfig>;

const FIRST_MONTH_BONUS_BY_TIER = {
  starter: 5,
  pro: 15,
} satisfies Record<StripePlanTier, number>;

function configuredPriceId(envName: string) {
  return optionalEnv(envName).trim() || null;
}

function requireConfiguredPriceId(sku: StripeCheckoutSku) {
  const envName = CHECKOUT_SKU_CONFIG[sku].envName;
  const priceId = configuredPriceId(envName);
  if (!priceId) {
    throw new Error(`Missing Stripe price environment variable: ${envName}`);
  }

  return priceId;
}

function addDays(date: Date, days: number) {
  return new Date(date.getTime() + days * 24 * 60 * 60 * 1000);
}

export function getPlanTierForPriceId(
  priceId: string | null | undefined,
): StripePlanTier | null {
  if (!priceId) {
    return null;
  }

  for (const config of [CHECKOUT_SKU_CONFIG.starter, CHECKOUT_SKU_CONFIG.pro]) {
    const configured = configuredPriceId(config.envName);
    if (configured && configured === priceId) {
      return config.planTier ?? null;
    }
  }

  return null;
}

export function getCreditGrantForPriceId(
  priceId: string | null | undefined,
  now = new Date(),
): StripeCreditGrant | null {
  if (!priceId) {
    return null;
  }

  for (const config of [
    CHECKOUT_SKU_CONFIG.exam_pass,
    CHECKOUT_SKU_CONFIG.top_up,
  ]) {
    const configured = configuredPriceId(config.envName);
    if (!configured || configured !== priceId || !config.credit) {
      continue;
    }

    return {
      source: config.credit.source,
      amountGranted: config.credit.amountGranted,
      expiresAt:
        config.credit.expiresInDays === null
          ? null
          : addDays(now, config.credit.expiresInDays),
    };
  }

  return null;
}

export function getCheckoutUrls() {
  const appUrl = getAppUrl();
  return {
    successUrl: `${appUrl}/app?checkout=success`,
    cancelUrl: `${appUrl}/app?checkout=cancelled`,
  };
}

function stripeTimeoutSignal() {
  const timeoutSeconds = Number(process.env.STRIPE_TIMEOUT_SEC ?? "25");
  const controller = new AbortController();
  const timeout = setTimeout(
    () => controller.abort(),
    (Number.isFinite(timeoutSeconds) ? timeoutSeconds : 25) * 1000,
  );

  return {
    signal: controller.signal,
    clear: () => clearTimeout(timeout),
  };
}

async function stripeFetch<T>(
  path: string,
  init: Omit<RequestInit, "headers"> & { headers?: Record<string, string> } = {},
): Promise<T> {
  const timeout = stripeTimeoutSignal();

  try {
    const response = await fetch(`https://api.stripe.com/v1${path}`, {
      ...init,
      headers: {
        Authorization: `Bearer ${requireEnv("STRIPE_SECRET_KEY")}`,
        ...(init.headers ?? {}),
      },
      signal: timeout.signal,
    });

    const payload = (await response.json().catch(() => null)) as
      | (Record<string, unknown> & { error?: { type?: string; code?: string } })
      | null;

    if (!response.ok) {
      const errorType =
        payload?.error?.type ?? payload?.error?.code ?? response.status;
      throw new Error(`Stripe request failed: ${errorType}`);
    }

    return payload as T;
  } finally {
    timeout.clear();
  }
}

function postStripeForm<T>(path: string, params: URLSearchParams) {
  return stripeFetch<T>(path, {
    method: "POST",
    headers: {
      "Content-Type": "application/x-www-form-urlencoded",
    },
    body: params,
  });
}

export async function createStripeCustomer(input: {
  email?: string | null;
  clerkUserId: string;
  userId: string;
}) {
  const params = new URLSearchParams();
  if (input.email) {
    params.set("email", input.email);
  }
  params.set("metadata[clerkUserId]", input.clerkUserId);
  params.set("metadata[userId]", input.userId);

  return postStripeForm<Stripe.Customer>("/customers", params);
}

export async function createStripeCheckoutSession(input: {
  customerId: string;
  clerkUserId: string;
  userId: string;
  sku?: StripeCheckoutSku | null;
}) {
  const { successUrl, cancelUrl } = getCheckoutUrls();
  const params = buildStripeCheckoutSessionParams({
    ...input,
    successUrl,
    cancelUrl,
  });

  return postStripeForm<Stripe.Checkout.Session>("/checkout/sessions", params);
}

export function buildStripeCheckoutSessionParams(input: {
  customerId: string;
  clerkUserId: string;
  userId: string;
  successUrl: string;
  cancelUrl: string;
  sku?: StripeCheckoutSku | null;
}) {
  const config = input.sku ? CHECKOUT_SKU_CONFIG[input.sku] : null;
  const priceId = input.sku
    ? requireConfiguredPriceId(input.sku)
    : getStripePriceId();
  const mode = config?.mode ?? "subscription";
  const params = new URLSearchParams();
  params.set("mode", mode);
  params.set("customer", input.customerId);
  params.set("client_reference_id", input.clerkUserId);
  params.set("metadata[clerkUserId]", input.clerkUserId);
  params.set("metadata[userId]", input.userId);
  params.set("metadata[checkoutSku]", input.sku ?? "legacy_subscription");
  params.set("metadata[priceId]", priceId);
  if (mode === "subscription") {
    params.set("subscription_data[metadata][clerkUserId]", input.clerkUserId);
    params.set("subscription_data[metadata][userId]", input.userId);
    params.set(
      "subscription_data[metadata][checkoutSku]",
      input.sku ?? "legacy_subscription",
    );
    params.set("subscription_data[metadata][priceId]", priceId);
  }
  params.set("line_items[0][price]", priceId);
  params.set("line_items[0][quantity]", "1");
  params.set("success_url", input.successUrl);
  params.set("cancel_url", input.cancelUrl);

  return params;
}

export async function createStripePortalSession(input: {
  customerId: string;
  returnUrl: string;
}) {
  const params = new URLSearchParams();
  params.set("customer", input.customerId);
  params.set("return_url", input.returnUrl);

  return postStripeForm<Stripe.BillingPortal.Session>(
    "/billing_portal/sessions",
    params,
  );
}

export function retrieveStripeSubscription(subscriptionId: string) {
  return stripeFetch<Stripe.Subscription>(`/subscriptions/${subscriptionId}`);
}

export function getSubscriptionPeriodEnd(
  subscription: Stripe.Subscription,
): Date | null {
  const subscriptionAny = subscription as Stripe.Subscription & {
    items?: {
      data?: Array<{
        current_period_end?: number;
        current_period_start?: number;
      }>;
    };
    current_period_end?: number;
  };
  const itemPeriodEnd = subscriptionAny.items?.data?.[0]?.current_period_end;
  const legacyPeriodEnd = subscriptionAny.current_period_end;
  const periodEnd = itemPeriodEnd ?? legacyPeriodEnd;

  return typeof periodEnd === "number" ? new Date(periodEnd * 1000) : null;
}

export function getSubscriptionPriceId(
  subscription: Stripe.Subscription,
): string | null {
  const price = subscription.items.data.at(0)?.price;
  return price?.id ?? null;
}

export function getSubscriptionPlanTier(subscription: Stripe.Subscription) {
  return getPlanTierForPriceId(getSubscriptionPriceId(subscription)) ?? "free";
}

function stripeId(value: string | { id: string } | null | undefined) {
  if (!value) {
    return null;
  }

  return typeof value === "string" ? value : value.id;
}

export async function applySubscriptionToUser(
  subscription: Stripe.Subscription,
  options: {
    clerkUserId?: string | null;
  } = {},
) {
  const customerId = stripeId(subscription.customer);
  const clerkUserId =
    options.clerkUserId ?? subscription.metadata?.clerkUserId ?? null;
  const user =
    (clerkUserId ? await findUserByClerkId(clerkUserId) : null) ??
    (customerId ? await findUserByStripeCustomerId(customerId) : null);

  if (!user) {
    return null;
  }

  const sql = getSql();
  const rows = (await sql`
    /* stripe:apply_subscription_to_user */
    UPDATE "User"
    SET
      "stripeCustomerId" = ${customerId},
      "stripeSubscriptionId" = ${subscription.id},
      "stripePriceId" = ${getSubscriptionPriceId(subscription)},
      "subscriptionStatus" = ${subscription.status},
      "currentPeriodEnd" = ${getSubscriptionPeriodEnd(subscription)},
      "planTier" = ${getSubscriptionPlanTier(subscription)},
      "updatedAt" = now()
    WHERE "id" = ${user.id}
    RETURNING *
  `) as Parameters<typeof mapUser>[0][];

  return rows[0] ? mapUser(rows[0]) : null;
}

export async function markSubscriptionInactive(subscription: Stripe.Subscription) {
  const customerId = stripeId(subscription.customer);

  const user = customerId ? await findUserByStripeCustomerId(customerId) : null;

  if (!user) {
    return null;
  }

  const sql = getSql();
  const rows = (await sql`
    UPDATE "User"
    SET
      "stripeCustomerId" = ${customerId},
      "stripeSubscriptionId" = ${subscription.id},
      "stripePriceId" = ${getSubscriptionPriceId(subscription)},
      "subscriptionStatus" = 'canceled',
      "currentPeriodEnd" = ${getSubscriptionPeriodEnd(subscription)},
      "planTier" = 'free',
      "updatedAt" = now()
    WHERE "id" = ${user.id}
    RETURNING *
  `) as Parameters<typeof mapUser>[0][];

  return rows[0] ? mapUser(rows[0]) : null;
}

async function resolveCreditUserId(input: {
  userId?: string | null;
  clerkUserId?: string | null;
  customerId?: string | null;
}) {
  if (input.userId) {
    return input.userId;
  }

  const user =
    (input.clerkUserId ? await findUserByClerkId(input.clerkUserId) : null) ??
    (input.customerId ? await findUserByStripeCustomerId(input.customerId) : null);

  return user?.id ?? null;
}

export type RewriteCreditGrantResult = {
  granted: boolean;
  credit: {
    id: string;
    userId: string;
    source: StripeCreditSource;
    amountGranted: number;
    amountConsumed: number;
    grantedAt: Date;
    expiresAt: Date | null;
    stripeEventId: string;
  } | null;
};

export async function grantRewriteCredit(input: {
  userId: string;
  grant: StripeCreditGrant;
  stripeEventId: string;
}): Promise<RewriteCreditGrantResult> {
  const sql = getSql();
  const rows = (await sql`
    /* stripe:grant_rewrite_credit */
    WITH existing AS (
      SELECT
        "id",
        "userId",
        "source",
        "amountGranted",
        "amountConsumed",
        "grantedAt",
        "expiresAt",
        "stripeEventId",
        false AS "wasInserted"
      FROM "RewriteCredit"
      WHERE "stripeEventId" = ${input.stripeEventId}
      LIMIT 1
    ),
    inserted AS (
      INSERT INTO "RewriteCredit" (
        "id",
        "userId",
        "source",
        "amountGranted",
        "amountConsumed",
        "grantedAt",
        "expiresAt",
        "stripeEventId"
      )
      SELECT
        ${createId()},
        ${input.userId},
        ${input.grant.source},
        ${input.grant.amountGranted},
        0,
        now(),
        ${input.grant.expiresAt},
        ${input.stripeEventId}
      WHERE NOT EXISTS (SELECT 1 FROM existing)
      RETURNING
        "id",
        "userId",
        "source",
        "amountGranted",
        "amountConsumed",
        "grantedAt",
        "expiresAt",
        "stripeEventId",
        true AS "wasInserted"
    )
    SELECT * FROM inserted
    UNION ALL
    SELECT * FROM existing
    LIMIT 1
  `) as Array<{
    id: string;
    userId: string;
    source: StripeCreditSource;
    amountGranted: number;
    amountConsumed: number;
    grantedAt: unknown;
    expiresAt: unknown;
    stripeEventId: string;
    wasInserted: boolean;
  }>;

  const row = rows[0];
  if (!row) {
    return { granted: false, credit: null };
  }

  return {
    granted: row.wasInserted,
    credit: {
      ...row,
      grantedAt: requiredDate(row.grantedAt),
      expiresAt: row.expiresAt ? requiredDate(row.expiresAt) : null,
    },
  };
}

export async function grantCheckoutSessionRewriteCredit(
  session: Stripe.Checkout.Session,
  eventId: string,
  now = new Date(),
) {
  if (session.mode !== "payment") {
    return { granted: false, credit: null } satisfies RewriteCreditGrantResult;
  }

  const priceId = session.metadata?.priceId ?? null;
  const grant = getCreditGrantForPriceId(priceId, now);
  if (!grant) {
    return { granted: false, credit: null } satisfies RewriteCreditGrantResult;
  }

  const userId = await resolveCreditUserId({
    userId: session.metadata?.userId,
    clerkUserId: session.client_reference_id ?? session.metadata?.clerkUserId,
    customerId: stripeId(session.customer),
  });
  if (!userId) {
    throw new Error("Cannot grant Stripe checkout credit without a matched user.");
  }

  return grantRewriteCredit({
    userId,
    grant,
    stripeEventId: eventId,
  });
}

export function getFirstMonthBonusGrant(
  subscription: Stripe.Subscription,
): StripeCreditGrant | null {
  const tier = getPlanTierForPriceId(getSubscriptionPriceId(subscription));
  if (!tier) {
    return null;
  }

  return {
    source: "first_month_bonus",
    amountGranted: FIRST_MONTH_BONUS_BY_TIER[tier],
    expiresAt: null,
  };
}

export async function grantFirstMonthBonusCredit(input: {
  invoice: Stripe.Invoice;
  subscription: Stripe.Subscription;
  now?: Date;
}) {
  if (input.invoice.billing_reason !== "subscription_create") {
    return { granted: false, credit: null } satisfies RewriteCreditGrantResult;
  }

  const grant = getFirstMonthBonusGrant(input.subscription);
  if (!grant) {
    return { granted: false, credit: null } satisfies RewriteCreditGrantResult;
  }

  const userId = await resolveCreditUserId({
    userId: input.subscription.metadata?.userId,
    clerkUserId: input.subscription.metadata?.clerkUserId,
    customerId: stripeId(input.subscription.customer),
  });
  if (!userId) {
    throw new Error("Cannot grant first-month bonus without a matched user.");
  }

  return grantRewriteCredit({
    userId,
    grant,
    stripeEventId: `first_month_bonus:${input.invoice.id}`,
  });
}
