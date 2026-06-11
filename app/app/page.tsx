import { redirect } from "next/navigation";

import type { CheckoutStatus } from "../../components/app/checkout-banner";
import { RewriteWorkspace } from "../../components/app/rewrite-workspace";
import { fetchAzureAccountSummary } from "../../lib/azure-api";
import {
  labelForQuotaSource,
  selectAppExperience,
  trialCreditSummary,
  trialExpiryLabel,
} from "../../lib/promo-app-state";

export const dynamic = "force-dynamic";

const paidStatuses = new Set(["active", "trialing", "testing"]);

type AppSearchParams = Record<string, string | string[] | undefined>;

type AppPageProps = {
  searchParams?: Promise<AppSearchParams>;
};

function checkoutStatusFromParam(
  value: string | string[] | undefined,
): CheckoutStatus | null {
  const checkout = Array.isArray(value) ? value[0] : value;
  return checkout === "success" || checkout === "cancelled" ? checkout : null;
}

export default async function AppPage({ searchParams }: AppPageProps = {}) {
  const params = await searchParams;
  const checkoutStatus = checkoutStatusFromParam(params?.checkout);
  const account = await fetchAzureAccountSummary();

  if (!account) {
    redirect("/sign-in");
  }

  const usage = account.usage;
  const rewriteHistoryUserKey = account.externalAuthUserId || account.userId;
  const pastDue = account.subscriptionStatus === "PastDue";
  const paid = usage.scope === "paid" || paidStatuses.has(account.subscriptionStatus) || pastDue;
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
    <RewriteWorkspace
        appExperience={appExperience}
        canRedeem={canRedeem}
        checkoutStatus={checkoutStatus}
        outOfCredits={outOfCredits}
        paid={paid}
        planRemaining={workspacePlanRemaining}
        promoState={promoState}
        quota={workspaceQuota}
        quotaSources={quotaSources}
        remaining={usage.remaining}
        rewriteHistoryUserKey={rewriteHistoryUserKey}
        usageExhausted={usage.exhausted}
        paymentGraceEndsAt={account.paymentGraceEndsAt}
        subscriptionStatus={account.subscriptionStatus}
        usageLabel={usageLabel}
      />
  );
}
