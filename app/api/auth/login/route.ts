import { NextResponse } from "next/server";

import { normalizeAuthRedirectParams } from "../../../../lib/auth-redirect-intent";
import { createLoginRedirectUrl } from "../../../../lib/entra-auth";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const authRedirect = normalizeAuthRedirectParams({
    intent: url.searchParams.get("intent"),
    redirectTo: url.searchParams.get("redirectTo") || "/app",
    sku: url.searchParams.get("sku"),
  });
  const rawLoginHint = url.searchParams.get("loginHint")?.trim();
  const loginHint =
    rawLoginHint && rawLoginHint.length <= 320 && rawLoginHint.includes("@")
      ? rawLoginHint
      : undefined;
  const target = await createLoginRedirectUrl(
    authRedirect.redirectTo,
    { intent: authRedirect.intent, loginHint, sku: authRedirect.sku },
  );

  return NextResponse.redirect(target);
}
