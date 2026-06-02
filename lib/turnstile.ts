import { isProduction } from "./env";

export const turnstileTestSiteKey = "1x00000000000000000000AA";

const turnstileSiteverifyUrl =
  "https://challenges.cloudflare.com/turnstile/v0/siteverify";
const turnstileTestSecretKey = "1x0000000000000000000000000000000AA";

type VerifyTurnstileInput = {
  clientIp?: string | null;
  token: string;
};

export type TurnstileVerifyResult =
  | { ok: true }
  | { error: "invalid_captcha" | "server_config"; ok: false };

export function getTurnstileSecretKey(): string | null {
  const configured = process.env.TURNSTILE_SECRET_KEY?.trim();
  if (configured) {
    return configured;
  }

  if (!isProduction()) {
    return turnstileTestSecretKey;
  }

  return null;
}

export async function verifyTurnstileToken(
  input: VerifyTurnstileInput,
): Promise<TurnstileVerifyResult> {
  const token = input.token.trim();
  if (!token) {
    return { error: "invalid_captcha", ok: false };
  }

  const secret = getTurnstileSecretKey();
  if (!secret) {
    return { error: "server_config", ok: false };
  }

  const body = new URLSearchParams({
    response: token,
    secret,
  });
  const clientIp = input.clientIp?.trim();
  if (clientIp) {
    body.set("remoteip", clientIp);
  }

  try {
    const response = await fetch(turnstileSiteverifyUrl, {
      body,
      cache: "no-store",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
      },
      method: "POST",
    });

    if (!response.ok) {
      return { error: "invalid_captcha", ok: false };
    }

    const result = await response.json() as { success?: unknown };
    return result.success === true
      ? { ok: true }
      : { error: "invalid_captcha", ok: false };
  } catch {
    return { error: "invalid_captcha", ok: false };
  }
}
