import Stripe from "stripe";

import { getSql } from "./db";
import { getAppUrl, requireEnv } from "./env";
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
}) {
  const { successUrl, cancelUrl } = getCheckoutUrls();
  const params = new URLSearchParams();
  params.set("mode", "subscription");
  params.set("customer", input.customerId);
  params.set("client_reference_id", input.clerkUserId);
  params.set("metadata[clerkUserId]", input.clerkUserId);
  params.set("metadata[userId]", input.userId);
  params.set("subscription_data[metadata][clerkUserId]", input.clerkUserId);
  params.set("subscription_data[metadata][userId]", input.userId);
  params.set("line_items[0][price]", getStripePriceId());
  params.set("line_items[0][quantity]", "1");
  params.set("success_url", successUrl);
  params.set("cancel_url", cancelUrl);

  return postStripeForm<Stripe.Checkout.Session>("/checkout/sessions", params);
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
    UPDATE "User"
    SET
      "stripeCustomerId" = ${customerId},
      "stripeSubscriptionId" = ${subscription.id},
      "stripePriceId" = ${getSubscriptionPriceId(subscription)},
      "subscriptionStatus" = ${subscription.status},
      "currentPeriodEnd" = ${getSubscriptionPeriodEnd(subscription)},
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
      "updatedAt" = now()
    WHERE "id" = ${user.id}
    RETURNING *
  `) as Parameters<typeof mapUser>[0][];

  return rows[0] ? mapUser(rows[0]) : null;
}
