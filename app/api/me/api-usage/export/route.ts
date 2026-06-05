import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../../lib/azure-api";
import { serializeCsv } from "../../../../../lib/csv-export";
import { getCurrentAccessToken } from "../../../../../lib/entra-auth";
import { jsonError, requireSameOrigin } from "../../../../../lib/http";

export const dynamic = "force-dynamic";

const usageColumns = ["createdAt", "endpoint", "statusCode", "latencyMs", "keyLast4"] as const;

function limitFromRequest(url: URL) {
  const rawLimit = url.searchParams.get("limit");
  if (!rawLimit || !/^\d+$/.test(rawLimit)) {
    return 1000;
  }

  const parsed = Number.parseInt(rawLimit, 10);
  if (!Number.isFinite(parsed) || parsed < 1) {
    return 1000;
  }

  return Math.min(parsed, 1000);
}

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

function csvResponse(csv: string) {
  return new NextResponse(csv, {
    headers: {
      "Content-Disposition": 'attachment; filename="api-usage.csv"',
      "Content-Type": "text/csv; charset=utf-8",
    },
    status: 200,
  });
}

export async function GET(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    return jsonError("Authentication required.", 401);
  }

  const url = new URL(request.url);
  const limit = limitFromRequest(url);
  const response = await fetch(
    `${getAzureApiBaseUrl()}/api/me/api-usage/recent?limit=${limit}`,
    {
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    },
  );

  if (!response.ok) {
    return forwardAzureResponse(response);
  }

  const payload = (await response.json().catch(() => null)) as unknown;
  if (!Array.isArray(payload)) {
    return jsonError("Unexpected usage response.", 502);
  }

  const rows = payload.map((row) => {
    const item = row as Record<string, unknown>;
    return {
      createdAt: item.createdAt,
      endpoint: item.endpoint,
      keyLast4: item.keyLast4,
      latencyMs: item.latencyMs,
      statusCode: item.statusCode,
    };
  });

  return csvResponse(serializeCsv(usageColumns, rows));
}
