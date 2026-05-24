// Server-side client for Microsoft Entra External ID native authentication using the
// EMAIL + PASSWORD method (the tenant's "SignUpSignIn" user flow). A one-time passcode is
// used ONLY to verify the address during sign-up; normal sign-in is email + password.
// All calls run server-side (native-auth endpoints don't allow browser CORS); the resulting
// tokens are handed to lib/entra-auth.ts:createSessionFromTokens to mint the rimv_session
// cookie, so downstream code (middleware, /api/me, .NET) is unchanged.
//
// Flow reference (verified 2026-05-24):
//   https://learn.microsoft.com/en-us/entra/identity-platform/reference-native-authentication-api
//
//   Sign-in:  POST /oauth2/v2.0/initiate (challenge_type "password redirect")
//          -> POST /oauth2/v2.0/challenge -> POST /oauth2/v2.0/token (grant_type=password)
//   Sign-up:  POST /signup/v1.0/start (username+password, challenge_type "oob password redirect")
//          -> POST /signup/v1.0/challenge (emails the code)
//          -> POST /signup/v1.0/continue (grant_type=oob, verify email)
//          -> [POST /signup/v1.0/continue (grant_type=attributes) when attributes_required]
//          -> POST /oauth2/v2.0/token (grant_type=continuation_token, auto sign-in)

import { getEntraAuthority, getEntraClientId } from "./entra-auth";
import { requireEnv } from "./env";

export type NativeAuthErrorCode =
  | "invalid_email"
  | "invalid_credentials"
  | "invalid_code"
  | "code_expired"
  | "flow_expired"
  | "user_not_found"
  | "user_already_exists"
  | "weak_password"
  | "redirect_required"
  | "rate_limited"
  | "unknown";

export type NativeAuthTokens = {
  idToken: string;
  accessToken: string;
  refreshToken: string | null;
  expiresIn: number;
};

export type TokenResult =
  | { ok: true; tokens: NativeAuthTokens }
  | { ok: false; error: NativeAuthErrorCode; description?: string };

export type SignUpStartResult =
  | { ok: true; continuationToken: string; codeLength: number; channelLabel: string | null }
  | { ok: false; error: NativeAuthErrorCode; description?: string };

const SIGNIN_CHALLENGE_TYPE = "password redirect";
const SIGNUP_CHALLENGE_TYPE = "oob password redirect";
const DEFAULT_CODE_LENGTH = 6;
const MIN_PASSWORD_LENGTH = 8;

type RawResponse = { status: number; ok: boolean; data: Record<string, unknown> };

function nativeAuthBase(): string {
  return getEntraAuthority().replace(/\/v2\.0$/, "");
}

function nativeAuthScopes(): string {
  // OIDC scopes are always allowed alongside a single resource's scopes; the custom API
  // scope is that resource so the issued access token is accepted by validateEntraBearerToken.
  return `openid offline_access profile email ${requireEnv("NEXT_PUBLIC_ENTRA_API_SCOPE")}`;
}

async function postForm(path: string, params: Record<string, string>): Promise<RawResponse> {
  const response = await fetch(`${nativeAuthBase()}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams(params),
  });
  let data: Record<string, unknown> = {};
  try {
    data = (await response.json()) as Record<string, unknown>;
  } catch {
    data = {};
  }
  return { status: response.status, ok: response.ok, data };
}

function stringField(data: Record<string, unknown>, key: string): string | null {
  const value = data[key];
  return typeof value === "string" && value.trim() ? value : null;
}

function numberField(data: Record<string, unknown>, key: string): number | null {
  const value = data[key];
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function describe(data: Record<string, unknown>): string | undefined {
  return stringField(data, "error_description") ?? undefined;
}

function mapError(data: Record<string, unknown>): NativeAuthErrorCode {
  const error = (stringField(data, "error") ?? "").toLowerCase();
  const suberror = (stringField(data, "suberror") ?? "").toLowerCase();
  const description = (stringField(data, "error_description") ?? "").toLowerCase();
  const codes = Array.isArray(data.error_codes)
    ? (data.error_codes as unknown[]).filter((c): c is number => typeof c === "number")
    : [];

  if (codes.includes(50034) || description.includes("does not exist")) return "user_not_found";
  if (suberror === "user_already_exists" || description.includes("already exists")) return "user_already_exists";
  if (
    description.includes("password") &&
    (description.includes("weak") ||
      description.includes("complex") ||
      description.includes("requirement") ||
      description.includes("short") ||
      description.includes("length") ||
      description.includes("banned"))
  ) {
    return "weak_password";
  }
  if (suberror === "invalid_oob_value") return "invalid_code";
  if (error === "invalid_grant" && description.includes("expired")) return "code_expired";
  if (error === "invalid_grant") return "invalid_code";
  if (
    error === "expired_token" ||
    suberror === "invalid_continuation_token" ||
    (description.includes("continuation") && description.includes("expired"))
  ) {
    return "flow_expired";
  }
  if (description.includes("throttl") || error === "too_many_requests" || codes.includes(50053)) {
    return "rate_limited";
  }
  return "unknown";
}

async function requestTokens(params: Record<string, string>): Promise<TokenResult> {
  const result = await postForm("/oauth2/v2.0/token", {
    client_id: getEntraClientId(),
    scope: nativeAuthScopes(),
    ...params,
  });
  const idToken = stringField(result.data, "id_token");
  const accessToken = stringField(result.data, "access_token");
  if (!result.ok || !idToken || !accessToken) {
    return { ok: false, error: mapError(result.data), description: describe(result.data) };
  }
  return {
    ok: true,
    tokens: {
      idToken,
      accessToken,
      refreshToken: stringField(result.data, "refresh_token"),
      expiresIn: numberField(result.data, "expires_in") ?? 3600,
    },
  };
}

/** Sign in an existing user with email + password. Single round-trip; no email code. */
export async function signInWithPassword(email: string, password: string): Promise<TokenResult> {
  const username = email.trim();
  if (!username || username.length > 320 || !username.includes("@")) {
    return { ok: false, error: "invalid_email" };
  }
  if (!password) {
    return { ok: false, error: "invalid_credentials" };
  }

  const initiate = await postForm("/oauth2/v2.0/initiate", {
    client_id: getEntraClientId(),
    username,
    challenge_type: SIGNIN_CHALLENGE_TYPE,
  });
  let continuationToken = stringField(initiate.data, "continuation_token");
  if (!initiate.ok || !continuationToken) {
    return { ok: false, error: mapError(initiate.data), description: describe(initiate.data) };
  }

  const challenge = await postForm("/oauth2/v2.0/challenge", {
    client_id: getEntraClientId(),
    continuation_token: continuationToken,
    challenge_type: SIGNIN_CHALLENGE_TYPE,
  });
  if (stringField(challenge.data, "challenge_type") === "redirect") {
    return { ok: false, error: "redirect_required" };
  }
  continuationToken = stringField(challenge.data, "continuation_token");
  if (!challenge.ok || !continuationToken) {
    return { ok: false, error: mapError(challenge.data), description: describe(challenge.data) };
  }

  const tokens = await requestTokens({
    grant_type: "password",
    password,
    continuation_token: continuationToken,
  });
  // A token failure on the password grant is almost always a wrong email/password combo.
  if (!tokens.ok && (tokens.error === "invalid_code" || tokens.error === "unknown")) {
    return { ok: false, error: "invalid_credentials", description: tokens.description };
  }
  return tokens;
}

async function requestSignUpChallenge(continuationToken: string): Promise<SignUpStartResult> {
  const result = await postForm("/signup/v1.0/challenge", {
    client_id: getEntraClientId(),
    continuation_token: continuationToken,
    challenge_type: SIGNUP_CHALLENGE_TYPE,
  });
  if (stringField(result.data, "challenge_type") === "redirect") {
    return { ok: false, error: "redirect_required" };
  }
  const nextToken = stringField(result.data, "continuation_token");
  if (!result.ok || !nextToken) {
    return { ok: false, error: mapError(result.data), description: describe(result.data) };
  }
  return {
    ok: true,
    continuationToken: nextToken,
    codeLength: numberField(result.data, "code_length") ?? DEFAULT_CODE_LENGTH,
    channelLabel: stringField(result.data, "challenge_target_label"),
  };
}

/** Begin sign-up: register the email + password and email the verification code. */
export async function startPasswordSignUp(email: string, password: string): Promise<SignUpStartResult> {
  const username = email.trim();
  if (!username || username.length > 320 || !username.includes("@")) {
    return { ok: false, error: "invalid_email" };
  }
  if (!password || password.length < MIN_PASSWORD_LENGTH) {
    return { ok: false, error: "weak_password" };
  }

  const start = await postForm("/signup/v1.0/start", {
    client_id: getEntraClientId(),
    username,
    password,
    challenge_type: SIGNUP_CHALLENGE_TYPE,
  });
  const continuationToken = stringField(start.data, "continuation_token");
  if (!start.ok || !continuationToken) {
    return { ok: false, error: mapError(start.data), description: describe(start.data) };
  }
  return requestSignUpChallenge(continuationToken);
}

/** Resend the sign-up verification code. */
export async function resendSignUpCode(continuationToken: string): Promise<SignUpStartResult> {
  return requestSignUpChallenge(continuationToken);
}

function deriveDisplayName(displayName: string | null, email: string): string {
  const trimmed = displayName?.trim();
  if (trimmed) return trimmed;
  const local = email.split("@")[0] ?? "";
  return local || "New user";
}

/** Finish sign-up: verify the emailed code, submit required attributes, and mint tokens. */
export async function completeSignUp(
  continuationToken: string,
  code: string,
  email: string,
  displayName: string | null,
): Promise<TokenResult> {
  const oob = code.trim();
  if (!/^\d{4,8}$/.test(oob)) {
    return { ok: false, error: "invalid_code" };
  }

  // Verify the email one-time passcode.
  const verifyResponse = await postForm("/signup/v1.0/continue", {
    client_id: getEntraClientId(),
    continuation_token: continuationToken,
    grant_type: "oob",
    oob,
  });
  let nextToken = stringField(verifyResponse.data, "continuation_token");
  // A genuine OTP failure (wrong/expired code) returns no continuation token.
  if (!nextToken) {
    return { ok: false, error: mapError(verifyResponse.data), description: describe(verifyResponse.data) };
  }

  // attributes_required is returned alongside a continuation token — submit displayName.
  const requiresAttributes =
    (stringField(verifyResponse.data, "error") ?? "").toLowerCase() === "attributes_required" ||
    (stringField(verifyResponse.data, "suberror") ?? "").toLowerCase() === "attributes_required" ||
    Array.isArray(verifyResponse.data.required_attributes);

  if (requiresAttributes) {
    const attributesResponse = await postForm("/signup/v1.0/continue", {
      client_id: getEntraClientId(),
      continuation_token: nextToken,
      grant_type: "attributes",
      attributes: JSON.stringify({ displayName: deriveDisplayName(displayName, email) }),
    });
    nextToken = stringField(attributesResponse.data, "continuation_token");
    if (!nextToken) {
      return { ok: false, error: mapError(attributesResponse.data), description: describe(attributesResponse.data) };
    }
  }

  // Exchange the verified continuation token for tokens (auto sign-in after sign-up).
  return requestTokens({ grant_type: "continuation_token", continuation_token: nextToken });
}
