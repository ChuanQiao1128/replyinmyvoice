import { NextResponse } from "next/server";

import {
  captureApiEvent,
  parseApiErrorCode,
  readApiRequestId,
} from "../../../../lib/api-observability";
import { getAzureApiBaseUrl } from "../../../../lib/azure-api";
import { copyV1ResponseHeaders } from "../../../../lib/v1-response-headers";

export const dynamic = "force-dynamic";

const endpoint = "POST /api/v1/rewrite";

function callerHeaders(request: Request) {
  const headers = new Headers();
  const authorization = request.headers.get("authorization");
  const contentType = request.headers.get("content-type");
  const idempotencyKey = request.headers.get("idempotency-key");

  if (authorization !== null) {
    headers.set("Authorization", authorization);
  }
  headers.set("Content-Type", contentType ?? "application/json");
  if (idempotencyKey !== null) {
    headers.set("Idempotency-Key", idempotencyKey);
  }

  return headers;
}

async function forwardFunctionsResponse(response: Response, startedAtMs: number) {
  const headers = new Headers();
  const contentType = response.headers.get("content-type");
  const location = response.headers.get("location");

  if (contentType) {
    headers.set("Content-Type", contentType);
  }
  if (location) {
    headers.set("Location", location);
  }
  copyV1ResponseHeaders(response.headers, headers);

  const responseText = await response.text();
  void captureApiEvent({
    endpoint,
    errorCode: response.status >= 400 ? parseApiErrorCode(responseText) : undefined,
    latencyMs: Date.now() - startedAtMs,
    requestId: readApiRequestId(response.headers),
    statusCode: response.status,
  });

  return new NextResponse(responseText, {
    headers,
    status: response.status,
  });
}

export async function POST(request: Request) {
  const startedAtMs = Date.now();

  try {
    const response = await fetch(`${getAzureApiBaseUrl()}/api/v1/rewrite`, {
      body: await request.text(),
      cache: "no-store",
      headers: callerHeaders(request),
      method: "POST",
    });

    return forwardFunctionsResponse(response, startedAtMs);
  } catch (error) {
    void captureApiEvent({
      endpoint,
      error,
      errorCode: "proxy_request_failed",
      latencyMs: Date.now() - startedAtMs,
      statusCode: 500,
    });
    throw error;
  }
}
