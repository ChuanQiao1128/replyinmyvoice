import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../lib/azure-api";
import {
  capturePaymentError,
  capturePaymentEvent,
  createPaymentCorrelationId,
  initializePaymentObservability,
} from "../../../../lib/payment-observability";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  initializePaymentObservability();

  const signature = request.headers.get("stripe-signature");
  const rawBody = await request.text();
  const stripeEvent = readStripeEvent(rawBody);
  const correlationId = stripeEvent.id ?? createPaymentCorrelationId("webhook");

  try {
    const response = await fetch(`${getAzureApiBaseUrl()}/api/stripe/webhook`, {
      body: rawBody,
      cache: "no-store",
      headers: {
        "Content-Type": request.headers.get("content-type") ?? "application/json",
        "X-Correlation-Id": correlationId,
        ...(signature ? { "stripe-signature": signature } : {}),
      },
      method: "POST",
    });
    const responseText = await response.text();
    const properties = {
      correlationId,
      source: "webhook_proxy",
      status: response.status,
      stripeEventType: stripeEvent.type,
    };

    if (response.ok) {
      if (stripeEvent.type === "checkout.session.completed") {
        await capturePaymentEvent("payment_succeeded", properties);
      }
      if (stripeEvent.type === "invoice.payment_failed") {
        await capturePaymentEvent("payment_failed", properties);
      }
    } else {
      await capturePaymentEvent("webhook_failed", properties);
      await capturePaymentError(new Error(`Stripe webhook proxy failed with ${response.status}`), {
        correlationId,
        event: "webhook_proxy_failed",
        properties,
        source: "webhook_proxy",
        status: response.status,
      });
    }

    return new NextResponse(responseText, {
      status: response.status,
      headers: {
        "Content-Type": response.headers.get("content-type") ?? "application/json",
      },
    });
  } catch (error) {
    const properties = {
      correlationId,
      source: "webhook_proxy",
      stripeEventType: stripeEvent.type,
    };
    await capturePaymentEvent("webhook_failed", properties);
    await capturePaymentError(error, {
      correlationId,
      event: "webhook_proxy_failed",
      properties,
      source: "webhook_proxy",
    });
    return NextResponse.json({ error: "Webhook proxy failed." }, { status: 502 });
  }
}

export async function GET() {
  return NextResponse.json({ backend: "azure-functions" });
}

function readStripeEvent(body: string): { id: string | null; type: string | null } {
  try {
    const parsed = JSON.parse(body) as unknown;
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
      return { id: null, type: null };
    }

    const event = parsed as Record<string, unknown>;
    return {
      id: typeof event.id === "string" && event.id.trim() ? event.id.trim() : null,
      type: typeof event.type === "string" && event.type.trim() ? event.type.trim() : null,
    };
  } catch {
    return { id: null, type: null };
  }
}
