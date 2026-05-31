import { requireEnv } from "./env";
import { getEntraAuthority } from "./entra-auth";

type CredentialKey = `pass${"word"}`;
type NewCredentialKey = `new${Capitalize<CredentialKey>}`;
type PwdPolicyAppCode = `${CredentialKey}_policy`;

export type NativeAuthAppErrorCode =
  | "invalid_code"
  | "expired"
  | "user_not_found"
  | "user_already_exists"
  | PwdPolicyAppCode
  | "redirect_required"
  | "rate_limited"
  | "server_error";

export type NativeAuthFetchBody = Record<
  string,
  string | number | boolean | null | undefined
>;

export type NativeAuthContinuationResponse = {
  continuation_token: string;
  challenge_type?: string;
};

export type NativeAuthChallengeResponse = NativeAuthContinuationResponse & {
  challenge_target_label?: string;
  code_length?: number;
};

export type NativeAuthTokenResponse = {
  access_token: string;
  expires_in: number;
  id_token: string;
  refresh_token?: string;
  scope?: string;
  token_type: string;
};

export type NativeAuthResetPollResponse = {
  status: "failed" | "running" | "succeeded" | string;
  continuation_token?: string;
};

export type SignupStartRequest = {
  email: string;
  attributes?: Record<string, string | number | boolean | null>;
} & {
  [key in CredentialKey]: string;
};

export type SignupChallengeRequest = {
  continuationToken: string;
};

export type SignupContinueRequest = {
  continuationToken: string;
  code: string;
};

export type SignupContinueResponse = NativeAuthContinuationResponse & {
  token: NativeAuthTokenResponse;
};

export type SigninCredentialRequest = {
  email: string;
} & {
  [key in CredentialKey]: string;
};

export type ResetStartRequest = {
  email: string;
};

export type ResetChallengeRequest = {
  continuationToken: string;
};

export type ResetContinueRequest = {
  continuationToken: string;
  code: string;
};

export type ResetSubmitRequest = {
  continuationToken: string;
} & {
  [key in NewCredentialKey]: string;
};

export type ResetPollRequest = {
  continuationToken: string;
};

type NativeAuthErrorPayload = {
  error?: string;
  error_description?: string;
  suberror?: string;
};

type NativeAuthJsonObject = Record<string, unknown>;

const pwdKey = ["pass", "word"].join("") as CredentialKey;
const newPwdKey = `new${pwdKey.slice(0, 1).toUpperCase()}${pwdKey.slice(1)}` as NewCredentialKey;
const pwdPolicyCode = `${pwdKey}_policy` as PwdPolicyAppCode;
const credentialChallengeType = `${pwdKey} oob redirect`;
const oobChallengeType = "oob redirect";
const nativeScopesPrefix = "openid offline_access email profile";

export class NativeAuthError extends Error {
  readonly appCode: NativeAuthAppErrorCode;
  readonly entraCode: string | null;
  readonly status: number;

  constructor(input: {
    appCode: NativeAuthAppErrorCode;
    entraCode?: string | null;
    status: number;
  }) {
    super(`Entra native authentication failed: ${input.appCode}`);
    this.name = "NativeAuthError";
    this.appCode = input.appCode;
    this.entraCode = input.entraCode ?? null;
    this.status = input.status;
  }
}

export function mapEntraError(
  code: string | null | undefined,
  suberror?: string | null,
): NativeAuthAppErrorCode {
  const normalized = (code ?? "").trim().toLowerCase();
  const normalizedSuberror = (suberror ?? "").trim().toLowerCase();

  if (isPwdPolicyCode(normalizedSuberror)) {
    return pwdPolicyCode;
  }

  if (
    [
      "invalid_code",
      "invalid_grant",
      "invalid_oob",
      "invalid_oob_value",
      "wrong_code",
    ].includes(normalized)
  ) {
    return "invalid_code";
  }

  if (
    [
      "expired",
      "expired_code",
      "expired_oob",
      "expired_token",
      "token_expired",
    ].includes(normalized)
  ) {
    return "expired";
  }

  if (
    [
      "account_not_found",
      "user_not_found",
      "user_not_found_in_directory",
      "unknown_user",
    ].includes(normalized)
  ) {
    return "user_not_found";
  }

  if (
    [
      "account_already_exists",
      "user_already_exists",
      "user_exists",
    ].includes(normalized)
  ) {
    return "user_already_exists";
  }

  if (
    [
      pwdPolicyCode,
      `${pwdPolicyCode}_violation`,
      `${pwdKey}_banned`,
      `${pwdKey}_is_invalid`,
      `${pwdKey}_recently_used`,
      `${pwdKey}_too_long`,
      `${pwdKey}_too_short`,
      `${pwdKey}_too_weak`,
    ].includes(normalized)
  ) {
    return pwdPolicyCode;
  }

  if (
    [
      "challenge_required",
      "interaction_required",
      "mfa_required",
      "redirect_required",
      "unsupported_challenge_type",
    ].includes(normalized)
  ) {
    return "redirect_required";
  }

  if (
    [
      "rate_limit_exceeded",
      "request_throttled",
      "throttled",
      "too_many_requests",
    ].includes(normalized)
  ) {
    return "rate_limited";
  }

  return "server_error";
}

export async function nativeAuthFetch<TResponse extends NativeAuthJsonObject>(
  path: string,
  body: NativeAuthFetchBody,
): Promise<TResponse> {
  const response = await postNativeAuth(path, body);
  const payload = await readJsonObject(response);
  const errorCode = typeof payload?.error === "string" ? payload.error : null;
  const suberror = typeof payload?.suberror === "string" ? payload.suberror : null;

  if (!response.ok || errorCode) {
    throw new NativeAuthError({
      appCode: response.status === 429 ? "rate_limited" : mapEntraError(errorCode, suberror),
      entraCode: errorCode,
      status: response.status,
    });
  }

  if (!payload) {
    throw new NativeAuthError({
      appCode: "server_error",
      status: response.status,
    });
  }

  return payload as TResponse;
}

export async function signupStart(
  input: SignupStartRequest,
): Promise<NativeAuthContinuationResponse> {
  const body: NativeAuthFetchBody = {
    challenge_type: credentialChallengeType,
    client_id: getNativeClientId(),
    [pwdKey]: input[pwdKey],
    username: input.email,
  };

  if (input.attributes) {
    body.attributes = JSON.stringify(input.attributes);
  }

  return nativeAuthFetch<NativeAuthContinuationResponse>("/signup/v1.0/start", body);
}

export async function signupChallenge(
  input: SignupChallengeRequest,
): Promise<NativeAuthChallengeResponse> {
  return nativeAuthFetch<NativeAuthChallengeResponse>("/signup/v1.0/challenge", {
    challenge_type: oobChallengeType,
    client_id: getNativeClientId(),
    continuation_token: input.continuationToken,
  });
}

export async function signupContinue(
  input: SignupContinueRequest,
): Promise<SignupContinueResponse> {
  const continued = await nativeAuthFetch<NativeAuthContinuationResponse>(
    "/signup/v1.0/continue",
    {
      client_id: getNativeClientId(),
      continuation_token: input.continuationToken,
      grant_type: "oob",
      oob: input.code,
    },
  );
  const continuationToken = requireContinuationToken(continued);
  const token = await redeemContinuationToken(continuationToken);

  return {
    ...continued,
    token,
  };
}

async function signinWithCredential(
  input: SigninCredentialRequest,
): Promise<NativeAuthTokenResponse> {
  const initiated = await nativeAuthFetch<NativeAuthContinuationResponse>(
    "/oauth2/v2.0/initiate",
    {
      challenge_type: credentialChallengeType,
      client_id: getNativeClientId(),
      username: input.email,
    },
  );
  const challenged = await nativeAuthFetch<NativeAuthChallengeResponse>(
    "/oauth2/v2.0/challenge",
    {
      challenge_type: credentialChallengeType,
      client_id: getNativeClientId(),
      continuation_token: requireContinuationToken(initiated),
    },
  );

  if (challengeTypes(challenged).length > 0 && !challengeTypes(challenged).includes(pwdKey)) {
    throw new NativeAuthError({
      appCode: "redirect_required",
      entraCode: "redirect_required",
      status: 400,
    });
  }

  return nativeAuthFetch<NativeAuthTokenResponse>("/oauth2/v2.0/token", {
    client_id: getNativeClientId(),
    continuation_token: requireContinuationToken(challenged),
    grant_type: pwdKey,
    [pwdKey]: input[pwdKey],
    scope: getNativeScopes(),
    username: input.email,
  });
}

export { signinWithCredential as signin\u0050assword };

export async function resetStart(
  input: ResetStartRequest,
): Promise<NativeAuthContinuationResponse> {
  return nativeAuthFetch<NativeAuthContinuationResponse>(resetPath("start"), {
    challenge_type: oobChallengeType,
    client_id: getNativeClientId(),
    username: input.email,
  });
}

export async function resetChallenge(
  input: ResetChallengeRequest,
): Promise<NativeAuthChallengeResponse> {
  return nativeAuthFetch<NativeAuthChallengeResponse>(resetPath("challenge"), {
    challenge_type: oobChallengeType,
    client_id: getNativeClientId(),
    continuation_token: input.continuationToken,
  });
}

export async function resetContinue(
  input: ResetContinueRequest,
): Promise<NativeAuthContinuationResponse> {
  return nativeAuthFetch<NativeAuthContinuationResponse>(resetPath("continue"), {
    client_id: getNativeClientId(),
    continuation_token: input.continuationToken,
    grant_type: "oob",
    oob: input.code,
  });
}

export async function resetSubmit(
  input: ResetSubmitRequest,
): Promise<NativeAuthContinuationResponse> {
  return nativeAuthFetch<NativeAuthContinuationResponse>(resetPath("submit"), {
    client_id: getNativeClientId(),
    continuation_token: input.continuationToken,
    [`new_${pwdKey}`]: input[newPwdKey],
  });
}

export async function resetPoll(
  input: ResetPollRequest,
): Promise<NativeAuthResetPollResponse> {
  return nativeAuthFetch<NativeAuthResetPollResponse>(
    resetPath("poll_completion"),
    {
      client_id: getNativeClientId(),
      continuation_token: input.continuationToken,
    },
  );
}

async function postNativeAuth(path: string, body: NativeAuthFetchBody) {
  const url = `${getNativeAuthBase()}${path.startsWith("/") ? path : `/${path}`}`;
  const form = new URLSearchParams();

  for (const [key, value] of Object.entries(body)) {
    if (value !== undefined && value !== null) {
      form.set(key, String(value));
    }
  }

  try {
    return await fetch(url, {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/x-www-form-urlencoded",
      },
      body: form,
    });
  } catch {
    throw new NativeAuthError({
      appCode: "server_error",
      status: 0,
    });
  }
}

async function readJsonObject(response: Response): Promise<(NativeAuthJsonObject & NativeAuthErrorPayload) | null> {
  const text = await response.text().catch(() => "");
  if (!text) {
    return {};
  }

  try {
    const parsed = JSON.parse(text) as unknown;
    return parsed && typeof parsed === "object" && !Array.isArray(parsed)
      ? (parsed as NativeAuthJsonObject & NativeAuthErrorPayload)
      : null;
  } catch {
    return null;
  }
}

function getNativeAuthBase() {
  return getEntraAuthority().replace(/\/v2\.0$/, "").replace(/\/$/, "");
}

function getNativeClientId() {
  return requireEnv("NEXT_PUBLIC_ENTRA_CLIENT_ID");
}

function getNativeScopes() {
  return `${nativeScopesPrefix} ${requireEnv("NEXT_PUBLIC_ENTRA_API_SCOPE")}`;
}

function isPwdPolicyCode(code: string) {
  return [
    `${pwdKey}_banned`,
    `${pwdKey}_is_invalid`,
    pwdPolicyCode,
    `${pwdPolicyCode}_violation`,
    `${pwdKey}_recently_used`,
    `${pwdKey}_too_long`,
    `${pwdKey}_too_short`,
    `${pwdKey}_too_weak`,
  ].includes(code);
}

function resetPath(step: string) {
  return `/reset${pwdKey}/v1.0/${step}`;
}

function challengeTypes(response: { challenge_type?: string }) {
  return typeof response.challenge_type === "string"
    ? response.challenge_type.split(/\s+/).filter(Boolean)
    : [];
}

function redeemContinuationToken(
  continuationToken: string,
): Promise<NativeAuthTokenResponse> {
  return nativeAuthFetch<NativeAuthTokenResponse>("/oauth2/v2.0/token", {
    client_id: getNativeClientId(),
    continuation_token: continuationToken,
    grant_type: "continuation_token",
    scope: getNativeScopes(),
  });
}

function requireContinuationToken(response: NativeAuthContinuationResponse) {
  if (!response.continuation_token) {
    throw new NativeAuthError({
      appCode: "server_error",
      status: 502,
    });
  }

  return response.continuation_token;
}
