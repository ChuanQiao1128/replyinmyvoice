"use client";

function getAzureApiBaseUrl() {
  return (
    process.env.NEXT_PUBLIC_AZURE_API_BASE_URL ??
    "https://replyinmyvoice-func-dev.azurewebsites.net"
  ).replace(/\/$/, "");
}

async function getAccessToken() {
  const response = await fetch("/api/auth/access-token", {
    cache: "no-store",
  });
  const payload = (await response.json().catch(() => null)) as {
    accessToken?: string;
    error?: string;
  } | null;

  if (!response.ok || !payload?.accessToken) {
    throw new Error(payload?.error ?? "Authentication required.");
  }

  return payload.accessToken;
}

export async function azureApiFetch(path: string, init: RequestInit = {}) {
  const accessToken = await getAccessToken();
  const headers = new Headers(init.headers);
  headers.set("Authorization", `Bearer ${accessToken}`);

  return fetch(`${getAzureApiBaseUrl()}${path}`, {
    ...init,
    headers,
  });
}
