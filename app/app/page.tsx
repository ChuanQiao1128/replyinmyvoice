import { redirect } from "next/navigation";

import { RewriteWorkspace } from "../../components/app/rewrite-workspace";
import { SiteHeader } from "../../components/site-header";
import { fetchAzureAccountSummary } from "../../lib/azure-api";
import {
  labelForQuotaSource,
  selectAppExperience,
  trialCreditSummary,
  trialExpiryLabel,
} from "../../lib/promo-app-state";

export const dynamic = "force-dynamic";

const paidStatuses = new Set(["active", "trialing", "testing"]);

export default async function AppPage() {
  const account = await fetchAzureAccountSummary();

  if (!account) {
    redirect("/sign-in");
  }

  const usage = account.usage;
  const paid = usage.scope === "paid" || paidStatuses.has(account.subscriptionStatus);
  const promo = account.promo ?? null;
  const quotaSources = (usage.sources ?? []).map((source) => ({
    ...source,
    label: labelForQuotaSource(source.source, source.label),
  }));
  const trialCredits = trialCreditSummary(
    quotaSources,
    Math.max(promo?.trialRemaining ?? 0, 0),
  );
  const promoState = {
    hasRedeemed: Boolean(promo?.hasRedeemed),
    trialExpiresAt: promo?.trialExpiresAt ?? null,
    trialRemaining: trialCredits.remaining,
  };
  const appExperience = selectAppExperience({
    paid,
    promo: promoState,
    usageExhausted: usage.exhausted,
    usageRemaining: usage.remaining,
  });
  const trialExpiry = trialExpiryLabel(promo?.trialExpiresAt ?? null);
  const hasTrialCredits = !paid && promo?.hasRedeemed && trialCredits.remaining > 0;
  const usageLabel = paid
    ? `${usage.remaining} of ${usage.quota} rewrites remaining this billing period`
    : hasTrialCredits
      ? `${trialCredits.remaining} of ${trialCredits.granted} trial rewrites remaining${trialExpiry ? ` - ${trialExpiry}` : ""}`
    : `${usage.remaining} of ${usage.quota} rewrite credits remaining`;
  const workspaceQuota = hasTrialCredits ? trialCredits.granted : usage.quota;
  const workspacePlanRemaining = hasTrialCredits
    ? trialCredits.remaining
    : usage.remaining;
  const outOfCredits = usage.remaining <= 0 || usage.exhausted;
  const canRedeem =
    !paid && (!promoState.hasRedeemed || promoState.trialRemaining === 0);

  return (
    <>
      <SiteHeader />
      <RewriteWorkspace
        appExperience={appExperience}
        canRedeem={canRedeem}
        outOfCredits={outOfCredits}
        paid={paid}
        planRemaining={workspacePlanRemaining}
        promoState={promoState}
        quota={workspaceQuota}
        quotaSources={quotaSources}
        remaining={usage.remaining}
        usageExhausted={usage.exhausted}
        subscriptionStatus={account.subscriptionStatus}
        usageLabel={usageLabel}
      />
    </>
  );
}
