import { auth } from "@clerk/nextjs/server";
import { NextResponse } from "next/server";

import { getAppUrl } from "../../../../lib/env";
import { jsonError, requireSameOrigin } from "../../../../lib/http";
import { getStripe } from "../../../../lib/stripe";
import { getCurrentAppUser } from "../../../../lib/users";

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
  if (!user?.stripeCustomerId) {
    return jsonError("Billing customer not found.", 400);
  }

  const stripe = getStripe();
  const session = await stripe.billingPortal.sessions.create({
    customer: user.stripeCustomerId,
    return_url: `${getAppUrl()}/app`,
  });

  return NextResponse.json({ url: session.url });
}
