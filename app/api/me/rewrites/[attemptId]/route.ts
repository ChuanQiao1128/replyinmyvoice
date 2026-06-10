import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../../lib/azure-api";
import { getCurrentAccessToken } from "../../../../../lib/entra-auth";
import { jsonError, requireSameOrigin } from "../../../../../lib/http";

export const dynamic = "force-dynamic";

type RouteContext = { params: Promise<{ attemptId: string }> };

async function forward(
  request: Request,
  context: RouteContext,
  method: "GET" | "DELETE",
) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    return jsonError("Authentication required.", 401);
  }

  const { attemptId } = await context.params;
  if (!/^[0-9a-fA-F-]{36}$/.test(attemptId)) {
    return jsonError("Invalid rewrite id.", 400);
  }

  let response: Response;
  try {
    response = await fetch(
      `${getAzureApiBaseUrl()}/api/me/rewrites/${attemptId}`,
      {
        cache: "no-store",
        headers: { Authorization: `Bearer ${accessToken}` },
        method,
      },
    );
  } catch {
    return jsonError("History request failed.", 502);
  }

  const headers = new Headers();
  const contentType = response.headers.get("content-type");
  if (contentType) {
    headers.set("Content-Type", contentType);
  }
  if (response.status === 204) {
    return new NextResponse(null, { headers, status: 204 });
  }
  return new NextResponse(await response.text(), {
    headers,
    status: response.status,
  });
}

export function GET(request: Request, context: RouteContext) {
  return forward(request, context, "GET");
}

export function DELETE(request: Request, context: RouteContext) {
  return forward(request, context, "DELETE");
}
