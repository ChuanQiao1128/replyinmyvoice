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

const authScheme = "Bearer";

async function currentAuthorizationHeader() {
  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    return null;
  }

  return `${authScheme} ${accessToken}`;
}

export async function GET() {
  const authorization = await currentAuthorizationHeader();
  if (!authorization) {
    return jsonError("Authentication required.", 401);
  }

  const response = await fetch(`${getAzureApiBaseUrl()}/api/me`, {
    cache: "no-store",
    headers: {
      Authorization: authorization,
    },
  });

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

  const response = await fetch(`${getAzureApiBaseUrl()}/api/me`, {
    cache: "no-store",
    headers: {
      Authorization: authorization,
    },
    method: "DELETE",
  });

  return forwardAzureResponse(response);
}
