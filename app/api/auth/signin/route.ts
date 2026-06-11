import { NextResponse } from "next/server";

import {
  createSessionFromTokens,
} from "../../../../lib/entra-auth";
import {
  NativeAuthError,
  signinPassword as signinWithCredential,
} from "../../../../lib/entra-native-auth";
import {
  authRateLimitPolicies,
  checkAuthRateLimit,
} from "../../../../lib/auth-rate-limit";
import {
  buildAuthRedirectSearchParams,
  buildPostAuthRedirectPath,
  normalizeAuthRedirectParams,
  type AuthRedirectInput,
} from "../../../../lib/auth-redirect-intent";
import { getAppUrl } from "../../../../lib/env";
import { requireSameOrigin } from "../../../../lib/http";

export const dynamic = "force-dynamic";

const minCredentialLength = 8;
type CredentialField = "password";
const credentialField: CredentialField = "password";

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const body = await readJsonObject(request);
  const email = normalizeEmail(body?.email);
  const credential = stringValue(body?.[credentialField]);
  const authRedirect = normalizeAuthRedirectParams(body ?? {});
  const postAuthRedirect = buildPostAuthRedirectPath(authRedirect);

  if (!email) {
    return signinJsonError("invalid_request", 400);
  }

  if (!credential || credential.length < minCredentialLength) {
    return signinJsonError("invalid_credentials", 401);
  }

  const rateLimit = await checkAuthRateLimit({
    email,
    policy: authRateLimitPolicies.signin,
    request,
  });
  if (!rateLimit.allowed) {
    return signinJsonError("rate_limited", 429);
  }

  try {
    const tokens = await signinWithCredential({
      email,
      [credentialField]: credential,
    });
    const response = NextResponse.redirect(`${getAppUrl()}${postAuthRedirect}`, 302);
    await createSessionFromTokens({
      accessToken: tokens.access_token,
      idToken: tokens.id_token,
      refreshToken: tokens.refresh_token ?? null,
    }, response.cookies);
    return response;
  } catch (error) {
    return nativeSigninError(error, email, authRedirect);
  }
}

async function readJsonObject(request: Request) {
  try {
    const parsed = await request.json() as unknown;
    return parsed && typeof parsed === "object" && !Array.isArray(parsed)
      ? parsed as Record<string, unknown>
      : null;
  } catch {
    return null;
  }
}

function normalizeEmail(value: unknown) {
  const email = optionalText(value, 320)?.toLowerCase() ?? "";
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) ? email : null;
}

function optionalText(value: unknown, maxLength: number) {
  const text = stringValue(value)?.trim();
  return text && text.length <= maxLength ? text : null;
}

function stringValue(value: unknown) {
  return typeof value === "string" ? value : null;
}

function nativeSigninError(error: unknown, email: string, authRedirect: AuthRedirectInput) {
  if (!(error instanceof NativeAuthError)) {
    return signinJsonError("signin_failed", 502);
  }

  const appCode = error.appCode as string;
  if (appCode === "invalid_code") {
    return signinJsonError("invalid_credentials", 401);
  }

  if (appCode === "user_not_found") {
    return signinJsonError("user_not_found", 404);
  }

  if (appCode === "redirect_required") {
    return NextResponse.json({
      error: "redirect_required",
      fallbackRedirect: browserFallbackRedirect(email, authRedirect),
      ok: false,
    }, { status: 409 });
  }

  if (appCode === "rate_limited") {
    return signinJsonError("rate_limited", 429);
  }

  return signinJsonError("signin_failed", 502);
}

function signinJsonError(error: string, status: number) {
  return NextResponse.json({ error, ok: false }, { status });
}

function browserFallbackRedirect(email: string, authRedirect: AuthRedirectInput) {
  const params = buildAuthRedirectSearchParams(authRedirect, { loginHint: email });
  return `/api/auth/login?${params.toString()}`;
}
