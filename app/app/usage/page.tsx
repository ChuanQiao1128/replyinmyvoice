import type { Metadata } from "next";

import {
  DeveloperUpsell,
  PageHeader,
} from "../../../components/app/shell/shell-primitives";
import { isDeveloperTierStatus } from "../../../components/app/shell/shell-types";
import { DeveloperDashboard } from "../../../components/developers/developer-dashboard";
import { fetchAzureAccountSummary } from "../../../lib/azure-api";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Usage" };

export default async function UsagePage() {
  const account = await fetchAzureAccountSummary();
  const subscriptionStatus = account?.subscriptionStatus ?? "inactive";

  if (!isDeveloperTierStatus(subscriptionStatus)) {
    return (
      <>
        <PageHeader
          title="Usage"
          description="API call volume, quota, and recent requests."
        />
        <DeveloperUpsell />
      </>
    );
  }

  return (
    <div className="rimv">
      <DeveloperDashboard
        initialTab="usage"
        paymentGraceEndsAt={account?.paymentGraceEndsAt ?? null}
        subscriptionStatus={subscriptionStatus}
      />
    </div>
  );
}
