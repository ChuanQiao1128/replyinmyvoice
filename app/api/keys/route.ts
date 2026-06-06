import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../lib/azure-api";
import { getCurrentAccessToken } from "../../../lib/entra-auth";
import { jsonError, requireSameOrigin } from "../../../lib/http";

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

async function currentAuthorizationHeader() {
  const accessToken = await getCurrentAccessToken();
  return accessToken ? `Bearer ${accessToken}` : null;
}

export async function GET(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const authorization = await currentAuthorizationHeader();
  if (!authorization) {
    return jsonError("Authentication required.", 401);
  }

  const response = await fetch(`${getAzureApiBaseUrl()}/api/keys`, {
    cache: "no-store",
    headers: {
      Authorization: authorization,
    },
  });

  return forwardAzureResponse(response);
}

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const authorization = await currentAuthorizationHeader();
  if (!authorization) {
    return jsonError("Authentication required.", 401);
  }

  const response = await fetch(`${getAzureApiBaseUrl()}/api/keys`, {
    body: await request.text(),
    cache: "no-store",
    headers: {
      Authorization: authorization,
      "Content-Type": request.headers.get("content-type") ?? "application/json",
    },
    method: "POST",
  });

  return forwardAzureResponse(response);
}
