import { NextResponse } from "next/server";

import { createLoginRedirectUrl } from "../../../../lib/entra-auth";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const redirectTo = url.searchParams.get("redirectTo") || "/app";
  const target = await createLoginRedirectUrl(redirectTo.startsWith("/") ? redirectTo : "/app");

  return NextResponse.redirect(target);
}
