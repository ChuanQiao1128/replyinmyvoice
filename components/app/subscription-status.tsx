import { CreditCard, RefreshCcw, ShieldCheck } from "lucide-react";

import { Button } from "../ui/button";

type Props = {
  status: string;
  usageLabel: string;
  paid: boolean;
};

async function openBillingPortal() {
  const response = await fetch("/api/stripe/portal", { method: "POST" });
  const payload = (await response.json()) as { url?: string; error?: string };

  if (!response.ok || !payload.url) {
    throw new Error(payload.error ?? "Could not open billing.");
  }

  window.location.href = payload.url;
}

async function openCheckout() {
  const response = await fetch("/api/stripe/checkout", { method: "POST" });
  const payload = (await response.json()) as { url?: string; error?: string };

  if (!response.ok || !payload.url) {
    throw new Error(payload.error ?? "Could not open checkout.");
  }

  window.location.href = payload.url;
}

export function SubscriptionStatus({ status, usageLabel, paid }: Props) {
  return (
    <div className="rounded-lg border border-line bg-white/82 p-4 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm font-semibold">Account</p>
          <p className="mt-1 text-sm text-ink/60">
            {paid ? `Subscription: ${status}` : "Free workspace"}
          </p>
          <p className="mt-1 text-sm font-medium text-evergreen">{usageLabel}</p>
          <p className="mt-1 flex items-center gap-1.5 text-xs text-ink/45">
            <ShieldCheck className="h-3.5 w-3.5" aria-hidden="true" />
            Operated by TimeAwake Ltd. Billing is handled by Stripe.
          </p>
        </div>
        {paid ? (
          <Button
            onClick={() => void openBillingPortal()}
            type="button"
            variant="secondary"
          >
            <CreditCard className="h-4 w-4" aria-hidden="true" />
            Manage billing
          </Button>
        ) : (
          <Button
            onClick={() => void openCheckout()}
            type="button"
            variant="secondary"
          >
            <RefreshCcw className="h-4 w-4" aria-hidden="true" />
            Upgrade
          </Button>
        )}
      </div>
    </div>
  );
}
