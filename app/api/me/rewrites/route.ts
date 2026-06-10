import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../lib/azure-api";
import { getCurrentAccessToken } from "../../../../lib/entra-auth";
import { jsonError, requireSameOrigin } from "../../../../lib/http";

export const dynamic = "force-dynamic";

// Proxies the caller-scoped server-side history list:
//   GET /api/me/rewrites?page=&pageSize=  →  {azure}/api/me/rewrites
// Surfaces the backend `ListMyRewriteAttempts` endpoint that the UI never used.
export async function GET(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    return jsonError("Authentication required.", 401);
  }

  const incoming = new URL(request.url);
  const target = new URL(`${getAzureApiBaseUrl()}/api/me/rewrites`);
  const page = incoming.searchParams.get("page");
  const pageSize = incoming.searchParams.get("pageSize");
  if (page) {
    target.searchParams.set("page", page);
  }
  if (pageSize) {
    target.searchParams.set("pageSize", pageSize);
  }

  let response: Response;
  try {
    response = await fetch(target, {
      cache: "no-store",
      headers: { Authorization: `Bearer ${accessToken}` },
    });
  } catch {
    return jsonError("History request failed.", 502);
  }

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
