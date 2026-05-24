import { CreditCard, RefreshCcw, ShieldCheck } from "lucide-react";

import { azureApiFetch } from "../../lib/client-azure-api";
import { Button } from "../ui/button";

type Props = {
  status: string;
  usageLabel: string;
  paid: boolean;
};

async function openBillingPortal() {
  const response = await azureApiFetch("/api/stripe/portal", { method: "POST" });
  const payload = (await response.json()) as { url?: string; error?: string };

  if (!response.ok || !payload.url) {
    throw new Error(payload.error ?? "Could not open billing.");
  }

  window.location.href = payload.url;
}

async function openCheckout() {
  const response = await azureApiFetch("/api/stripe/checkout", { method: "POST" });
  const payload = (await response.json()) as { url?: string; error?: string };

  if (!response.ok || !payload.url) {
    throw new Error(payload.error ?? "Could not open checkout.");
  }

  window.location.href = payload.url;
}

export function SubscriptionStatus({ status, usageLabel, paid }: Props) {
  return (
    <section className="rounded-lg border border-line bg-sky/80 p-4 shadow-crisp">
      <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_auto] md:items-center">
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="min-w-0">
            <p className="text-xs font-semibold uppercase tracking-[0.14em] text-ink/45">
              Account status
            </p>
            <p className="mt-1 text-sm font-semibold text-ink">
              {paid ? `Subscription: ${status}` : "Free workspace"}
            </p>
          </div>
          <div className="min-w-0">
            <p className="text-xs font-semibold uppercase tracking-[0.14em] text-ink/45">
              Usage remaining
            </p>
            <p className="mt-1 text-sm font-semibold text-sage">{usageLabel}</p>
          </div>
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
      <p className="mt-3 flex items-start gap-1.5 text-xs leading-5 text-ink/45">
        <ShieldCheck className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
        Operated by TimeAwake Ltd. Billing is handled by Stripe.
      </p>
    </section>
  );
}
