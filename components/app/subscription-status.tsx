"use client";

import { CreditCard, RefreshCcw, Ticket } from "lucide-react";
import { useState } from "react";

import { azureApiFetch } from "../../lib/client-azure-api";
import { Button } from "../ui/button";

type Props = {
  status: string;
  usageLabel: string;
  paid: boolean;
  canRedeem: boolean;
  onRedeemClick: () => void;
};

const workspaceCheckoutSku = "value_pack";

export async function openBillingPortal() {
  const response = await azureApiFetch("/api/stripe/portal", { method: "POST" });
  const payload = (await response.json()) as { url?: string; error?: string };

  if (!response.ok || !payload.url) {
    throw new Error(payload.error ?? "Could not open billing.");
  }

  window.location.href = payload.url;
}

async function openCheckout() {
  const response = await fetch("/api/stripe/checkout", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ sku: workspaceCheckoutSku }),
  });

  if (response.status === 401) {
    window.location.assign(`/sign-in?redirectTo=${encodeURIComponent("/app")}`);
    return;
  }

  const payload = (await response.json().catch(() => ({}))) as {
    url?: string;
    error?: string;
  };

  if (!response.ok || !payload.url) {
    throw new Error(payload.error ?? "Could not start checkout.");
  }

  window.location.assign(payload.url);
}

export function SubscriptionStatus({
  status,
  usageLabel,
  paid,
  canRedeem,
  onRedeemClick,
}: Props) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function handleBillingAction() {
    setLoading(true);
    setError("");

    try {
      if (paid) {
        await openBillingPortal();
      } else {
        await openCheckout();
      }
    } catch (billingError) {
      setError(
        billingError instanceof Error
          ? billingError.message
          : "Could not start checkout.",
      );
      setLoading(false);
    }
  }

  return (
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
          disabled={loading}
          onClick={handleBillingAction}
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
  );
}
