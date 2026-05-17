import { CreditCard, RefreshCcw } from "lucide-react";

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

export function SubscriptionStatus({ status, usageLabel, paid }: Props) {
  return (
    <div className="rounded-lg border border-line bg-white/75 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-sm font-semibold">Account</p>
          <p className="mt-1 text-sm text-ink/60">
            {paid ? `Subscription: ${status}` : "Free workspace"}
          </p>
          <p className="mt-1 text-sm font-medium text-sage">{usageLabel}</p>
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
          <div className="flex items-center gap-2 rounded-md bg-paper px-3 py-2 text-xs font-medium text-ink/55">
            <RefreshCcw className="h-4 w-4 text-clay" aria-hidden="true" />
            Upgrade anytime
          </div>
        )}
      </div>
    </div>
  );
}
