import { NextResponse } from "next/server";

import {
  parseApiErrorCode,
  readApiRequestId,
  scheduleApiEvent,
} from "../../../../../lib/api-observability";
import { getAzureApiBaseUrl } from "../../../../../lib/azure-api";
import { copyV1ResponseHeaders } from "../../../../../lib/v1-response-headers";

export const dynamic = "force-dynamic";

const endpoint = "GET /api/v1/rewrite/{id}";
const proxyRequestFailedBody = {
  error: {
    code: "proxy_request_failed",
    message: "The API service could not be reached. Please try again.",
  },
};

type RouteContext = {
  params: Promise<{ id: string }>;
};

function callerHeaders(request: Request) {
  const headers = new Headers();
  const authorization = request.headers.get("authorization");

  if (authorization !== null) {
    headers.set("Authorization", authorization);
  }

  return headers;
}

async function forwardFunctionsResponse(response: Response, startedAtMs: number) {
  const headers = new Headers();
  const contentType = response.headers.get("content-type");

  if (contentType) {
    headers.set("Content-Type", contentType);
  }
  copyV1ResponseHeaders(response.headers, headers);

  const responseText = await response.text();
  scheduleApiEvent({
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

export async function GET(request: Request, context: RouteContext) {
  const startedAtMs = Date.now();
  const { id } = await context.params;

  try {
    const response = await fetch(
      `${getAzureApiBaseUrl()}/api/v1/rewrite/${encodeURIComponent(id)}`,
      {
        cache: "no-store",
        headers: callerHeaders(request),
      },
    );

    return forwardFunctionsResponse(response, startedAtMs);
  } catch (error) {
    scheduleApiEvent({
      endpoint,
      error,
      errorCode: "proxy_request_failed",
      latencyMs: Date.now() - startedAtMs,
      statusCode: 502,
    });
    return NextResponse.json(proxyRequestFailedBody, { status: 502 });
  }
}
