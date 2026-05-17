import { auth } from "@clerk/nextjs/server";
import { NextResponse } from "next/server";

import { getCheckoutUrls, getStripe, getStripePriceId } from "../../../../lib/stripe";
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

  const { userId } = await auth();
  if (!userId) {
    return jsonError("Authentication required.", 401);
  }

  const user = await getCurrentAppUser();
  if (!user) {
    return jsonError("Authentication required.", 401);
  }

  const stripe = getStripe();
  let customerId = user.stripeCustomerId;

  if (!customerId) {
    const customer = await stripe.customers.create({
      email: user.email ?? undefined,
      metadata: {
        clerkUserId: user.clerkUserId,
        userId: user.id,
      },
    });
    customerId = customer.id;

    await updateUserStripeCustomer(user.id, customerId);
  }

  const { successUrl, cancelUrl } = getCheckoutUrls();
  const session = await stripe.checkout.sessions.create({
    mode: "subscription",
    customer: customerId,
    client_reference_id: user.clerkUserId,
    metadata: {
      clerkUserId: user.clerkUserId,
      userId: user.id,
    },
    subscription_data: {
      metadata: {
        clerkUserId: user.clerkUserId,
        userId: user.id,
      },
    },
    line_items: [
      {
        price: getStripePriceId(),
        quantity: 1,
      },
    ],
    success_url: successUrl,
    cancel_url: cancelUrl,
  });

  if (!session.url) {
    return jsonError("Could not create checkout session.", 500);
  }

  return NextResponse.json({ url: session.url });
}
