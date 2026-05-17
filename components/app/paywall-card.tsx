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
    <main className="min-h-screen bg-paper px-6 py-12 text-ink">
      <section className="mx-auto max-w-2xl">
        <Card className="p-6 md:p-8">
          <div className="mb-5 flex h-12 w-12 items-center justify-center rounded-md bg-paper-deep text-clay">
            <CreditCard className="h-5 w-5" aria-hidden="true" />
          </div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
            {status}
          </p>
          <h1 className="mt-3 text-3xl font-semibold">{title}</h1>
          <p className="mt-3 leading-7 text-ink/65">{description}</p>
          <div className="mt-6 rounded-lg border border-line bg-white p-5">
            <p className="text-sm font-medium text-ink/60">Reply In My Voice</p>
            <p className="mt-2 text-3xl font-semibold">NZD $9/month</p>
            <p className="mt-2 text-sm text-ink/60">
              100 rewrites per billing month. Cancel anytime.
            </p>
            <p className="mt-2 text-xs text-ink/45">
              Operated by TimeAwake Ltd. Payments are managed by Stripe.
            </p>
          </div>
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
        </Card>
      </section>
    </main>
  );
}
