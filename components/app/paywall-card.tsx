"use client";

import { ArrowRight, CreditCard } from "lucide-react";
import { useState } from "react";

import { Button } from "../ui/button";
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
      const response = await fetch(
        action === "checkout" ? "/api/stripe/checkout" : "/api/stripe/portal",
        { method: "POST" },
      );
      const payload = (await response.json()) as { url?: string; error?: string };

      if (!response.ok || !payload.url) {
        throw new Error(payload.error ?? "Could not start checkout.");
      }

      window.location.href = payload.url;
    } catch (checkoutError) {
      setError(
        checkoutError instanceof Error
          ? checkoutError.message
          : "Could not start checkout.",
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
          <p className="mt-2 text-3xl font-semibold">NZD $9/month</p>
          <p className="mt-2 text-sm text-ink/60">
            40 rewrites per billing month. Cancel anytime.
          </p>
          <p className="mt-2 text-xs leading-5 text-ink/45">
            Operated by TimeAwake Ltd. Payments are managed by Stripe.
          </p>
          <Button
            className="mt-6 w-full"
            disabled={loading}
            onClick={startCheckout}
            type="button"
            variant="clay"
          >
            {action === "checkout" ? "Subscribe and continue" : "Manage billing"}
            <ArrowRight className="h-4 w-4" aria-hidden="true" />
          </Button>
          {error ? <p className="mt-3 text-sm text-red-700">{error}</p> : null}
        </aside>
      </section>
    </main>
  );
}
