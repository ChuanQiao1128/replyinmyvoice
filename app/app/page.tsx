import { redirect } from "next/navigation";

import { PaywallCard } from "../../components/app/paywall-card";
import { RedeemCodeCard } from "../../components/app/redeem-code-card";
import { RewriteWorkspace } from "../../components/app/rewrite-workspace";
import { SiteHeader } from "../../components/site-header";
import { fetchAzureAccountSummary } from "../../lib/azure-api";
import {
  labelForQuotaSource,
  selectAppExperience,
  trialExpiryLabel,
} from "../../lib/promo-app-state";

export const dynamic = "force-dynamic";

const paidStatuses = new Set(["active", "trialing", "testing"]);
const trialQuota = 3;

export default async function AppPage() {
  const account = await fetchAzureAccountSummary();

  if (!account) {
    redirect("/sign-in");
  }

  const usage = account.usage;
  const paid = usage.scope === "paid" || paidStatuses.has(account.subscriptionStatus);
  const promo = account.promo ?? null;
  const experience = selectAppExperience({
    paid,
    promo,
    usageExhausted: usage.exhausted,
    usageRemaining: usage.remaining,
  });
  const quotaSources = (usage.sources ?? []).map((source) => ({
    ...source,
    label: labelForQuotaSource(source.source, source.label),
  }));
  const trialRemaining = Math.max(promo?.trialRemaining ?? 0, 0);
  const trialExpiry = trialExpiryLabel(promo?.trialExpiresAt ?? null);
  const hasTrialCredits = !paid && promo?.hasRedeemed && trialRemaining > 0;

  if (experience === "redeem") {
    return (
      <>
        <SiteHeader />
        <RedeemCodeCard />
      </>
    );
  }

  if (experience === "free-paywall") {
    return (
      <>
        <SiteHeader />
        <PaywallCard
          description="Your trial rewrites have been used. Buy a rewrite pack — Quick Pack for 10 or Value Pack for 30 — or go Pro/API for 90 rewrites a month and API access."
          status="Trial used"
          title="Keep writing in your own voice."
        />
      </>
    );
  }

  if (experience === "paid-paywall") {
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
    : hasTrialCredits
      ? `${trialRemaining} of ${trialQuota} trial rewrites remaining${trialExpiry ? ` - ${trialExpiry}` : ""}`
    : `${usage.remaining} of ${usage.quota} free rewrites remaining`;
  const workspaceQuota = hasTrialCredits ? trialQuota : usage.quota;
  const workspacePlanRemaining = hasTrialCredits ? trialRemaining : usage.remaining;

  return (
    <>
      <SiteHeader />
      <RewriteWorkspace
        paid={paid}
        planRemaining={workspacePlanRemaining}
        quota={workspaceQuota}
        quotaSources={quotaSources}
        remaining={usage.remaining}
        subscriptionStatus={account.subscriptionStatus}
        usageLabel={usageLabel}
      />
    </>
  );
}
