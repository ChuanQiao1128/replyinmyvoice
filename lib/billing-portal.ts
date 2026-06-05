import { azureApiFetch } from "./client-azure-api";

export async function openBillingPortal() {
  const response = await azureApiFetch("/api/stripe/portal", { method: "POST" });
  const payload = (await response.json()) as { url?: string; error?: string };

  if (!response.ok || !payload.url) {
    throw new Error(payload.error ?? "Could not open billing.");
  }

  window.location.href = payload.url;
}
