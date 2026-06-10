import type { Metadata } from "next";

import { DeveloperDashboard } from "../../../components/developers/developer-dashboard";
import { fetchAzureAccountSummary } from "../../../lib/azure-api";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Usage" };

export default async function UsagePage() {
  const account = await fetchAzureAccountSummary();

  return (
    <div className="rimv">
      <DeveloperDashboard
        initialTab="usage"
        paymentGraceEndsAt={account?.paymentGraceEndsAt ?? null}
        subscriptionStatus={account?.subscriptionStatus ?? "inactive"}
      />
    </div>
  );
}
