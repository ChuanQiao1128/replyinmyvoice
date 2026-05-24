import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../lib/azure-api";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  const signature = request.headers.get("stripe-signature");
  const response = await fetch(`${getAzureApiBaseUrl()}/api/stripe/webhook`, {
    method: "POST",
    headers: {
      "Content-Type": request.headers.get("content-type") ?? "application/json",
      ...(signature ? { "stripe-signature": signature } : {}),
    },
    body: await request.text(),
    cache: "no-store",
  });

  return new NextResponse(await response.text(), {
    status: response.status,
    headers: {
      "Content-Type": response.headers.get("content-type") ?? "application/json",
    },
  });
}

export async function GET() {
  return NextResponse.json({ backend: "azure-functions" });
}
