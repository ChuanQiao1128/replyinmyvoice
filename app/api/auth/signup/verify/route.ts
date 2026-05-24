import { NextResponse } from "next/server";

import {
  clearSignupFlowCookie,
  createSessionFromTokens,
  readSignupFlowCookie,
} from "../../../../../lib/entra-auth";
import { completeSignUp } from "../../../../../lib/entra-native-auth";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  let body: { code?: unknown };
  try {
    body = (await request.json()) as { code?: unknown };
  } catch {
    return NextResponse.json({ ok: false, error: "invalid_request" }, { status: 400 });
  }

  const code = typeof body.code === "string" ? body.code.trim() : "";

  const flow = await readSignupFlowCookie();
  if (!flow) {
    return NextResponse.json({ ok: false, error: "flow_expired" }, { status: 400 });
  }
  if (!/^\d{4,8}$/.test(code)) {
    return NextResponse.json({ ok: false, error: "invalid_code" }, { status: 400 });
  }

  const result = await completeSignUp(flow.continuationToken, code, flow.email, flow.displayName);
  if (!result.ok) {
    return NextResponse.json({ ok: false, error: result.error }, { status: 400 });
  }

  const response = NextResponse.json({ ok: true, redirectTo: "/app" });
  await createSessionFromTokens(
    {
      idToken: result.tokens.idToken,
      accessToken: result.tokens.accessToken,
      refreshToken: result.tokens.refreshToken,
    },
    response.cookies,
  );
  await clearSignupFlowCookie(response.cookies);

  return response;
}
