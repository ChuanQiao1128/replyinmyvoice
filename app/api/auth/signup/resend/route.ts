import { NextResponse } from "next/server";

import { readSignupFlowCookie, setSignupFlowCookie } from "../../../../../lib/entra-auth";
import { resendSignUpCode } from "../../../../../lib/entra-native-auth";

export const dynamic = "force-dynamic";

const RESEND_COOLDOWN_SECONDS = 30;
const FLOW_TTL_SECONDS = 10 * 60;

export async function POST() {
  const flow = await readSignupFlowCookie();
  if (!flow) {
    return NextResponse.json({ ok: false, error: "flow_expired" }, { status: 400 });
  }

  const now = Math.floor(Date.now() / 1000);
  const elapsed = now - flow.lastSentAt;
  if (elapsed < RESEND_COOLDOWN_SECONDS) {
    return NextResponse.json(
      { ok: false, error: "rate_limited", cooldownSeconds: RESEND_COOLDOWN_SECONDS - elapsed },
      { status: 429 },
    );
  }

  const result = await resendSignUpCode(flow.continuationToken);
  if (!result.ok) {
    return NextResponse.json({ ok: false, error: result.error }, { status: 400 });
  }

  const response = NextResponse.json({ ok: true, cooldownSeconds: RESEND_COOLDOWN_SECONDS });
  await setSignupFlowCookie({
    ...flow,
    continuationToken: result.continuationToken,
    codeLength: result.codeLength,
    channelLabel: result.channelLabel,
    lastSentAt: now,
    exp: now + FLOW_TTL_SECONDS,
  }, response.cookies);

  return response;
}
