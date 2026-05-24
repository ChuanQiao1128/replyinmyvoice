import { cookies } from "next/headers";

import { getAppUrl, optionalEnv, requireEnv } from "./env";

const textEncoder = new TextEncoder();

export const sessionCookieName = "rimv_session";
export const oauthStateCookieName = "rimv_oauth";
const accessTokenCookieName = "rimv_access";
const accessTokenCookieChunkSize = 3000;
const accessTokenCookieMaxChunks = 8;

export type AuthSession = {
  sub: string;
  email: string | null;
  name: string | null;
  exp: number;
  accessToken?: string;
  accessTokenExp?: number;
  refreshToken?: string;
};

type BrowserSessionCookie = Pick<AuthSession, "sub" | "email" | "name" | "exp">;

type AccessTokenCookie = {
  accessToken: string;
  accessTokenExp: number;
  exp: number;
};

type OAuthState = {
  state: string;
  nonce: string;
  codeVerifier: string;
  redirectTo: string;
  exp: number;
};

export type EntraAuthorizeUrlInput = {
  authority: string;
  clientId: string;
  redirectUri: string;
  state: string;
  nonce: string;
  codeVerifier: string;
  loginHint?: string;
  prompt?: "select_account";
};

export type EntraTokenRequestBodyInput = {
  clientId: string;
  code: string;
  redirectUri: string;
  codeVerifier: string;
  clientSecret?: string;
};

export type EntraBearerTokenValidationResult =
  | { ok: true; user: AuthSession }
  | { ok: false; status: 401 | 403; error: string };

export type EntraBearerTokenValidationOptions = {
  audience?: string;
  issuer?: string;
  jwksUrl?: string;
  requiredScope?: string;
};

function base64UrlEncode(input: Uint8Array | string) {
  const buffer =
    typeof input === "string"
      ? Buffer.from(input, "utf8")
      : Buffer.from(input);
  return buffer.toString("base64url");
}

function base64UrlDecode(input: string) {
  return Buffer.from(input, "base64url").toString("utf8");
}

function base64UrlDecodeArrayBuffer(input: string) {
  const buffer = Buffer.from(input, "base64url");
  return buffer.buffer.slice(
    buffer.byteOffset,
    buffer.byteOffset + buffer.byteLength,
  ) as ArrayBuffer;
}

function randomBase64Url(bytes = 32) {
  const values = new Uint8Array(bytes);
  crypto.getRandomValues(values);
  return base64UrlEncode(values);
}

async function sha256Base64Url(value: string) {
  const digest = await crypto.subtle.digest("SHA-256", textEncoder.encode(value));
  return base64UrlEncode(new Uint8Array(digest));
}

async function signPayload(payload: string, secret: string) {
  const key = await crypto.subtle.importKey(
    "raw",
    textEncoder.encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const signature = await crypto.subtle.sign("HMAC", key, textEncoder.encode(payload));
  return base64UrlEncode(new Uint8Array(signature));
}

function timingSafeEqual(left: string, right: string) {
  if (left.length !== right.length) {
    return false;
  }

  let diff = 0;
  for (let index = 0; index < left.length; index += 1) {
    diff |= left.charCodeAt(index) ^ right.charCodeAt(index);
  }

  return diff === 0;
}

export async function createSignedCookieValue(payload: unknown, secret: string) {
  const encodedPayload = base64UrlEncode(JSON.stringify(payload));
  const signature = await signPayload(encodedPayload, secret);
  return `${encodedPayload}.${signature}`;
}

export async function verifySignedCookieValue<T>(
  cookieValue: string | undefined,
  secret: string,
): Promise<T | null> {
  if (!cookieValue) {
    return null;
  }

  const [encodedPayload, signature] = cookieValue.split(".");
  if (!encodedPayload || !signature) {
    return null;
  }

  const expected = await signPayload(encodedPayload, secret);
  if (!timingSafeEqual(signature, expected)) {
    return null;
  }

  try {
    return JSON.parse(base64UrlDecode(encodedPayload)) as T;
  } catch {
    return null;
  }
}

type MutableCookieStore = Awaited<ReturnType<typeof cookies>>;

function chunkCookieName(baseName: string, index: number) {
  return `${baseName}_${index}`;
}

async function setChunkedSignedCookie(
  cookieStore: MutableCookieStore,
  baseName: string,
  payload: unknown,
  secret: string,
  options: NonNullable<Parameters<MutableCookieStore["set"]>[2]>,
) {
  const value = await createSignedCookieValue(payload, secret);
  const chunks = value.match(new RegExp(`.{1,${accessTokenCookieChunkSize}}`, "g")) ?? [""];
  if (chunks.length > accessTokenCookieMaxChunks) {
    throw new Error("Signed access token cookie is too large.");
  }

  chunks.forEach((chunk, index) => {
    cookieStore.set(chunkCookieName(baseName, index), chunk, options);
  });

  for (let index = chunks.length; index < accessTokenCookieMaxChunks; index += 1) {
    cookieStore.delete(chunkCookieName(baseName, index));
  }
}

async function readChunkedSignedCookie<T>(
  cookieStore: MutableCookieStore,
  baseName: string,
  secret: string,
) {
  const chunks: string[] = [];
  for (let index = 0; index < accessTokenCookieMaxChunks; index += 1) {
    const chunk = cookieStore.get(chunkCookieName(baseName, index))?.value;
    if (!chunk) {
      break;
    }
    chunks.push(chunk);
  }

  if (!chunks.length) {
    return null;
  }

  return verifySignedCookieValue<T>(chunks.join(""), secret);
}

function deleteChunkedCookie(cookieStore: MutableCookieStore, baseName: string) {
  for (let index = 0; index < accessTokenCookieMaxChunks; index += 1) {
    cookieStore.delete(chunkCookieName(baseName, index));
  }
}

export function getEntraAuthority() {
  return requireEnv("NEXT_PUBLIC_ENTRA_AUTHORITY").replace(/\/$/, "");
}

export function getEntraClientId() {
  return requireEnv("NEXT_PUBLIC_ENTRA_CLIENT_ID");
}

export function getEntraRedirectUri() {
  return `${getAppUrl()}/auth/callback`;
}

function getEntraApiScope() {
  return requireEnv("NEXT_PUBLIC_ENTRA_API_SCOPE");
}

function getEntraOAuthScopes() {
  return `openid profile email ${getEntraApiScope()}`;
}

function getEntraJwksUrl() {
  return `${getEntraAuthority().replace(/\/v2\.0$/, "")}/discovery/v2.0/keys`;
}

function getSessionSecret(): string;
function getSessionSecret(options: { required: false }): string | null;
function getSessionSecret({ required = true }: { required?: boolean } = {}): string | null {
  const value =
    optionalEnv("AUTH_SESSION_SECRET") ||
    optionalEnv("STRIPE_WEBHOOK_SECRET");

  if (value) {
    return value;
  }

  if (!required) {
    return null;
  }

  return requireEnv("AUTH_SESSION_SECRET");
}

export async function buildEntraAuthorizeUrl(input: EntraAuthorizeUrlInput) {
  const codeChallenge = await sha256Base64Url(input.codeVerifier);
  const url = new URL(`${input.authority.replace(/\/v2\.0$/, "")}/oauth2/v2.0/authorize`);

  url.searchParams.set("client_id", input.clientId);
  url.searchParams.set("response_type", "code");
  url.searchParams.set("redirect_uri", input.redirectUri);
  url.searchParams.set("scope", getEntraOAuthScopes());
  url.searchParams.set("response_mode", "query");
  url.searchParams.set("state", input.state);
  url.searchParams.set("nonce", input.nonce);
  url.searchParams.set("code_challenge", codeChallenge);
  url.searchParams.set("code_challenge_method", "S256");

  if (input.prompt) {
    url.searchParams.set("prompt", input.prompt);
  }

  if (input.loginHint) {
    url.searchParams.set("login_hint", input.loginHint);
  }

  return url;
}

export function buildEntraTokenRequestBody(input: EntraTokenRequestBodyInput) {
  const body = new URLSearchParams({
    client_id: input.clientId,
    grant_type: "authorization_code",
    code: input.code,
    redirect_uri: input.redirectUri,
    code_verifier: input.codeVerifier,
    scope: getEntraOAuthScopes(),
  });

  if (input.clientSecret) {
    body.set("client_secret", input.clientSecret);
  }

  return body;
}

export async function createLoginRedirectUrl(
  redirectTo = "/app",
  options: { loginHint?: string } = {},
) {
  const state = randomBase64Url(24);
  const nonce = randomBase64Url(24);
  const codeVerifier = randomBase64Url(48);
  const expiresAt = Math.floor(Date.now() / 1000) + 10 * 60;
  const cookieValue = await createSignedCookieValue(
    { state, nonce, codeVerifier, redirectTo, exp: expiresAt } satisfies OAuthState,
    getSessionSecret(),
  );

  const cookieStore = await cookies();
  cookieStore.set(oauthStateCookieName, cookieValue, {
    httpOnly: true,
    secure: getAppUrl().startsWith("https://"),
    sameSite: "lax",
    path: "/",
    maxAge: 10 * 60,
  });

  return buildEntraAuthorizeUrl({
    authority: getEntraAuthority(),
    clientId: getEntraClientId(),
    redirectUri: getEntraRedirectUri(),
    state,
    nonce,
    codeVerifier,
    loginHint: options.loginHint,
    prompt: "select_account",
  });
}

function parseJwtPayload(token: string): Record<string, unknown> | null {
  const [, payload] = token.split(".");
  if (!payload) {
    return null;
  }

  try {
    return JSON.parse(base64UrlDecode(payload)) as Record<string, unknown>;
  } catch {
    return null;
  }
}

function parseJwtObject(value: string): Record<string, unknown> | null {
  try {
    const parsed = JSON.parse(base64UrlDecode(value)) as unknown;
    return parsed && typeof parsed === "object" && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : null;
  } catch {
    return null;
  }
}

function parseJwt(token: string) {
  const parts = token.split(".");
  if (parts.length !== 3 || parts.some((part) => !part)) {
    return null;
  }

  const [encodedHeader, encodedPayload, encodedSignature] = parts;
  const header = parseJwtObject(encodedHeader);
  const claims = parseJwtObject(encodedPayload);
  if (!header || !claims) {
    return null;
  }

  return {
    claims,
    header,
    signature: base64UrlDecodeArrayBuffer(encodedSignature),
    signingInput: `${encodedHeader}.${encodedPayload}`,
  };
}

function stringClaim(claims: Record<string, unknown>, name: string) {
  const value = claims[name];
  return typeof value === "string" && value.trim() ? value : null;
}

function emailClaim(claims: Record<string, unknown>) {
  const direct = stringClaim(claims, "email") ?? stringClaim(claims, "preferred_username");
  if (direct) {
    return direct;
  }

  const emails = claims.emails;
  return Array.isArray(emails) && typeof emails[0] === "string" ? emails[0] : null;
}

function claimIncludes(value: unknown, expected: string) {
  const expectedValues = scopeAlternates(expected);

  if (typeof value === "string") {
    return value
      .split(/\s+/)
      .some((candidate) => expectedValues.includes(candidate));
  }

  return Array.isArray(value) &&
    value.some((candidate) => typeof candidate === "string" && expectedValues.includes(candidate));
}

function scopeAlternates(scope: string) {
  const segments = [scope];
  const slashIndex = scope.lastIndexOf("/");
  if (slashIndex >= 0 && slashIndex < scope.length - 1) {
    segments.push(scope.slice(slashIndex + 1));
  }
  return segments;
}

function audienceMatches(value: unknown, expected: string) {
  return value === expected || (Array.isArray(value) && value.includes(expected));
}

function validateIdTokenClaims(claims: Record<string, unknown>) {
  const now = Math.floor(Date.now() / 1000);
  const exp = typeof claims.exp === "number" ? claims.exp : 0;
  const aud = stringClaim(claims, "aud");
  const sub = stringClaim(claims, "sub") ?? stringClaim(claims, "oid");

  if (!sub || exp <= now || aud !== getEntraClientId()) {
    return null;
  }

  return {
    sub,
    email: emailClaim(claims),
    name: stringClaim(claims, "name"),
    exp: Math.min(exp, now + 7 * 24 * 60 * 60),
  } satisfies AuthSession;
}

async function verifyJwtSignature({
  header,
  jwksUrl,
  signature,
  signingInput,
}: {
  header: Record<string, unknown>;
  jwksUrl: string;
  signature: ArrayBuffer;
  signingInput: string;
}) {
  const kid = stringClaim(header, "kid");
  if (header.alg !== "RS256" || !kid) {
    return false;
  }

  const response = await fetch(jwksUrl);
  const jwks = (await response.json().catch(() => null)) as {
    keys?: (JsonWebKey & { kid?: string })[];
  } | null;
  const key = response.ok ? jwks?.keys?.find((candidate) => candidate.kid === kid) : null;
  if (!key) {
    return false;
  }

  try {
    const cryptoKey = await crypto.subtle.importKey(
      "jwk",
      key,
      { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
      false,
      ["verify"],
    );
    return crypto.subtle.verify(
      "RSASSA-PKCS1-v1_5",
      cryptoKey,
      signature,
      textEncoder.encode(signingInput),
    );
  } catch {
    return false;
  }
}

export async function validateEntraBearerToken(
  authorizationHeader: string | null | undefined,
  options: EntraBearerTokenValidationOptions = {},
): Promise<EntraBearerTokenValidationResult> {
  const token = authorizationHeader?.match(/^Bearer\s+(.+)$/i)?.[1];
  if (!token) {
    return { ok: false, status: 401, error: "unauthorized" };
  }

  const jwt = parseJwt(token);
  if (!jwt) {
    return { ok: false, status: 401, error: "unauthorized" };
  }

  const signatureValid = await verifyJwtSignature({
    header: jwt.header,
    jwksUrl: options.jwksUrl ?? getEntraJwksUrl(),
    signature: jwt.signature,
    signingInput: jwt.signingInput,
  });
  if (!signatureValid) {
    return { ok: false, status: 401, error: "unauthorized" };
  }

  const now = Math.floor(Date.now() / 1000);
  const exp = typeof jwt.claims.exp === "number" ? jwt.claims.exp : 0;
  const nbf = typeof jwt.claims.nbf === "number" ? jwt.claims.nbf : null;
  const sub = stringClaim(jwt.claims, "sub") ?? stringClaim(jwt.claims, "oid");

  if (
    !sub ||
    exp <= now ||
    (nbf !== null && nbf > now) ||
    !audienceMatches(jwt.claims.aud, options.audience ?? getEntraClientId()) ||
    stringClaim(jwt.claims, "iss") !== (options.issuer ?? getEntraAuthority())
  ) {
    return { ok: false, status: 401, error: "unauthorized" };
  }

  const requiredScope = options.requiredScope ?? getEntraApiScope();
  if (
    requiredScope &&
    !claimIncludes(jwt.claims.scp, requiredScope) &&
    !claimIncludes(jwt.claims.roles, requiredScope)
  ) {
    return { ok: false, status: 403, error: "forbidden" };
  }

  return {
    ok: true,
    user: {
      sub,
      email: emailClaim(jwt.claims),
      name: stringClaim(jwt.claims, "name"),
      exp,
    },
  };
}

/**
 * Mints the rimv_session cookie from a freshly issued token set. Keep the
 * browser cookie small: Entra access/refresh tokens can push a signed cookie
 * past the 4KB browser limit, which makes the next /app request look signed out.
 */
export async function createSessionFromTokens(tokens: {
  idToken: string;
  accessToken: string;
  refreshToken?: string | null;
}): Promise<AuthSession> {
  const claims = parseJwtPayload(tokens.idToken);
  const baseSession = claims ? validateIdTokenClaims(claims) : null;
  if (!baseSession) {
    throw new Error("Invalid Entra identity token.");
  }

  const accessClaims = parseJwtPayload(tokens.accessToken);
  const accessTokenExp =
    accessClaims && typeof accessClaims.exp === "number" ? accessClaims.exp : baseSession.exp;

  const session = {
    ...baseSession,
    accessToken: tokens.accessToken,
    accessTokenExp,
    refreshToken: tokens.refreshToken ?? undefined,
    exp: Math.min(baseSession.exp, accessTokenExp),
  } satisfies AuthSession;

  const cookieStore = await cookies();
  const cookieOptions = {
    httpOnly: true,
    secure: getAppUrl().startsWith("https://"),
    sameSite: "lax" as const,
    path: "/",
    maxAge: Math.max(60, session.exp - Math.floor(Date.now() / 1000)),
  };
  const browserSession = {
    sub: session.sub,
    email: session.email,
    name: session.name,
    exp: session.exp,
  } satisfies BrowserSessionCookie;
  const secret = getSessionSecret();
  const sessionCookie = await createSignedCookieValue(browserSession, secret);
  cookieStore.set(sessionCookieName, sessionCookie, cookieOptions);
  await setChunkedSignedCookie(cookieStore, accessTokenCookieName, {
    accessToken: session.accessToken,
    accessTokenExp: session.accessTokenExp,
    exp: session.exp,
  } satisfies AccessTokenCookie, secret, cookieOptions);

  return session;
}

export const signupFlowCookieName = "rimv_signup";

/**
 * Short-lived signed cookie that threads the native-auth email+password sign-up flow
 * (email-verification step) across requests. Sign-in is single-shot and needs no cookie.
 */
export type SignupFlowState = {
  continuationToken: string;
  email: string;
  displayName: string | null;
  codeLength: number;
  channelLabel: string | null;
  lastSentAt: number;
  exp: number;
};

export async function setSignupFlowCookie(state: SignupFlowState) {
  const cookieStore = await cookies();
  const value = await createSignedCookieValue(state, getSessionSecret());
  cookieStore.set(signupFlowCookieName, value, {
    httpOnly: true,
    secure: getAppUrl().startsWith("https://"),
    sameSite: "lax",
    path: "/",
    maxAge: Math.max(60, state.exp - Math.floor(Date.now() / 1000)),
  });
}

export async function readSignupFlowCookie(): Promise<SignupFlowState | null> {
  const cookieStore = await cookies();
  const state = await verifySignedCookieValue<SignupFlowState>(
    cookieStore.get(signupFlowCookieName)?.value,
    getSessionSecret(),
  );
  if (!state || state.exp <= Math.floor(Date.now() / 1000)) {
    return null;
  }
  return state;
}

export async function clearSignupFlowCookie() {
  const cookieStore = await cookies();
  cookieStore.delete(signupFlowCookieName);
}

export async function completeEntraCallback(requestUrl: string) {
  const url = new URL(requestUrl);
  const code = url.searchParams.get("code");
  const state = url.searchParams.get("state");

  if (!code || !state) {
    throw new Error("Missing Entra callback code or state.");
  }

  const cookieStore = await cookies();
  const oauthState = await verifySignedCookieValue<OAuthState>(
    cookieStore.get(oauthStateCookieName)?.value,
    getSessionSecret(),
  );

  if (
    !oauthState ||
    oauthState.state !== state ||
    oauthState.exp <= Math.floor(Date.now() / 1000)
  ) {
    throw new Error("Invalid or expired Entra callback state.");
  }

  const tokenResponse = await fetch(
    `${getEntraAuthority().replace(/\/v2\.0$/, "")}/oauth2/v2.0/token`,
    {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: buildEntraTokenRequestBody({
        clientId: getEntraClientId(),
        code,
        redirectUri: getEntraRedirectUri(),
        codeVerifier: oauthState.codeVerifier,
        clientSecret: optionalEnv("ENTRA_CLIENT_SECRET") || undefined,
      }),
    },
  );

  const payload = (await tokenResponse.json().catch(() => null)) as {
    access_token?: string;
    id_token?: string;
    error?: string;
    error_description?: string;
  } | null;

  if (!tokenResponse.ok || !payload?.id_token || !payload.access_token) {
    throw new Error(
      `Entra token exchange failed: ${payload?.error ?? tokenResponse.status} ${payload?.error_description ?? ""}`.trim(),
    );
  }

  const session = await createSessionFromTokens({
    idToken: payload.id_token,
    accessToken: payload.access_token,
  });
  cookieStore.delete(oauthStateCookieName);

  return { session, redirectTo: oauthState.redirectTo || "/app" };
}

export async function getCurrentSession(): Promise<AuthSession | null> {
  const secret = getSessionSecret({ required: false });
  if (!secret) {
    return null;
  }

  const cookieStore = await cookies();
  const session = await verifySignedCookieValue<AuthSession>(
    cookieStore.get(sessionCookieName)?.value,
    secret,
  );

  if (!session || session.exp <= Math.floor(Date.now() / 1000)) {
    return null;
  }

  return session;
}

export async function getCurrentAccessToken(): Promise<string | null> {
  const secret = getSessionSecret({ required: false });
  if (secret) {
    const cookieStore = await cookies();
    const token = await readChunkedSignedCookie<AccessTokenCookie>(
      cookieStore,
      accessTokenCookieName,
      secret,
    );
    const now = Math.floor(Date.now() / 1000);
    if (
      token?.accessToken &&
      token.accessTokenExp > now &&
      token.exp > now
    ) {
      return token.accessToken;
    }
  }

  const session = await getCurrentSession();
  if (!session?.accessToken || !session.accessTokenExp) {
    return null;
  }

  if (session.accessTokenExp <= Math.floor(Date.now() / 1000)) {
    return null;
  }

  return session.accessToken;
}

export async function clearCurrentSession() {
  const cookieStore = await cookies();
  cookieStore.delete(sessionCookieName);
  cookieStore.delete(oauthStateCookieName);
  deleteChunkedCookie(cookieStore, accessTokenCookieName);
}
