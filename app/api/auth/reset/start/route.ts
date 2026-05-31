import { NextResponse } from "next/server";

import {
  createSignedCookieValue,
  type CookieWriter,
} from "../../../../../lib/entra-auth";
import {
  NativeAuthError,
  resetChallenge,
  resetStart,
} from "../../../../../lib/entra-native-auth";
import { getAppUrl, optionalEnv, requireEnv } from "../../../../../lib/env";
import { requireSameOrigin } from "../../../../../lib/http";

export const dynamic = "force-dynamic";

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

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const body = await readJsonObject(request);
  const email = normalizeEmail(body?.email);

  if (!email) {
    return resetJsonError("Enter a valid email.", 400);
  }

  try {
    const started = await resetStart({ email });
    const challenged = await resetChallenge({
      continuationToken: requireContinuationToken(started.continuation_token),
    });
    const codeLength = typeof challenged.code_length === "number"
      ? challenged.code_length
      : 6;
    const channelLabel = typeof challenged.challenge_target_label === "string" &&
      challenged.challenge_target_label.trim()
      ? challenged.challenge_target_label.trim()
      : email;
    const nowMs = Date.now();
    const state: ResetFlowState = {
      channelLabel,
      codeLength,
      continuationToken: requireContinuationToken(challenged.continuation_token),
      email,
      exp: Math.floor(nowMs / 1000) + resetFlowTtlSeconds,
      lastSentAt: nowMs,
    };
    const response = NextResponse.json({
      channelLabel,
      codeLength,
      ok: true,
    });
    await setResetFlowCookie(state, response.cookies);
    return response;
  } catch (error) {
    return nativeResetStartError(error);
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

function nativeResetStartError(error: unknown) {
  if (!(error instanceof NativeAuthError)) {
    return resetJsonError("We could not start reset. Please try again.", 502);
  }

  if (error.appCode === "user_not_found") {
    return resetJsonError("No account for this email. Please create one.", 404);
  }

  if (error.appCode === "rate_limited") {
    return resetJsonError("Too many requests. Please try again later.", 429);
  }

  return resetJsonError("We could not start reset. Please try again.", 502);
}

function resetJsonError(error: string, status: number) {
  return NextResponse.json({ error, ok: false }, { status });
}
