import { NextResponse } from "next/server";

import {
  createStripeCheckoutSession,
  createStripeCustomer,
} from "../../../../lib/stripe";
import { jsonError, requireSameOrigin } from "../../../../lib/http";
import {
  getCurrentAppUser,
  updateUserStripeCustomer,
} from "../../../../lib/users";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
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

  const session = await createStripeCheckoutSession({
    customerId,
    clerkUserId: user.clerkUserId,
    userId: user.id,
  });

  if (!session.url) {
    return jsonError("Could not create checkout session.", 500);
  }

  return NextResponse.json({ url: session.url });
}
