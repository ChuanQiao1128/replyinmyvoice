import { NextResponse } from "next/server";

import {
  createStripeCheckoutSession,
  createStripeCustomer,
  type StripeCheckoutSku,
} from "../../../../lib/stripe";
import { jsonError, requireSameOrigin } from "../../../../lib/http";
import {
  getCurrentAppUser,
  updateUserStripeCustomer,
} from "../../../../lib/users";

export const dynamic = "force-dynamic";

const checkoutSkus = new Set(["starter", "pro", "exam_pass", "top_up"]);

async function requestedCheckoutSku(request: Request) {
  const payload = (await request.json().catch(() => null)) as {
    sku?: unknown;
    plan?: unknown;
  } | null;
  const requested = payload?.sku ?? payload?.plan ?? null;

  if (requested == null || requested === "") {
    return { ok: true as const, sku: null };
  }

  if (typeof requested !== "string" || !checkoutSkus.has(requested)) {
    return { ok: false as const };
  }

  return { ok: true as const, sku: requested as StripeCheckoutSku };
}

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const requestedSku = await requestedCheckoutSku(request);
  if (!requestedSku.ok) {
    return jsonError("Invalid checkout plan.", 400);
  }

  const user = await getCurrentAppUser();
  if (!user) {
    return jsonError("Authentication required.", 401);
  }

  let customerId = user.stripeCustomerId;

  if (!customerId) {
    const customer = await createStripeCustomer({
      email: user.email,
      clerkUserId: user.clerkUserId,
      userId: user.id,
    });
    customerId = customer.id;

    await updateUserStripeCustomer(user.id, customerId);
  }

  let session;
  try {
    session = await createStripeCheckoutSession({
      customerId,
      clerkUserId: user.clerkUserId,
      userId: user.id,
      sku: requestedSku.sku,
    });
  } catch (error) {
    if (
      error instanceof Error &&
      error.message.includes("Missing Stripe price environment variable")
    ) {
      return jsonError("Checkout plan is not configured.", 503);
    }
    throw error;
  }

  if (!session.url) {
    return jsonError("Could not create checkout session.", 500);
  }

  return NextResponse.json({ url: session.url });
}
