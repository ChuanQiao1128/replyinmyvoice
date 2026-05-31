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

  const correlationId = createPaymentCorrelationId("checkout");
  const body = await request.text();
  const sku = readCheckoutSku(body);

  try {
    const response = await fetch(`${getAzureApiBaseUrl()}/api/stripe/checkout`, {
      body,
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${accessToken}`,
        "Content-Type": request.headers.get("content-type") ?? "application/json",
        "X-Correlation-Id": correlationId,
      },
      method: "POST",
    });
    const responseText = await response.text();
    const properties = {
      correlationId,
      sku,
      source: "checkout_proxy",
      status: response.status,
    };

    if (response.ok) {
      await capturePaymentEvent("checkout_redirected", properties);
    } else {
      await capturePaymentEvent("payment_failed", properties);
      await capturePaymentError(new Error(`Checkout proxy failed with ${response.status}`), {
        correlationId,
        event: "checkout_proxy_failed",
        properties,
        source: "checkout_proxy",
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
      sku,
      source: "checkout_proxy",
    };
    await capturePaymentEvent("payment_failed", properties);
    await capturePaymentError(error, {
      correlationId,
      event: "checkout_proxy_failed",
      properties,
      source: "checkout_proxy",
    });
    return jsonError("Could not start checkout.", 502);
  }
}

function readCheckoutSku(body: string) {
  try {
    const parsed = JSON.parse(body) as unknown;
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
      return null;
    }

    const sku = (parsed as Record<string, unknown>).sku;
    return typeof sku === "string" && sku.trim() ? sku.trim() : null;
  } catch {
    return null;
  }
}
