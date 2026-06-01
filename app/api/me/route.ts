import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../lib/azure-api";
import { getCurrentAccessToken } from "../../../lib/entra-auth";
import { jsonError, requireSameOrigin } from "../../../lib/http";
import {
  capturePaymentError,
  createPaymentCorrelationId,
  initializePaymentObservability,
} from "../../../lib/payment-observability";

export const dynamic = "force-dynamic";

async function forwardAzureResponse(response: Response) {
  const headers = new Headers();
  const contentType = response.headers.get("content-type");
  if (contentType) {
    headers.set("Content-Type", contentType);
  }

  if (response.status === 204) {
    return new NextResponse(null, {
      headers,
      status: response.status,
    });
  }

  return new NextResponse(await response.text(), {
    headers,
    status: response.status,
  });
}

const authScheme = "Bearer";

async function currentAuthorizationHeader() {
  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    return null;
  }

  return `${authScheme} ${accessToken}`;
}

export async function GET() {
  initializePaymentObservability();

  const authorization = await currentAuthorizationHeader();
  if (!authorization) {
    return jsonError("Authentication required.", 401);
  }

  const correlationId = createPaymentCorrelationId("account");
  let response: Response;
  try {
    response = await fetch(`${getAzureApiBaseUrl()}/api/me`, {
      cache: "no-store",
      headers: {
        Authorization: authorization,
        "X-Correlation-Id": correlationId,
      },
    });
  } catch (error) {
    await captureAccountProxyFailure(error, correlationId, "GET");
    return jsonError("Account request failed.", 502);
  }

  if (response.status >= 500) {
    await captureAccountProxyFailure(
      new Error(`Account proxy failed with ${response.status}`),
      correlationId,
      "GET",
      response.status,
    );
  }

  return forwardAzureResponse(response);
}

export async function DELETE(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const authorization = await currentAuthorizationHeader();
  if (!authorization) {
    return jsonError("Authentication required.", 401);
  }

  const correlationId = createPaymentCorrelationId("account");
  let response: Response;
  try {
    response = await fetch(`${getAzureApiBaseUrl()}/api/me`, {
      cache: "no-store",
      headers: {
        Authorization: authorization,
        "X-Correlation-Id": correlationId,
      },
      method: "DELETE",
    });
  } catch (error) {
    await captureAccountProxyFailure(error, correlationId, "DELETE");
    return jsonError("Account request failed.", 502);
  }

  if (response.status >= 500) {
    await captureAccountProxyFailure(
      new Error(`Account proxy failed with ${response.status}`),
      correlationId,
      "DELETE",
      response.status,
    );
  }

  return forwardAzureResponse(response);
}

async function captureAccountProxyFailure(
  error: unknown,
  correlationId: string,
  method: "DELETE" | "GET",
  status?: number,
) {
  await capturePaymentError(error, {
    correlationId,
    event: "account_proxy_failed",
    properties: {
      correlationId,
      method,
      source: "account_proxy",
      status: status ?? null,
    },
    source: "account_proxy",
    status,
  });
}
