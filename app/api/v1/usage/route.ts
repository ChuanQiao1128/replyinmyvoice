import { NextResponse } from "next/server";

import {
  captureApiEvent,
  parseApiErrorCode,
  readApiRequestId,
} from "../../../../lib/api-observability";
import { getAzureApiBaseUrl } from "../../../../lib/azure-api";
import { copyV1ResponseHeaders } from "../../../../lib/v1-response-headers";

export const dynamic = "force-dynamic";

const endpoint = "GET /api/v1/usage";

async function forwardAzureResponse(response: Response, startedAtMs: number) {
  const headers = new Headers();
  const contentType = response.headers.get("content-type");
  if (contentType) {
    headers.set("Content-Type", contentType);
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

export async function GET(request: Request) {
  const startedAtMs = Date.now();
  const authorization = request.headers.get("authorization");
  const headers: Record<string, string> = {};
  if (authorization) {
    headers.Authorization = authorization;
  }

  try {
    const response = await fetch(`${getAzureApiBaseUrl()}/api/v1/usage`, {
      cache: "no-store",
      headers,
    });

    return forwardAzureResponse(response, startedAtMs);
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
