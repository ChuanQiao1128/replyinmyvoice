import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../../lib/azure-api";

export const dynamic = "force-dynamic";

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

async function forwardFunctionsResponse(response: Response) {
  const headers = new Headers();
  const contentType = response.headers.get("content-type");

  if (contentType) {
    headers.set("Content-Type", contentType);
  }

  return new NextResponse(await response.text(), {
    headers,
    status: response.status,
  });
}

export async function GET(request: Request, context: RouteContext) {
  const { id } = await context.params;
  const response = await fetch(
    `${getAzureApiBaseUrl()}/api/v1/rewrite/${encodeURIComponent(id)}`,
    {
      cache: "no-store",
      headers: callerHeaders(request),
    },
  );

  return forwardFunctionsResponse(response);
}
