import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../lib/azure-api";

export const dynamic = "force-dynamic";

export async function GET() {
  const response = await fetch(`${getAzureApiBaseUrl()}/api/health/db`, {
    cache: "no-store",
  });

  return new NextResponse(await response.text(), {
    status: response.status,
    headers: {
      "Content-Type": response.headers.get("content-type") ?? "application/json",
    },
  });
}
