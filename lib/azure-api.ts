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

export function getAzureApiBaseUrl() {
  return optionalEnv(
    "NEXT_PUBLIC_AZURE_API_BASE_URL",
    "https://replyinmyvoice-func-dev.azurewebsites.net",
  ).replace(/\/$/, "");
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
    console.warn("azure_account_summary_auth_rejected", { status: response.status });
    return null;
  }

  if (!response.ok) {
    throw new Error(`Azure account request failed with ${response.status}.`);
  }

  return (await response.json()) as AzureAccountSummary;
}
