import { NextResponse } from "next/server";

import { adminPromoDetailFromPayload } from "./admin-promo-codes";
import { getAzureApiBaseUrl } from "./azure-api";
import { getCurrentAccessToken } from "./entra-auth";
import { jsonError, requireSameOrigin } from "./http";

const authScheme = "Bearer";

export async function currentAdminAuthorizationHeader() {
  const accessToken = await getCurrentAccessToken();
  return accessToken ? `${authScheme} ${accessToken}` : null;
}

export function isGuid(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
    value,
  );
}

export async function forwardAzureResponse(response: Response) {
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

export async function forwardAdminGet(path: string) {
  const authorization = await currentAdminAuthorizationHeader();
  if (!authorization) {
    return jsonError("Authentication required.", 401);
  }

  const response = await fetch(`${getAzureApiBaseUrl()}${path}`, {
    cache: "no-store",
    headers: {
      Authorization: authorization,
    },
  });

  return forwardAzureResponse(response);
}

export async function forwardAdminMutation(
  request: Request,
  path: string,
  method: "PATCH" | "POST",
) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const authorization = await currentAdminAuthorizationHeader();
  if (!authorization) {
    return jsonError("Authentication required.", 401);
  }

  const body = await request.text();
  const headers: Record<string, string> = {
    Authorization: authorization,
  };
  if (body) {
    headers["Content-Type"] = request.headers.get("content-type") ?? "application/json";
  }

  const response = await fetch(`${getAzureApiBaseUrl()}${path}`, {
    body: body || undefined,
    cache: "no-store",
    headers,
    method,
  });

  return forwardAzureResponse(response);
}

export async function forwardAdminDetail(path: string) {
  const authorization = await currentAdminAuthorizationHeader();
  if (!authorization) {
    return jsonError("Authentication required.", 401);
  }

  const response = await fetch(`${getAzureApiBaseUrl()}${path}`, {
    cache: "no-store",
    headers: {
      Authorization: authorization,
    },
  });

  if (!response.ok) {
    return forwardAzureResponse(response);
  }

  const payload = await response.json().catch(() => null);
  const detail = adminPromoDetailFromPayload(payload);
  if (!detail) {
    return jsonError("Admin promo code detail response was invalid.", 502);
  }

  return NextResponse.json(detail);
}
