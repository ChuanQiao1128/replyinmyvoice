import { NextResponse } from "next/server";

import { requireSameOrigin } from "../../../../lib/http";
import {
  capturePaymentError,
  capturePaymentEvent,
  createPaymentCorrelationId,
  initializePaymentObservability,
  type PaymentEventProperties,
  type PaymentFunnelEvent,
} from "../../../../lib/payment-observability";

export const dynamic = "force-dynamic";

const allowedEvents = new Set<PaymentFunnelEvent>([
  "checkout_started",
  "checkout_redirected",
  "payment_succeeded",
  "payment_failed",
  "webhook_failed",
]);

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  initializePaymentObservability();

  let payload: unknown;
  try {
    payload = await request.json();
  } catch {
    return NextResponse.json({ error: "Invalid event payload." }, { status: 400 });
  }

  if (!isPaymentEventPayload(payload)) {
    return NextResponse.json({ error: "Invalid event payload." }, { status: 400 });
  }

  const incomingProperties = payload.properties ?? {};
  const correlationId = textValue(incomingProperties.correlationId) ??
    createPaymentCorrelationId("browser");
  const properties = sanitizeClientProperties({
    ...incomingProperties,
    correlationId,
  });

  await capturePaymentEvent(payload.event, properties);

  if (payload.event === "payment_failed") {
    await capturePaymentError(new Error("Browser payment flow failed"), {
      correlationId,
      event: "payment_failed",
      properties,
      source: "browser_payment_flow",
    });
  }

  return new NextResponse(null, { status: 204 });
}

function isPaymentEventPayload(payload: unknown): payload is {
  event: PaymentFunnelEvent;
  properties: PaymentEventProperties;
} {
  if (!payload || typeof payload !== "object" || Array.isArray(payload)) {
    return false;
  }

  const candidate = payload as Record<string, unknown>;
  if (typeof candidate.event !== "string" || !allowedEvents.has(candidate.event as PaymentFunnelEvent)) {
    return false;
  }

  return !("properties" in candidate) ||
    candidate.properties === undefined ||
    (typeof candidate.properties === "object" && !Array.isArray(candidate.properties));
}

function sanitizeClientProperties(
  properties: Record<string, unknown>,
): PaymentEventProperties {
  return Object.fromEntries(
    Object.entries(properties).flatMap(([key, value]) => {
      if (
        typeof value === "string" ||
        typeof value === "number" ||
        typeof value === "boolean" ||
        value === null
      ) {
        return [[key, value]];
      }

      return [];
    }),
  );
}

function textValue(value: unknown) {
  return typeof value === "string" && value.trim() ? value.trim() : null;
}
