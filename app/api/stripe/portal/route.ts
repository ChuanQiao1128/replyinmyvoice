import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../lib/azure-api";
import { getCurrentAccessToken } from "../../../../lib/entra-auth";
import { jsonError, requireSameOrigin } from "../../../../lib/http";
import {
  capturePaymentError,
  capturePaymentEvent,
  createPaymentCorrelationId,
  initializePaymentObservability,
} from "../../../../lib/payment-observability";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  initializePaymentObservability();

  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    return jsonError("Authentication required.", 401);
  }

  const correlationId = createPaymentCorrelationId("portal");

  try {
    const response = await fetch(`${getAzureApiBaseUrl()}/api/stripe/portal`, {
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "X-Correlation-Id": correlationId,
      },
      method: "POST",
    });
    const responseText = await response.text();

    if (!response.ok) {
      const properties = {
        correlationId,
        source: "billing_portal_proxy",
        status: response.status,
      };
      await capturePaymentEvent("payment_failed", properties);
      await capturePaymentError(new Error(`Billing portal proxy failed with ${response.status}`), {
        correlationId,
        event: "billing_portal_failed",
        properties,
        source: "billing_portal_proxy",
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
      source: "billing_portal_proxy",
    };
    await capturePaymentEvent("payment_failed", properties);
    await capturePaymentError(error, {
      correlationId,
      event: "billing_portal_failed",
      properties,
      source: "billing_portal_proxy",
    });
    return jsonError("Could not open billing portal.", 502);
  }
}
