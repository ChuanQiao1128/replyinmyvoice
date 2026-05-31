import { getCurrentAccessToken } from "./entra-auth";
import { optionalEnv } from "./env";

export type AzureAccountUsage = {
  scope: "free" | "paid";
  periodKey: string;
  quota: number;
  used: number;
  reserved: number;
  remaining: number;
  exhausted: boolean;
  periodEnd: string | null;
};

export type AzureAccountSummary = {
  userId: string;
  externalAuthUserId: string;
  email: string | null;
  subscriptionStatus: string;
  currentPeriodEnd: string | null;
  usage: AzureAccountUsage;
};

export type AzureAccountPayment = {
  sku: string | null;
  paymentIntentId: string | null;
  amount: number | null;
  currency: string | null;
  date: string;
  expiry: string | null;
  remaining: number;
};

export type AzureBillingSupportRequest = {
  id: string;
  userId: string;
  type: "refund" | "billing-question";
  relatedPaymentIntentId: string | null;
  message: string;
  status: "open" | "resolved";
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
};

export function getAzureApiBaseUrl() {
  return optionalEnv(
    "NEXT_PUBLIC_AZURE_API_BASE_URL",
    "https://replyinmyvoice-func-dev.azurewebsites.net",
  ).replace(/\/$/, "");
}

function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const [, encodedPayload] = token.split(".");
  if (!encodedPayload) {
    return null;
  }

  try {
    const padded = encodedPayload
      .replace(/-/g, "+")
      .replace(/_/g, "/")
      .padEnd(Math.ceil(encodedPayload.length / 4) * 4, "=");
    const decoded = atob(padded);
    const parsed = JSON.parse(decoded) as unknown;
    return parsed && typeof parsed === "object" && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : null;
  } catch {
    return null;
  }
}

function stringClaim(claims: Record<string, unknown>, name: string) {
  const value = claims[name];
  return typeof value === "string" && value.trim() ? value : null;
}

function stringArrayClaim(claims: Record<string, unknown>, name: string) {
  const value = claims[name];
  if (typeof value === "string") {
    return value.split(/\s+/).filter(Boolean);
  }
  if (Array.isArray(value)) {
    return value.filter((candidate): candidate is string => typeof candidate === "string");
  }
  return [];
}

function summarizeAudience(value: unknown) {
  const values = Array.isArray(value) ? value : [value];
  return values
    .filter((candidate): candidate is string => typeof candidate === "string" && !!candidate)
    .map((candidate) => {
      if (candidate.startsWith("api://")) {
        return `api-uri:${candidate.slice(-8)}`;
      }
      if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(candidate)) {
        return `guid:${candidate.slice(-8)}`;
      }
      try {
        return `url-host:${new URL(candidate).host}`;
      } catch {
        return `other:${candidate.length}`;
      }
    });
}

export function summarizeAccessTokenForLog(token: string) {
  const claims = decodeJwtPayload(token);
  if (!claims) {
    return { parseable: false };
  }

  const issuer = stringClaim(claims, "iss");
  const roles = stringArrayClaim(claims, "roles");
  const scopes = stringArrayClaim(claims, "scp");

  return {
    aud: summarizeAudience(claims.aud),
    hasOid: Boolean(stringClaim(claims, "oid")),
    hasRoles: roles.length > 0,
    hasScp: scopes.length > 0,
    hasSub: Boolean(stringClaim(claims, "sub")),
    issHost: issuer ? new URL(issuer).host : null,
    roleCount: roles.length,
    scopeNames: scopes,
    ver: stringClaim(claims, "ver"),
  };
}

export async function fetchAzureAccountSummary(): Promise<AzureAccountSummary | null> {
  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    console.warn("azure_account_summary_missing_access_token");
    return null;
  }

  const response = await fetch(`${getAzureApiBaseUrl()}/api/me`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
    cache: "no-store",
  });

  if (response.status === 401 || response.status === 403) {
    console.warn("azure_account_summary_auth_rejected", {
      status: response.status,
      token: summarizeAccessTokenForLog(accessToken),
    });
    return null;
  }

  if (!response.ok) {
    throw new Error(`Azure account request failed with ${response.status}.`);
  }

  return (await response.json()) as AzureAccountSummary;
}
