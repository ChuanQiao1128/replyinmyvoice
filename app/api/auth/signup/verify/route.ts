import { NextResponse } from "next/server";

import {
  clearSignupFlowCookie,
  createSessionFromTokens,
  readSignupFlowCookie,
} from "../../../../../lib/entra-auth";
import {
  NativeAuthError,
  signupContinue,
} from "../../../../../lib/entra-native-auth";
import { getAppUrl } from "../../../../../lib/env";
import { requireSameOrigin } from "../../../../../lib/http";

export const dynamic = "force-dynamic";

type SignupFlow = NonNullable<Awaited<ReturnType<typeof readSignupFlowCookie>>>;

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const body = await readJsonObject(request);
  const code = optionalText(body?.code, 32);
  const redirectTo = safeRedirectTo(body?.redirectTo);

  if (!code) {
    return signupJsonError("Enter the verification code.", 400);
  }

  const flow = await readSignupFlowCookie();
  if (!isUsableSignupFlow(flow)) {
    const response = signupJsonError("Sign-up session expired. Please start again.", 400);
    await clearSignupFlowCookie(response.cookies);
    return response;
  }

  try {
    const completed = await signupContinue({
      code,
      continuationToken: flow.continuationToken,
    });
    const response = NextResponse.redirect(`${getAppUrl()}${redirectTo}`, 302);
    await createSessionFromTokens({
      accessToken: completed.token.access_token,
      idToken: completed.token.id_token,
      refreshToken: completed.token.refresh_token ?? null,
    }, response.cookies);
    await clearSignupFlowCookie(response.cookies);
    return response;
  } catch (error) {
    return nativeVerifyError(error);
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

function optionalText(value: unknown, maxLength: number) {
  if (typeof value !== "string") {
    return null;
  }

  const text = value.trim();
  return text && text.length <= maxLength ? text : null;
}

function safeRedirectTo(value: unknown) {
  if (typeof value !== "string" || !value.startsWith("/") || value.startsWith("//")) {
    return "/app";
  }
  return value;
}

function isUsableSignupFlow(
  flow: Awaited<ReturnType<typeof readSignupFlowCookie>>,
): flow is SignupFlow {
  return Boolean(
    flow &&
    typeof flow.continuationToken === "string" &&
    flow.continuationToken &&
    typeof flow.email === "string" &&
    flow.email,
  );
}

async function nativeVerifyError(error: unknown) {
  if (!(error instanceof NativeAuthError)) {
    return signupJsonError("We could not verify the code. Please try again.", 502);
  }

  if (error.appCode === "invalid_code") {
    return signupJsonError("Code is incorrect.", 400);
  }

  if (error.appCode === "expired") {
    const response = signupJsonError("Code expired. Please start again.", 400);
    await clearSignupFlowCookie(response.cookies);
    return response;
  }

  if (error.appCode === "rate_limited") {
    return signupJsonError("Too many attempts. Please try again later.", 429);
  }

  return signupJsonError("We could not verify the code. Please try again.", 502);
}

function signupJsonError(error: string, status: number) {
  return NextResponse.json({ error, ok: false }, { status });
}
