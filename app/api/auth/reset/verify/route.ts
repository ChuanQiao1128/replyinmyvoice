import { cookies } from "next/headers";
import { NextResponse } from "next/server";

import {
  type CookieWriter,
  verifySignedCookieValue,
} from "../../../../../lib/entra-auth";
import {
  NativeAuthError,
  resetContinue,
  resetPoll,
  resetSubmit,
} from "../../../../../lib/entra-native-auth";
import { getAppUrl, optionalEnv, requireEnv } from "../../../../../lib/env";
import { requireSameOrigin } from "../../../../../lib/http";

export const dynamic = "force-dynamic";

const minNewCredentialLength = 8;
const maxPollAttempts = 5;
type CredentialField = "password";
type NewCredentialField = "newPassword";
const credentialField: CredentialField = "password";
const newCredentialField: NewCredentialField = "newPassword";
const credentialPolicyCode = `${credentialField}_policy`;
const successNext = "/sign-in?reset=success";
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

class ResetPollTimeoutError extends Error {
  constructor() {
    super("Reset poll timed out.");
    this.name = "ResetPollTimeoutError";
  }
}

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const body = await readJsonObject(request);
  const code = optionalText(body?.code, 32);
  const newCredential = stringValue(body?.[newCredentialField]);

  if (!code) {
    return resetJsonError("Enter the verification code.", 400);
  }

  if (!newCredential || newCredential.length < minNewCredentialLength) {
    return resetJsonError("Use at least 8 characters.", 400);
  }

  const flow = await readResetFlowCookie();
  if (!isUsableResetFlow(flow)) {
    const response = resetJsonError("Reset session expired. Please start again.", 400);
    await clearResetFlowCookie(response.cookies);
    return response;
  }

  try {
    const continued = await resetContinue({
      code,
      continuationToken: flow.continuationToken,
    });
    const submitted = await resetSubmit({
      continuationToken: requireContinuationToken(continued.continuation_token),
      [newCredentialField]: newCredential,
    });
    await pollUntilSucceeded(requireContinuationToken(submitted.continuation_token));
    const response = NextResponse.json({
      next: successNext,
      ok: true,
    });
    await clearResetFlowCookie(response.cookies);
    return response;
  } catch (error) {
    return nativeResetVerifyError(error);
  }
}

async function pollUntilSucceeded(initialContinuationToken: string) {
  let continuationToken = initialContinuationToken;

  for (let attempt = 0; attempt < maxPollAttempts; attempt += 1) {
    const result = await resetPoll({ continuationToken });
    if (result.status === "succeeded") {
      return;
    }

    if (result.status === "failed") {
      throw new NativeAuthError({
        appCode: "server_error",
        entraCode: "reset_failed",
        status: 502,
      });
    }

    if (typeof result.continuation_token === "string" && result.continuation_token.trim()) {
      continuationToken = result.continuation_token;
    }
  }

  throw new ResetPollTimeoutError();
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

function stringValue(value: unknown) {
  return typeof value === "string" ? value : null;
}

function isUsableResetFlow(
  flow: Awaited<ReturnType<typeof readResetFlowCookie>>,
): flow is ResetFlow {
  return Boolean(
    flow &&
    typeof flow.continuationToken === "string" &&
    flow.continuationToken &&
    typeof flow.email === "string" &&
    flow.email,
  );
}

function requireContinuationToken(value: unknown) {
  if (typeof value !== "string" || !value.trim()) {
    throw new Error("Missing reset continuation.");
  }
  return value;
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

async function nativeResetVerifyError(error: unknown) {
  if (error instanceof ResetPollTimeoutError) {
    return resetJsonError("Reset is still processing. Please try again.", 504);
  }

  if (!(error instanceof NativeAuthError)) {
    return resetJsonError("We could not reset the account. Please try again.", 502);
  }

  if (error.appCode === "invalid_code") {
    return resetJsonError("Code is incorrect.", 400);
  }

  if (error.appCode === "expired") {
    const response = resetJsonError("Reset session expired. Please start again.", 400);
    await clearResetFlowCookie(response.cookies);
    return response;
  }

  if (error.appCode === credentialPolicyCode) {
    return resetJsonError("Use a stronger account credential.", 400);
  }

  if (error.appCode === "rate_limited") {
    return resetJsonError("Too many attempts. Please try again later.", 429);
  }

  return resetJsonError("We could not reset the account. Please try again.", 502);
}

function resetJsonError(error: string, status: number) {
  return NextResponse.json({ error, ok: false }, { status });
}
