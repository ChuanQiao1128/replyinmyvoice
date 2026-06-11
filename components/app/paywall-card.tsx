"use client";

import { ArrowRight, CreditCard } from "lucide-react";
import { useState } from "react";

import { azureApiFetch } from "../../lib/client-azure-api";
import { failureCopy } from "../../lib/failure-copy";
import { Button, LinkButton } from "../ui/button";
import { Card } from "../ui/card";

type Props = {
  title: string;
  description: string;
  status: string;
  action?: "checkout" | "portal";
};

export function PaywallCard({
  title,
  description,
  status,
  action = "checkout",
}: Props) {
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function startCheckout() {
    setLoading(true);
    setError("");

    try {
      const response = await azureApiFetch(
        action === "checkout" ? "/api/stripe/checkout" : "/api/stripe/portal",
        { method: "POST" },
      );
      const payload = (await response.json()) as { url?: string; error?: string };

      if (!response.ok || !payload.url) {
        throw new Error(payload.error ?? failureCopy.checkout.start);
      }

      window.location.href = payload.url;
    } catch (checkoutError) {
      setError(
        checkoutError instanceof Error
          ? checkoutError.message
          : failureCopy.checkout.start,
      );
      setLoading(false);
    }
  }

  return (
    <main className="min-h-screen bg-paper px-4 py-8 text-ink md:px-6 md:py-10">
      <section className="mx-auto grid max-w-6xl gap-5 lg:grid-cols-[minmax(0,1fr)_360px] lg:items-start">
        <Card className="p-5 md:p-7">
          <div className="mb-5 flex h-12 w-12 items-center justify-center rounded-md bg-paper-deep text-clay">
            <CreditCard className="h-5 w-5" aria-hidden="true" />
          </div>
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-clay">
            {status}
          </p>
          <h1 className="mt-3 text-2xl font-semibold md:text-3xl">{title}</h1>
          <p className="mt-3 max-w-2xl leading-7 text-ink/65">{description}</p>
          <p className="mt-5 text-sm text-ink/55">
            The workspace stays available as soon as billing or quota is current.
          </p>
        </Card>
        <aside className="rounded-lg border border-line bg-white/80 p-5 shadow-crisp lg:sticky lg:top-20">
          <p className="text-sm font-medium text-ink/60">Reply In My Voice</p>
          <p className="mt-2 text-3xl font-semibold">Value Pack</p>
          <p className="mt-1 text-xl font-semibold">NZ$6.90</p>
          <p className="mt-2 text-sm text-ink/60">
            30 rewrites, valid 90 days. Best price per rewrite.
          </p>
          <p className="mt-2 text-sm text-ink/60">
            Quick Pack is NZ$2.50 for 10 rewrites to start.
          </p>
          <p className="mt-2 text-sm text-ink/60">
            Pro/API is NZ$19.90/month for 90 rewrites and API access.
          </p>
          <p className="mt-2 text-xs leading-5 text-ink/45">
            Operated by TimeAwake Ltd. Payments are managed by Stripe.
          </p>
          {action === "portal" ? (
            <Button
              className="mt-6 w-full"
              disabled={loading}
              onClick={startCheckout}
              type="button"
              variant="clay"
            >
              Manage billing
              <ArrowRight className="h-4 w-4" aria-hidden="true" />
            </Button>
          ) : (
            <LinkButton className="mt-6 w-full" href="/pricing" variant="clay">
              See plans and buy rewrites
              <ArrowRight className="h-4 w-4" aria-hidden="true" />
            </LinkButton>
          )}
          {error ? <p className="mt-3 text-sm text-red-700">{error}</p> : null}
        </aside>
      </section>
    </main>
  );
}
