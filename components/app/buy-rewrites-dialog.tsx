"use client";

import { Sparkles, X } from "lucide-react";
import { useEffect } from "react";

import { BuyButton } from "../landing/buy-button";
import { Card } from "../ui/card";

type WorkspacePack = {
  sku: string;
  name: string;
  price: string;
  allowance: string;
  term: string;
  unitPrice: string;
  description: string;
  cta: string;
  badge?: string;
  highlight?: boolean;
};

// The three packs offered from the workspace, mirroring /pricing. Kept local
// (not shared with the pricing page) so the pricing-page source-string contract
// tests stay stable. The real source of truth for what each sku charges is
// Stripe plus the backend price env vars — this is display copy only.
const workspacePacks: WorkspacePack[] = [
  {
    sku: "quick_pack",
    name: "Quick Pack",
    price: "NZ$2.50",
    allowance: "10 rewrites",
    term: "Valid 90 days",
    unitPrice: "≈ NZ$0.25 / rewrite",
    description: "The lowest-cost way to start. No subscription, no auto-renew.",
    cta: "Get Quick Pack",
  },
  {
    sku: "value_pack",
    name: "Value Pack",
    price: "NZ$6.90",
    allowance: "30 rewrites",
    term: "Valid 90 days",
    unitPrice: "≈ NZ$0.23 / rewrite",
    description: "Best price per rewrite — most people start here.",
    cta: "Get Value Pack",
    badge: "Most popular",
    highlight: true,
  },
  {
    sku: "pro_api",
    name: "Pro/API",
    price: "NZ$19.90/mo",
    allowance: "90 rewrites/mo",
    term: "Monthly subscription",
    unitPrice: "≈ NZ$0.22 / rewrite",
    description: "For heavy use and developers. Includes API access.",
    cta: "Go Pro/API",
  },
];

type BuyRewritesDialogProps = {
  open?: boolean;
  onClose?: () => void;
};

export function BuyRewritesDialog({
  open = false,
  onClose,
}: BuyRewritesDialogProps) {
  useEffect(() => {
    if (!open || !onClose) {
      return;
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        onClose?.();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open, onClose]);

  if (!open) {
    return null;
  }

  return (
    <div
      aria-labelledby="buy-rewrites-title"
      aria-modal="true"
      className="fixed inset-0 z-50 grid place-items-center overflow-y-auto bg-ink/35 px-4 py-6 text-ink"
      role="dialog"
    >
      <div className="w-full max-w-4xl">
        <Card className="p-5 md:p-7">
          <div className="mb-5 flex items-start justify-between gap-4">
            <div>
              <div className="flex h-12 w-12 items-center justify-center rounded-md bg-mint text-sage">
                <Sparkles className="h-5 w-5" aria-hidden="true" />
              </div>
              <p className="mt-4 text-xs font-semibold uppercase tracking-[0.16em] text-sage">
                Rewrite packs &amp; Pro/API
              </p>
              <h2
                className="mt-2 text-2xl font-semibold md:text-3xl"
                id="buy-rewrites-title"
              >
                Choose a pack
              </h2>
              <p className="mt-2 max-w-2xl leading-7 text-ink/65">
                Pick the size that fits. Packs are one-time and valid 90 days;
                Pro/API is billed monthly through Stripe.
              </p>
            </div>
            {onClose ? (
              <button
                aria-label="Close"
                className="rounded-md p-1.5 text-ink/45 transition hover:bg-paper hover:text-ink"
                onClick={onClose}
                type="button"
              >
                <X className="h-4 w-4" aria-hidden="true" />
              </button>
            ) : null}
          </div>

          <div className="grid gap-3 md:grid-cols-3">
            {workspacePacks.map((pack) => (
              <article
                className={
                  "flex flex-col rounded-lg border p-4 " +
                  (pack.highlight
                    ? "border-sage bg-mint/25 shadow-sm ring-2 ring-sage/25"
                    : "border-line bg-paper/40")
                }
                key={pack.sku}
              >
                <div className="flex items-center justify-between gap-2">
                  <p className="font-semibold">{pack.name}</p>
                  {pack.badge ? (
                    <span className="rounded-full bg-sage px-2 py-0.5 text-[11px] font-semibold uppercase text-white">
                      {pack.badge}
                    </span>
                  ) : null}
                </div>
                <p className="mt-2 text-2xl font-semibold">{pack.price}</p>
                <p className="mt-1 text-sm text-ink/55">
                  {pack.allowance} · {pack.term}
                </p>
                <p className="mt-1 font-mono text-[11px] text-ink/50">
                  {pack.unitPrice}
                </p>
                <p className="mt-3 flex-1 text-sm leading-6 text-ink/65">
                  {pack.description}
                </p>
                <div className="mt-4">
                  <BuyButton label={pack.cta} sku={pack.sku} />
                </div>
              </article>
            ))}
          </div>

          <p className="mt-5 text-xs leading-5 text-ink/45">
            Payments are managed by Stripe. Monthly Pro/API rewrites reset each
            period and do not roll over.
          </p>
        </Card>
      </div>
    </div>
  );
}
