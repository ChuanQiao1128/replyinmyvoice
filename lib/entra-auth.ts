import { cookies } from "next/headers";

import { getAppUrl, optionalEnv, requireEnv } from "./env";

const textEncoder = new TextEncoder();

export const sessionCookieName = "rimv_session";
export const oauthStateCookieName = "rimv_oauth";

export type AuthSession = {
  sub: string;
  email: string | null;
  name: string | null;
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
  domainHint?: "google";
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

export function getEntraAuthority() {
  return requireEnv("NEXT_PUBLIC_ENTRA_AUTHORITY").replace(/\/$/, "");
}

export function getEntraClientId() {
  return requireEnv("NEXT_PUBLIC_ENTRA_CLIENT_ID");
}

export function getEntraRedirectUri() {
  return `${getAppUrl()}/auth/callback`;
}

function getSessionSecret(): string;
function getSessionSecret(options: { required: false }): string | null;
function getSessionSecret({ required = true }: { required?: boolean } = {}): string | null {
  const value =
    optionalEnv("AUTH_SESSION_SECRET") ||
    optionalEnv("CLERK_SECRET_KEY") ||
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
  url.searchParams.set("scope", "openid profile email");
  url.searchParams.set("response_mode", "query");
  url.searchParams.set("state", input.state);
  url.searchParams.set("nonce", input.nonce);
  url.searchParams.set("code_challenge", codeChallenge);
  url.searchParams.set("code_challenge_method", "S256");

  if (input.domainHint) {
    url.searchParams.set("domain_hint", input.domainHint);
  }

  return url;
}

export async function createLoginRedirectUrl(redirectTo = "/app") {
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
    domainHint: "google",
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
      body: new URLSearchParams({
        client_id: getEntraClientId(),
        grant_type: "authorization_code",
        code,
        redirect_uri: getEntraRedirectUri(),
        code_verifier: oauthState.codeVerifier,
        scope: "openid profile email",
      }),
    },
  );

  const payload = (await tokenResponse.json().catch(() => null)) as {
    id_token?: string;
    error?: string;
  } | null;

  if (!tokenResponse.ok || !payload?.id_token) {
    throw new Error(`Entra token exchange failed: ${payload?.error ?? tokenResponse.status}`);
  }

  const claims = parseJwtPayload(payload.id_token);
  const session = claims ? validateIdTokenClaims(claims) : null;
  if (!session) {
    throw new Error("Invalid Entra identity token.");
  }

  const sessionCookie = await createSignedCookieValue(session, getSessionSecret());
  cookieStore.set(sessionCookieName, sessionCookie, {
    httpOnly: true,
    secure: getAppUrl().startsWith("https://"),
    sameSite: "lax",
    path: "/",
    maxAge: Math.max(60, session.exp - Math.floor(Date.now() / 1000)),
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

export async function clearCurrentSession() {
  const cookieStore = await cookies();
  cookieStore.delete(sessionCookieName);
  cookieStore.delete(oauthStateCookieName);
}
