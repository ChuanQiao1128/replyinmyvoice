import { NextResponse } from "next/server";

import {
  clientIpFromRequest,
  cloudflareClientIpFromRequest,
} from "../../../../lib/auth-rate-limit";
import { getAzureApiBaseUrl } from "../../../../lib/azure-api";
import { getCurrentAccessToken } from "../../../../lib/entra-auth";
import { isProduction } from "../../../../lib/env";
import { requireSameOrigin } from "../../../../lib/http";
import { verifyTurnstileToken } from "../../../../lib/turnstile";

export const dynamic = "force-dynamic";

type PromoRedeemPayload = {
  code: string;
  turnstileToken: string;
};

type PromoProxyConfig =
  | { clientIp: string; proxySecret: string; ready: true }
  | { error: "server_config"; ready: false };

function errorResponse(error: string, status: number) {
  return NextResponse.json({ error }, { status });
}

function serverConfigError() {
  return errorResponse("server_config", 500);
}

function proxyConfigFromRequest(request: Request): PromoProxyConfig {
  const proxySecret = process.env.PROMO_PROXY_SHARED_SECRET?.trim();
  if (!proxySecret) {
    return { error: "server_config", ready: false };
  }

  const cloudflareIp = cloudflareClientIpFromRequest(request);
  if (cloudflareIp) {
    return { clientIp: cloudflareIp, proxySecret, ready: true };
  }

  if (isProduction()) {
    return { error: "server_config", ready: false };
  }

  return {
    clientIp: clientIpFromRequest(request),
    proxySecret,
    ready: true,
  };
}

async function readRedeemPayload(request: Request): Promise<PromoRedeemPayload | null> {
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return null;
  }

  if (!body || typeof body !== "object") {
    return null;
  }

  const record = body as Record<string, unknown>;
  const code = record.code;
  const turnstileToken = record.turnstileToken;
  if (typeof code !== "string" || !code.trim()) {
    return null;
  }

  if (typeof turnstileToken !== "string") {
    return {
      code,
      turnstileToken: "",
    };
  }

  return {
    code,
    turnstileToken,
  };
}

async function currentAuthorizationHeader() {
  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    return null;
  }

  return `Bearer ${accessToken}`;
}

async function forwardAzureResponse(response: Response) {
  const headers = new Headers();
  const contentType = response.headers.get("content-type");
  if (contentType) {
    headers.set("Content-Type", contentType);
  }

  if (response.status === 204) {
    return new NextResponse(null, {
      headers,
      status: response.status,
    });
  }

  return new NextResponse(await response.text(), {
    headers,
    status: response.status,
  });
}

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const payload = await readRedeemPayload(request);
  if (!payload) {
    return errorResponse("invalid_request", 400);
  }

  if (!payload.turnstileToken.trim()) {
    return errorResponse("invalid_captcha", 403);
  }

  const proxyConfig = proxyConfigFromRequest(request);
  if (!proxyConfig.ready) {
    return serverConfigError();
  }

  const verification = await verifyTurnstileToken({
    clientIp: proxyConfig.clientIp,
    token: payload.turnstileToken,
  });
  if (!verification.ok) {
    return verification.error === "server_config"
      ? serverConfigError()
      : errorResponse("invalid_captcha", 403);
  }

  const authorization = await currentAuthorizationHeader();
  if (!authorization) {
    return errorResponse("authentication_required", 401);
  }

  const response = await fetch(`${getAzureApiBaseUrl()}/api/promo/redeem`, {
    body: JSON.stringify(payload),
    cache: "no-store",
    headers: {
      Authorization: authorization,
      "Content-Type": "application/json",
      "X-Client-IP": proxyConfig.clientIp,
      "X-RIMV-Proxy-Secret": proxyConfig.proxySecret,
    },
    method: "POST",
  });

  return forwardAzureResponse(response);
}
