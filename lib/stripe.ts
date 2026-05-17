import Stripe from "stripe";

import { getAppUrl, requireEnv } from "./env";

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
  const { prisma } = await import("./db");
  const customerId = stripeId(subscription.customer);
  const clerkUserId =
    options.clerkUserId ?? subscription.metadata?.clerkUserId ?? null;
  const user =
    (clerkUserId
      ? await prisma.user.findUnique({ where: { clerkUserId } })
      : null) ??
    (customerId
      ? await prisma.user.findFirst({ where: { stripeCustomerId: customerId } })
      : null);

  if (!user) {
    return null;
  }

  return prisma.user.update({
    where: {
      id: user.id,
    },
    data: {
      stripeCustomerId: customerId,
      stripeSubscriptionId: subscription.id,
      stripePriceId: getSubscriptionPriceId(subscription),
      subscriptionStatus: subscription.status,
      currentPeriodEnd: getSubscriptionPeriodEnd(subscription),
    },
  });
}

export async function markSubscriptionInactive(subscription: Stripe.Subscription) {
  const { prisma } = await import("./db");
  const customerId = stripeId(subscription.customer);

  const user = customerId
    ? await prisma.user.findFirst({ where: { stripeCustomerId: customerId } })
    : null;

  if (!user) {
    return null;
  }

  return prisma.user.update({
    where: {
      id: user.id,
    },
    data: {
      stripeCustomerId: customerId,
      stripeSubscriptionId: subscription.id,
      stripePriceId: getSubscriptionPriceId(subscription),
      subscriptionStatus: "canceled",
      currentPeriodEnd: getSubscriptionPeriodEnd(subscription),
    },
  });
}
