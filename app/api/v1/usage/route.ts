import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../lib/azure-api";

export const dynamic = "force-dynamic";

async function forwardAzureResponse(response: Response) {
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

export async function GET(request: Request) {
  const authorization = request.headers.get("authorization");
  const headers: Record<string, string> = {};
  if (authorization) {
    headers.Authorization = authorization;
  }

  const response = await fetch(`${getAzureApiBaseUrl()}/api/v1/usage`, {
    cache: "no-store",
    headers,
  });

  return forwardAzureResponse(response);
}
