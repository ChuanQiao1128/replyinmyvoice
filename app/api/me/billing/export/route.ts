import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../../lib/azure-api";
import { serializeCsv } from "../../../../../lib/csv-export";
import { getCurrentAccessToken } from "../../../../../lib/entra-auth";
import { jsonError, requireSameOrigin } from "../../../../../lib/http";

export const dynamic = "force-dynamic";

const azureBillingHistoryPath = "/api/me/billing/history";
const billingColumns = [
  "date",
  "type",
  "description",
  "amount",
  "currency",
  "status",
  "url",
] as const;

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

function firstUrl(item: Record<string, unknown>) {
  const receiptUrl = typeof item.receiptUrl === "string" ? item.receiptUrl : "";
  if (receiptUrl.trim()) {
    return receiptUrl;
  }

  const hostedInvoiceUrl =
    typeof item.hostedInvoiceUrl === "string" ? item.hostedInvoiceUrl : "";
  return hostedInvoiceUrl.trim() ? hostedInvoiceUrl : null;
}

function csvResponse(csv: string) {
  return new NextResponse(csv, {
    headers: {
      "Content-Disposition": 'attachment; filename="billing-history.csv"',
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

  const response = await fetch(`${getAzureApiBaseUrl()}${azureBillingHistoryPath}`, {
    cache: "no-store",
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  });

  if (!response.ok) {
    return forwardAzureResponse(response);
  }

  const payload = (await response.json().catch(() => null)) as unknown;
  if (!Array.isArray(payload)) {
    return jsonError("Unexpected billing response.", 502);
  }

  const rows = payload.map((row) => {
    const item = row as Record<string, unknown>;
    return {
      amount: item.amount,
      currency: item.currency,
      date: item.date,
      description: item.description,
      status: item.status,
      type: item.type,
      url: firstUrl(item),
    };
  });

  return csvResponse(serializeCsv(billingColumns, rows));
}
