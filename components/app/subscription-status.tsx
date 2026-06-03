import { CreditCard, RefreshCcw, Ticket } from "lucide-react";

import { azureApiFetch } from "../../lib/client-azure-api";

type Props = {
  status: string;
  usageLabel: string;
  paid: boolean;
  canRedeem: boolean;
  onRedeemClick: () => void;
};

export async function openBillingPortal() {
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

export function SubscriptionStatus({
  status,
  usageLabel,
  paid,
  canRedeem,
  onRedeemClick,
}: Props) {
  return (
    <section className="flex flex-wrap items-center justify-between gap-x-5 gap-y-2 rounded-xl border border-line bg-sky/60 px-4 py-2.5">
      <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1 text-sm">
        <span className="font-mono text-[10px] font-semibold uppercase tracking-[0.18em] text-ink/40">
          {paid ? "Subscription" : "Free workspace"}
        </span>
        {paid ? <span className="font-medium text-ink">{status}</span> : null}
        <span aria-hidden="true" className="text-ink/25">
          ·
        </span>
        <span className="font-semibold text-sage">{usageLabel}</span>
      </div>
      <div className="flex flex-wrap items-center gap-3">
        {canRedeem ? (
          <button
            className="inline-flex items-center gap-1.5 text-sm font-semibold text-clay underline-offset-4 transition hover:text-clay/80 hover:underline"
            onClick={onRedeemClick}
            type="button"
          >
            <Ticket className="h-4 w-4" aria-hidden="true" />
            Redeem code
          </button>
        ) : null}
        <button
          className="inline-flex items-center gap-1.5 text-sm font-semibold text-ink/65 underline-offset-4 transition hover:text-ink hover:underline"
          onClick={() => void (paid ? openBillingPortal() : openCheckout())}
          type="button"
        >
          {paid ? (
            <CreditCard className="h-4 w-4" aria-hidden="true" />
          ) : (
            <RefreshCcw className="h-4 w-4" aria-hidden="true" />
          )}
          {paid ? "Manage billing" : "Buy rewrites"}
        </button>
      </div>
    </section>
  );
}
