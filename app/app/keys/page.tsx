import type { Metadata } from "next";

import { DeveloperDashboard } from "../../../components/developers/developer-dashboard";
import { fetchAzureAccountSummary } from "../../../lib/azure-api";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "API keys" };

// The developer console (keys / usage / billing tabs) now lives inside the app
// shell. /developers/keys permanently redirects here.
export default async function KeysPage() {
  const account = await fetchAzureAccountSummary();

  return (
    <div className="rimv">
      <DeveloperDashboard
        initialTab="keys"
        paymentGraceEndsAt={account?.paymentGraceEndsAt ?? null}
        subscriptionStatus={account?.subscriptionStatus ?? "inactive"}
      />
    </div>
  );
}
