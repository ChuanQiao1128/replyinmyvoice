import { NextResponse } from "next/server";

import { createSessionFromTokens } from "../../../../lib/entra-auth";
import { signInWithPassword } from "../../../../lib/entra-native-auth";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  let body: { email?: unknown; password?: unknown };
  try {
    body = (await request.json()) as { email?: unknown; password?: unknown };
  } catch {
    return NextResponse.json({ ok: false, error: "invalid_request" }, { status: 400 });
  }

  const email = typeof body.email === "string" ? body.email.trim() : "";
  const password = typeof body.password === "string" ? body.password : "";
  if (!email || !password) {
    return NextResponse.json({ ok: false, error: "invalid_credentials" }, { status: 400 });
  }

  const result = await signInWithPassword(email, password);
  if (!result.ok) {
    if (result.error === "redirect_required") {
      return NextResponse.json(
        { ok: false, error: result.error, fallbackRedirect: `/api/auth/login?loginHint=${encodeURIComponent(email)}` },
        { status: 409 },
      );
    }
    const status = result.error === "user_not_found" ? 404 : 401;
    return NextResponse.json({ ok: false, error: result.error }, { status });
  }

  await createSessionFromTokens({
    idToken: result.tokens.idToken,
    accessToken: result.tokens.accessToken,
    refreshToken: result.tokens.refreshToken,
  });
  return NextResponse.json({ ok: true, redirectTo: "/app" });
}
