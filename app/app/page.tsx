import { redirect } from "next/navigation";

import { PaywallCard } from "../../components/app/paywall-card";
import { RewriteWorkspace } from "../../components/app/rewrite-workspace";
import { SiteHeader } from "../../components/site-header";
import { shouldShowAdminEntry } from "../../lib/admin-visible";
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
  const showAdmin = shouldShowAdminEntry({
    userId: account.externalAuthUserId,
    email: account.email,
  });

  if (usage.exhausted && !paid) {
    return (
      <>
        <SiteHeader showAdmin={showAdmin} />
        <PaywallCard
          description="Your 3 free rewrites have been used. Upgrade to Starter for 55 monthly rewrites, choose Pro/API for heavier use, or use a one-time option when you only need a short burst."
          status="Free quota used"
          title="Keep writing in your own voice."
        />
      </>
    );
  }

  if (usage.exhausted && paid) {
    return (
      <>
        <SiteHeader showAdmin={showAdmin} />
        <PaywallCard
          action="portal"
          description="Your monthly rewrite quota has been used for this billing period. Top-up appears when quota runs low, or you can manage billing and come back when the next period starts."
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
      <SiteHeader showAdmin={showAdmin} />
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
