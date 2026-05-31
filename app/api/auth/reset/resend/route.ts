import { cookies } from "next/headers";
import { NextResponse } from "next/server";

import {
  createSignedCookieValue,
  type CookieWriter,
  verifySignedCookieValue,
} from "../../../../../lib/entra-auth";
import {
  NativeAuthError,
  resetChallenge,
} from "../../../../../lib/entra-native-auth";
import {
  authRateLimitPolicies,
  checkAuthRateLimit,
} from "../../../../../lib/auth-rate-limit";
import { getAppUrl, optionalEnv, requireEnv } from "../../../../../lib/env";
import { requireSameOrigin } from "../../../../../lib/http";

export const dynamic = "force-dynamic";

const cooldownMs = 30_000;
const cooldownSeconds = cooldownMs / 1000;
const resetFlowTtlSeconds = 10 * 60;
const resetFlowCookieName = "rimv_reset";

type ResetFlowState = {
  continuationToken: string;
  email: string;
  codeLength: number;
  channelLabel: string | null;
  lastSentAt: number;
  exp: number;
};

type ResetFlow = NonNullable<Awaited<ReturnType<typeof readResetFlowCookie>>>;

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const flow = await readResetFlowCookie();
  if (!isUsableResetFlow(flow)) {
    const response = resetJsonError("Reset session expired. Please start again.", 400);
    await clearResetFlowCookie(response.cookies);
    return response;
  }

  const nowMs = Date.now();
  const waitMs = cooldownMs - (nowMs - flow.lastSentAt);
  if (waitMs > 0) {
    return NextResponse.json({
      cooldownSeconds: Math.ceil(waitMs / 1000),
      error: "Please wait before requesting another code.",
      ok: false,
    }, { status: 429 });
  }

  const rateLimit = await checkAuthRateLimit({
    email: flow.email,
    policy: authRateLimitPolicies.resetResend,
    request,
  });
  if (!rateLimit.allowed) {
    return resetJsonError("Too many requests. Please try again later.", 429);
  }

  try {
    const challenged = await resetChallenge({
      continuationToken: flow.continuationToken,
    });
    const updatedFlow: ResetFlowState = {
      ...flow,
      channelLabel: textOrFallback(challenged.challenge_target_label, flow.channelLabel),
      codeLength: typeof challenged.code_length === "number"
        ? challenged.code_length
        : flow.codeLength,
      continuationToken: requireContinuationToken(challenged.continuation_token),
      exp: Math.floor(nowMs / 1000) + resetFlowTtlSeconds,
      lastSentAt: nowMs,
    };
    const response = NextResponse.json({
      cooldownSeconds,
      ok: true,
    });
    await setResetFlowCookie(updatedFlow, response.cookies);
    return response;
  } catch (error) {
    return nativeResetResendError(error);
  }
}

function isUsableResetFlow(
  flow: Awaited<ReturnType<typeof readResetFlowCookie>>,
): flow is ResetFlow {
  return Boolean(
    flow &&
    typeof flow.continuationToken === "string" &&
    flow.continuationToken &&
    typeof flow.email === "string" &&
    flow.email &&
    typeof flow.lastSentAt === "number",
  );
}

function requireContinuationToken(value: unknown) {
  if (typeof value !== "string" || !value.trim()) {
    throw new Error("Missing reset continuation.");
  }
  return value;
}

function textOrFallback(value: unknown, fallback: string | null) {
  return typeof value === "string" && value.trim() ? value.trim() : fallback;
}

function resetCookieOptions(maxAge: number) {
  return {
    httpOnly: true,
    secure: getAppUrl().startsWith("https://"),
    sameSite: "lax" as const,
    path: "/",
    maxAge,
  };
}

function getFlowCookieSigner() {
  const authName = "AUTH_SESSION_SECRET";
  const webhookName = "STRIPE_WEBHOOK_SECRET";
  return optionalEnv(authName) || optionalEnv(webhookName) || requireEnv(authName);
}

async function setResetFlowCookie(
  state: ResetFlowState,
  cookieWriter: CookieWriter,
) {
  const value = await createSignedCookieValue(state, getFlowCookieSigner());
  cookieWriter.set(
    resetFlowCookieName,
    value,
    resetCookieOptions(Math.max(60, state.exp - Math.floor(Date.now() / 1000))),
  );
}

async function readResetFlowCookie(): Promise<ResetFlowState | null> {
  const cookieStore = await cookies();
  const state = await verifySignedCookieValue<ResetFlowState>(
    cookieStore.get(resetFlowCookieName)?.value,
    getFlowCookieSigner(),
  );
  if (!state || state.exp <= Math.floor(Date.now() / 1000)) {
    return null;
  }
  return state;
}

async function clearResetFlowCookie(cookieWriter: CookieWriter) {
  cookieWriter.set(resetFlowCookieName, "", resetCookieOptions(0));
}

async function nativeResetResendError(error: unknown) {
  if (!(error instanceof NativeAuthError)) {
    return resetJsonError("We could not resend the code. Please try again.", 502);
  }

  if (error.appCode === "expired") {
    const response = resetJsonError("Reset session expired. Please start again.", 400);
    await clearResetFlowCookie(response.cookies);
    return response;
  }

  if (error.appCode === "rate_limited") {
    return resetJsonError("Too many requests. Please try again later.", 429);
  }

  return resetJsonError("We could not resend the code. Please try again.", 502);
}

function resetJsonError(error: string, status: number) {
  return NextResponse.json({ error, ok: false }, { status });
}
