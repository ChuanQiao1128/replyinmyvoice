import { redirect } from "next/navigation";

import { PaywallCard } from "../../components/app/paywall-card";
import { RewriteWorkspace } from "../../components/app/rewrite-workspace";
import { SiteHeader } from "../../components/site-header";
import { fetchAzureAccountSummary } from "../../lib/azure-api";

export const dynamic = "force-dynamic";

const paidStatuses = new Set(["active", "trialing", "testing"]);

export default async function AppPage() {
  const account = await fetchAzureAccountSummary();

  if (!account) {
    redirect("/sign-in");
  }

  const usage = account.usage;
  const paid = usage.scope === "paid" || paidStatuses.has(account.subscriptionStatus);

  if (usage.exhausted && !paid) {
    return (
      <>
        <SiteHeader />
        <PaywallCard
          description="Your 3 free rewrites have been used. Buy a rewrite pack — Quick Pack for 10 or Value Pack for 30 — or go Pro/API for 90 rewrites a month and API access."
          status="Free quota used"
          title="Keep writing in your own voice."
        />
      </>
    );
  }

  if (usage.exhausted && paid) {
    return (
      <>
        <SiteHeader />
        <PaywallCard
          action="portal"
          description="Your monthly rewrite quota has been used for this billing period. Buy a one-time pack to keep going now, or manage billing and come back when your next period starts."
          status="Monthly quota used"
          title="Your monthly limit has been reached."
        />
      </>
    );
  }

  const usageLabel = paid
    ? `${usage.remaining} of ${usage.quota} rewrites remaining this billing period`
    : `${usage.remaining} of ${usage.quota} free rewrites remaining`;

  return (
    <>
      <SiteHeader />
      <RewriteWorkspace
        paid={paid}
        planRemaining={usage.remaining}
        quota={usage.quota}
        quotaSources={[]}
        remaining={usage.remaining}
        subscriptionStatus={account.subscriptionStatus}
        usageLabel={usageLabel}
      />
    </>
  );
}
