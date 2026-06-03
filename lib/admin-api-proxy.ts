import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "./azure-api";
import { getCurrentAccessToken } from "./entra-auth";
import { jsonError, requireSameOrigin } from "./http";

function allowsBrowserSameSiteRead(request: Request) {
  if (request.headers.get("origin")) {
    return false;
  }

  const fetchSite = request.headers.get("sec-fetch-site");
  return fetchSite === "same-origin" || fetchSite === "none";
}

function requireAdminProxyOrigin(request: Request) {
  const originError = requireSameOrigin(request);
  if (!originError || allowsBrowserSameSiteRead(request)) {
    return null;
  }

  return originError;
}

function azureAdminUrl(path: string, request: Request) {
  const target = new URL(`${getAzureApiBaseUrl()}${path}`);
  const source = new URL(request.url);
  source.searchParams.forEach((value, key) => {
    target.searchParams.append(key, value);
  });
  return target.toString();
}

async function currentAuthorizationHeader() {
  const accessToken = await getCurrentAccessToken();
  return accessToken ? `Bearer ${accessToken}` : null;
}

export async function forwardAzureAdminResponse(response: Response) {
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

export async function forwardAdminGet(request: Request, path: string) {
  const originError = requireAdminProxyOrigin(request);
  if (originError) {
    return originError;
  }

  const authorization = await currentAuthorizationHeader();
  if (!authorization) {
    return jsonError("Authentication required.", 401);
  }

  const response = await fetch(azureAdminUrl(path, request), {
    cache: "no-store",
    headers: {
      Authorization: authorization,
    },
  });

  return forwardAzureAdminResponse(response);
}

export async function forwardAdminPost(request: Request, path: string) {
  const originError = requireAdminProxyOrigin(request);
  if (originError) {
    return originError;
  }

  const authorization = await currentAuthorizationHeader();
  if (!authorization) {
    return jsonError("Authentication required.", 401);
  }

  const response = await fetch(azureAdminUrl(path, request), {
    body: await request.text(),
    cache: "no-store",
    headers: {
      Authorization: authorization,
      "Content-Type": request.headers.get("content-type") ?? "application/json",
    },
    method: "POST",
  });

  return forwardAzureAdminResponse(response);
}

export async function forwardAdminDelete(request: Request, path: string) {
  const originError = requireAdminProxyOrigin(request);
  if (originError) {
    return originError;
  }

  const authorization = await currentAuthorizationHeader();
  if (!authorization) {
    return jsonError("Authentication required.", 401);
  }

  const response = await fetch(azureAdminUrl(path, request), {
    cache: "no-store",
    headers: {
      Authorization: authorization,
    },
    method: "DELETE",
  });

  return forwardAzureAdminResponse(response);
}
