import { NextResponse } from "next/server";

import { createLoginRedirectUrl } from "../../../../lib/entra-auth";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const redirectTo = url.searchParams.get("redirectTo") || "/app";
  const rawLoginHint = url.searchParams.get("loginHint")?.trim();
  const loginHint =
    rawLoginHint && rawLoginHint.length <= 320 && rawLoginHint.includes("@")
      ? rawLoginHint
      : undefined;
  const target = await createLoginRedirectUrl(
    redirectTo.startsWith("/") ? redirectTo : "/app",
    { loginHint },
  );

  return NextResponse.redirect(target);
}
