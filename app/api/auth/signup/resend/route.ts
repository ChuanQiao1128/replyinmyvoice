import { NextResponse } from "next/server";

import {
  clearSignupFlowCookie,
  readSignupFlowCookie,
  setSignupFlowCookie,
  type SignupFlowState,
} from "../../../../../lib/entra-auth";
import {
  NativeAuthError,
  signupChallenge,
} from "../../../../../lib/entra-native-auth";
import {
  authRateLimitPolicies,
  checkAuthRateLimit,
} from "../../../../../lib/auth-rate-limit";
import { requireSameOrigin } from "../../../../../lib/http";

export const dynamic = "force-dynamic";

const cooldownMs = 30_000;
const cooldownSeconds = cooldownMs / 1000;
const signupFlowTtlSeconds = 10 * 60;

type SignupFlow = NonNullable<Awaited<ReturnType<typeof readSignupFlowCookie>>>;

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const flow = await readSignupFlowCookie();
  if (!isUsableSignupFlow(flow)) {
    const response = signupJsonError("Sign-up session expired. Please start again.", 400);
    await clearSignupFlowCookie(response.cookies);
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
    policy: authRateLimitPolicies.signupResend,
    request,
  });
  if (!rateLimit.allowed) {
    return signupJsonError("Too many requests. Please try again later.", 429);
  }

  try {
    const challenged = await signupChallenge({
      continuationToken: flow.continuationToken,
    });
    const updatedFlow: SignupFlowState = {
      ...flow,
      channelLabel: textOrFallback(challenged.challenge_target_label, flow.channelLabel),
      codeLength: typeof challenged.code_length === "number"
        ? challenged.code_length
        : flow.codeLength,
      continuationToken: requireContinuationToken(challenged.continuation_token),
      exp: Math.floor(nowMs / 1000) + signupFlowTtlSeconds,
      lastSentAt: nowMs,
    };
    const response = NextResponse.json({
      cooldownSeconds,
      ok: true,
    });
    await setSignupFlowCookie(updatedFlow, response.cookies);
    return response;
  } catch (error) {
    return nativeResendError(error);
  }
}

function isUsableSignupFlow(
  flow: Awaited<ReturnType<typeof readSignupFlowCookie>>,
): flow is SignupFlow {
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
    throw new Error("Missing sign-up continuation.");
  }
  return value;
}

function textOrFallback(value: unknown, fallback: string | null) {
  return typeof value === "string" && value.trim() ? value.trim() : fallback;
}

async function nativeResendError(error: unknown) {
  if (!(error instanceof NativeAuthError)) {
    return signupJsonError("We could not resend the code. Please try again.", 502);
  }

  if (error.appCode === "expired") {
    const response = signupJsonError("Sign-up session expired. Please start again.", 400);
    await clearSignupFlowCookie(response.cookies);
    return response;
  }

  if (error.appCode === "rate_limited") {
    return signupJsonError("Too many requests. Please try again later.", 429);
  }

  return signupJsonError("We could not resend the code. Please try again.", 502);
}

function signupJsonError(error: string, status: number) {
  return NextResponse.json({ error, ok: false }, { status });
}
