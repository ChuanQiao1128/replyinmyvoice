import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../lib/azure-api";
import { copyV1ResponseHeaders } from "../../../../lib/v1-response-headers";

export const dynamic = "force-dynamic";

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

async function forwardFunctionsResponse(response: Response) {
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

  return new NextResponse(await response.text(), {
    headers,
    status: response.status,
  });
}

export async function POST(request: Request) {
  const response = await fetch(`${getAzureApiBaseUrl()}/api/v1/rewrite`, {
    body: await request.text(),
    cache: "no-store",
    headers: callerHeaders(request),
    method: "POST",
  });

  return forwardFunctionsResponse(response);
}
