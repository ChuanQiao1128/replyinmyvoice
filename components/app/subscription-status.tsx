"use client";

import { CreditCard, RefreshCcw, Ticket } from "lucide-react";
import { useState } from "react";

import { openBillingPortal } from "../../lib/billing-portal";
import { Button } from "../ui/button";
import { BuyRewritesDialog } from "./buy-rewrites-dialog";
import { CheckoutBanner, type CheckoutStatus } from "./checkout-banner";
import { PastDueBanner } from "./past-due-banner";

type Props = {
  status: string;
  usageLabel: string;
  paid: boolean;
  paymentGraceEndsAt: string | null;
  canRedeem: boolean;
  checkoutStatus?: CheckoutStatus | null;
  onRedeemClick: () => void;
};

export function SubscriptionStatus({
  status,
  usageLabel,
  paid,
  paymentGraceEndsAt,
  canRedeem,
  checkoutStatus = null,
  onRedeemClick,
}: Props) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [buyOpen, setBuyOpen] = useState(false);

  async function handleManageBilling() {
    setLoading(true);
    setError("");

    try {
      await openBillingPortal();
    } catch (billingError) {
      setError(
        billingError instanceof Error
          ? billingError.message
          : "Could not open billing.",
      );
      setLoading(false);
    }
  }

  return (
    <div className="space-y-3">
      <CheckoutBanner status={checkoutStatus} usageLabel={usageLabel} />
      {status === "PastDue" ? (
        <PastDueBanner paymentGraceEndsAt={paymentGraceEndsAt} />
      ) : null}
      <section className="flex flex-col gap-4 rounded-2xl border border-line bg-sky/50 p-5 md:flex-row md:items-center md:justify-between md:p-6">
        <div className="min-w-0 text-[15px] leading-relaxed">
          <p className="font-mono text-[11px] font-semibold uppercase tracking-[0.18em] text-ink/50">
            {paid ? "Subscription" : "Free workspace"}
          </p>
          <p className="mt-2 flex flex-col gap-1 text-ink/70 sm:flex-row sm:flex-wrap sm:items-center sm:gap-2">
            {paid ? <span className="font-semibold text-ink">{status}</span> : null}
            {paid ? (
              <span aria-hidden="true" className="hidden text-ink/25 sm:inline">
                ·
              </span>
            ) : null}
            <span className="font-semibold text-sage">{usageLabel}</span>
          </p>
        </div>
        <div className="flex w-full flex-col gap-3 sm:flex-row md:w-auto">
          {canRedeem ? (
            <Button
              className="w-full md:w-auto"
              onClick={onRedeemClick}
              type="button"
              variant="secondary"
            >
              <Ticket className="h-4 w-4" aria-hidden="true" />
              Redeem code
            </Button>
          ) : null}
          <Button
            className="w-full md:w-auto"
            disabled={paid && loading}
            onClick={paid ? handleManageBilling : () => setBuyOpen(true)}
            type="button"
            variant={paid ? "secondary" : "primary"}
          >
            {paid ? (
              <CreditCard className="h-4 w-4" aria-hidden="true" />
            ) : (
              <RefreshCcw className="h-4 w-4" aria-hidden="true" />
            )}
            {paid ? "Manage billing" : "Buy rewrites"}
          </Button>
        </div>
        {error ? (
          <p className="text-sm font-medium text-sage md:basis-full" role="alert">
            {error}
          </p>
        ) : null}
      </section>
      <BuyRewritesDialog open={buyOpen} onClose={() => setBuyOpen(false)} />
    </div>
  );
}
