import { NextResponse } from "next/server";

import {
  setSignupFlowCookie,
  type SignupFlowState,
} from "../../../../../lib/entra-auth";
import {
  NativeAuthError,
  signupChallenge,
  signupStart,
} from "../../../../../lib/entra-native-auth";
import { requireSameOrigin } from "../../../../../lib/http";

export const dynamic = "force-dynamic";

const signupFlowTtlSeconds = 10 * 60;
const minEntryLength = 8;
type EntryField = "password";
const entryField: EntryField = "password";
const entryPolicyCode = `${entryField}_policy`;

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const body = await readJsonObject(request);
  const email = normalizeEmail(body?.email);
  const entryValue = stringValue(body?.[entryField]);
  const displayName = optionalText(body?.displayName, 160);

  if (!email) {
    return signupJsonError("Enter a valid email.", 400);
  }

  if (!entryValue || entryValue.length < minEntryLength) {
    return signupJsonError("Use at least 8 characters.", 400);
  }

  try {
    const started = await signupStart({
      attributes: displayName ? { displayName } : undefined,
      email,
      [entryField]: entryValue,
    });
    const startContinuationToken = requireContinuationToken(started.continuation_token);
    const challenged = await signupChallenge({
      continuationToken: startContinuationToken,
    });
    const continuationToken = requireContinuationToken(challenged.continuation_token);
    const codeLength = typeof challenged.code_length === "number"
      ? challenged.code_length
      : 6;
    const channelLabel = typeof challenged.challenge_target_label === "string" &&
      challenged.challenge_target_label.trim()
      ? challenged.challenge_target_label.trim()
      : email;
    const nowMs = Date.now();
    const state: SignupFlowState = {
      channelLabel,
      codeLength,
      continuationToken,
      displayName,
      email,
      exp: Math.floor(nowMs / 1000) + signupFlowTtlSeconds,
      lastSentAt: nowMs,
    };
    const response = NextResponse.json({
      channelLabel,
      codeLength,
      ok: true,
    });
    await setSignupFlowCookie(state, response.cookies);
    return response;
  } catch (error) {
    return nativeSignupError(error, email);
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
    throw new Error("Missing sign-up continuation.");
  }
  return value;
}

function nativeSignupError(error: unknown, email: string) {
  if (!(error instanceof NativeAuthError)) {
    return signupJsonError("We could not start sign-up. Please try again.", 502);
  }

  if (error.appCode === "user_already_exists") {
    return NextResponse.json({
      error: "Account exists. Please sign in.",
      fallbackRedirect: `/sign-in?email=${encodeURIComponent(email)}`,
      ok: false,
    }, { status: 409 });
  }

  if (error.appCode === entryPolicyCode) {
    return signupJsonError("Use a stronger sign-up value.", 400);
  }

  if (error.appCode === "redirect_required") {
    return NextResponse.json({
      error: "Continue in browser.",
      fallbackRedirect: `/api/auth/login?loginHint=${encodeURIComponent(email)}`,
      ok: false,
    }, { status: 409 });
  }

  if (error.appCode === "rate_limited") {
    return signupJsonError("Too many requests. Please try again later.", 429);
  }

  return signupJsonError("We could not start sign-up. Please try again.", 502);
}

function signupJsonError(error: string, status: number) {
  return NextResponse.json({ error, ok: false }, { status });
}
