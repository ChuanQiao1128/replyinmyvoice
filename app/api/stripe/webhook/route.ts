import { NextResponse } from "next/server";
import type Stripe from "stripe";

import { isProduction, optionalEnv, requireEnv } from "../../../../lib/env";
import { getStripe, applySubscriptionToUser, markSubscriptionInactive } from "../../../../lib/stripe";

export const dynamic = "force-dynamic";

function stripeObjectId(value: string | { id: string } | null | undefined) {
  if (!value) {
    return null;
  }

  return typeof value === "string" ? value : value.id;
}

async function markEventProcessed(event: Stripe.Event) {
  const { prisma } = await import("../../../../lib/db");
  try {
    await prisma.stripeEvent.create({
      data: {
        id: event.id,
        type: event.type,
      },
    });
    return true;
  } catch (error) {
    if (
      typeof error === "object" &&
      error !== null &&
      "code" in error &&
      error.code === "P2002"
    ) {
      return false;
    }
    throw error;
  }
}

async function updateCustomerFromSession(session: Stripe.Checkout.Session) {
  const { prisma } = await import("../../../../lib/db");
  const clerkUserId =
    session.client_reference_id ?? session.metadata?.clerkUserId ?? null;
  const customerId = stripeObjectId(session.customer);

  if (clerkUserId && customerId) {
    await prisma.user.updateMany({
      where: { clerkUserId },
      data: { stripeCustomerId: customerId },
    });
  }

  const subscriptionId = stripeObjectId(session.subscription);
  if (subscriptionId) {
    const subscription = await getStripe().subscriptions.retrieve(subscriptionId);
    await applySubscriptionToUser(subscription, { clerkUserId });
  }
}

async function updateFromInvoice(invoice: Stripe.Invoice) {
  const subscriptionId = stripeObjectId(invoice.subscription);
  if (!subscriptionId) {
    return;
  }

  const subscription = await getStripe().subscriptions.retrieve(subscriptionId);
  await applySubscriptionToUser(subscription);
}

async function handleStripeEvent(event: Stripe.Event) {
  switch (event.type) {
    case "checkout.session.completed":
      await updateCustomerFromSession(
        event.data.object as Stripe.Checkout.Session,
      );
      break;
    case "customer.subscription.created":
    case "customer.subscription.updated":
      await applySubscriptionToUser(event.data.object as Stripe.Subscription);
      break;
    case "customer.subscription.deleted":
      await markSubscriptionInactive(event.data.object as Stripe.Subscription);
      break;
    case "invoice.paid":
    case "invoice.payment_failed":
      await updateFromInvoice(event.data.object as Stripe.Invoice);
      break;
    default:
      break;
  }
}

async function parseStripeEvent(request: Request) {
  const stripe = getStripe();
  const rawBody = await request.text();
  const signature = request.headers.get("stripe-signature");
  const webhookSecret = optionalEnv("STRIPE_WEBHOOK_SECRET");

  if (!webhookSecret) {
    if (isProduction()) {
      throw new Error("Stripe webhook secret is required in production.");
    }
    return JSON.parse(rawBody) as Stripe.Event;
  }

  if (!signature) {
    throw new Error("Missing Stripe signature.");
  }

  return stripe.webhooks.constructEventAsync(
    rawBody,
    signature,
    requireEnv("STRIPE_WEBHOOK_SECRET"),
  );
}

export async function POST(request: Request) {
  let event: Stripe.Event;

  try {
    event = await parseStripeEvent(request);
  } catch {
    return NextResponse.json({ error: "Invalid webhook event." }, { status: 400 });
  }

  const shouldProcess = await markEventProcessed(event);
  if (shouldProcess) {
    await handleStripeEvent(event);
  }

  return NextResponse.json({ received: true });
}

export async function GET() {
  return NextResponse.json({ ok: true, endpoint: "stripe-webhook" });
}
