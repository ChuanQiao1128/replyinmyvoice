import { NextResponse } from "next/server";
import type Stripe from "stripe";

import { isProduction, optionalEnv, requireEnv } from "../../../../lib/env";
import { getSql } from "../../../../lib/db";
import {
  getStripe,
  applySubscriptionToUser,
  markSubscriptionInactive,
  retrieveStripeSubscription,
} from "../../../../lib/stripe";
import {
  buildStripeEventUpdate,
  safeStripeEventError,
} from "../../../../lib/stripe-events";
import { updateStripeCustomerByClerkId } from "../../../../lib/users";

export const dynamic = "force-dynamic";

function stripeObjectId(value: string | { id: string } | null | undefined) {
  if (!value) {
    return null;
  }

  return typeof value === "string" ? value : value.id;
}

async function claimStripeEvent(event: Stripe.Event) {
  const sql = getSql();
  const rows = (await sql`
    INSERT INTO "StripeEvent" (
      "id",
      "type",
      "status",
      "attemptCount",
      "stripeMode",
      "createdAt",
      "updatedAt"
    )
    VALUES (
      ${event.id},
      ${event.type},
      'processing',
      1,
      ${event.livemode ? "live" : "test"},
      now(),
      now()
    )
    ON CONFLICT ("id") DO UPDATE SET
      "status" = 'processing',
      "attemptCount" = "StripeEvent"."attemptCount" + 1,
      "lastError" = NULL,
      "updatedAt" = now()
    WHERE "StripeEvent"."status" = 'failed'
    RETURNING "id"
  `) as Array<{ id: string }>;

  return rows.length === 1;
}

async function markStripeEventProcessed(event: Stripe.Event) {
  const sql = getSql();
  const update = buildStripeEventUpdate({
    status: "processed",
    now: new Date(),
  });

  await sql`
    UPDATE "StripeEvent"
    SET
      "status" = ${update.status},
      "processedAt" = ${update.processedAt},
      "failedAt" = ${update.failedAt},
      "lastError" = ${update.lastError},
      "updatedAt" = now()
    WHERE "id" = ${event.id}
  `;
}

async function markStripeEventFailed(event: Stripe.Event, error: unknown) {
  const sql = getSql();
  const update = buildStripeEventUpdate({
    status: "failed",
    error,
    now: new Date(),
  });

  await sql`
    UPDATE "StripeEvent"
    SET
      "status" = ${update.status},
      "processedAt" = ${update.processedAt},
      "failedAt" = ${update.failedAt},
      "lastError" = ${update.lastError},
      "updatedAt" = now()
    WHERE "id" = ${event.id}
  `;
}

async function updateCustomerFromSession(session: Stripe.Checkout.Session) {
  const clerkUserId =
    session.client_reference_id ?? session.metadata?.clerkUserId ?? null;
  const customerId = stripeObjectId(session.customer);

  if (clerkUserId && customerId) {
    await updateStripeCustomerByClerkId(clerkUserId, customerId);
  }

  const subscriptionId = stripeObjectId(session.subscription);
  if (subscriptionId) {
    const subscription = await retrieveStripeSubscription(subscriptionId);
    await applySubscriptionToUser(subscription, { clerkUserId });
  }
}

async function updateFromInvoice(invoice: Stripe.Invoice) {
  const subscriptionId = stripeObjectId(invoice.subscription);
  if (!subscriptionId) {
    return;
  }

  const subscription = await retrieveStripeSubscription(subscriptionId);
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

  const shouldProcess = await claimStripeEvent(event);
  if (!shouldProcess) {
    return NextResponse.json({ received: true, duplicate: true });
  }

  try {
    await handleStripeEvent(event);
    await markStripeEventProcessed(event);
  } catch (error) {
    await markStripeEventFailed(event, error);
    console.error("stripe_webhook_processing_failed", {
      eventId: event.id,
      type: event.type,
      message: safeStripeEventError(error),
    });
    return NextResponse.json(
      { error: "Webhook event processing failed." },
      { status: 500 },
    );
  }

  return NextResponse.json({ received: true });
}

export async function GET() {
  return NextResponse.json({ ok: true, endpoint: "stripe-webhook" });
}
