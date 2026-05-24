import { NextResponse } from "next/server";

import { setSignupFlowCookie } from "../../../../../lib/entra-auth";
import { startPasswordSignUp } from "../../../../../lib/entra-native-auth";

export const dynamic = "force-dynamic";

const FLOW_TTL_SECONDS = 10 * 60;

export async function POST(request: Request) {
  let body: { email?: unknown; password?: unknown; name?: unknown };
  try {
    body = (await request.json()) as { email?: unknown; password?: unknown; name?: unknown };
  } catch {
    return NextResponse.json({ ok: false, error: "invalid_request" }, { status: 400 });
  }

  const email = typeof body.email === "string" ? body.email.trim() : "";
  const password = typeof body.password === "string" ? body.password : "";
  const name = typeof body.name === "string" ? body.name.trim() : "";

  if (!email || email.length > 320 || !email.includes("@")) {
    return NextResponse.json({ ok: false, error: "invalid_email" }, { status: 400 });
  }

  const result = await startPasswordSignUp(email, password);
  if (!result.ok) {
    if (result.error === "redirect_required") {
      return NextResponse.json(
        { ok: false, error: result.error, fallbackRedirect: `/api/auth/login?loginHint=${encodeURIComponent(email)}` },
        { status: 409 },
      );
    }
    const status = result.error === "user_already_exists" ? 409 : 400;
    return NextResponse.json({ ok: false, error: result.error }, { status });
  }

  const now = Math.floor(Date.now() / 1000);
  const response = NextResponse.json({
    ok: true,
    codeLength: result.codeLength,
    channelLabel: result.channelLabel,
  });
  await setSignupFlowCookie({
    continuationToken: result.continuationToken,
    email,
    displayName: name || null,
    codeLength: result.codeLength,
    channelLabel: result.channelLabel,
    lastSentAt: now,
    exp: now + FLOW_TTL_SECONDS,
  }, response.cookies);

  return response;
}
