"use client";

import { AlertTriangle, CreditCard, Loader2 } from "lucide-react";
import { useState } from "react";

import { openBillingPortal } from "../../lib/billing-portal";
import { Button } from "../ui/button";

type Props = {
  paymentGraceEndsAt: string | null;
};

function formatGraceEnd(value: string | null) {
  if (!value) {
    return "the grace deadline";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "the grace deadline";
  }

  return new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(date);
}

export function PastDueBanner({ paymentGraceEndsAt }: Props) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const graceEnd = formatGraceEnd(paymentGraceEndsAt);

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
    <section
      className="rounded-lg border border-rust/30 bg-rust/10 p-4 text-ink shadow-soft sm:p-5"
      role="status"
    >
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex min-w-0 gap-3">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md border border-rust/20 bg-white/70 text-rust">
            <AlertTriangle className="h-5 w-5" aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <p className="font-semibold text-rust">Payment failed</p>
            <p className="mt-1 text-sm leading-relaxed text-ink/70">
              Payment failed - update your payment method by {graceEnd} to keep your plan.
            </p>
            {error ? (
              <p className="mt-2 text-sm font-medium text-rust" role="alert">
                {error}
              </p>
            ) : null}
          </div>
        </div>
        <Button
          className="w-full sm:w-auto"
          disabled={loading}
          onClick={handleManageBilling}
          type="button"
          variant="secondary"
        >
          {loading ? (
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
          ) : (
            <CreditCard className="h-4 w-4" aria-hidden="true" />
          )}
          Manage billing
        </Button>
      </div>
    </section>
  );
}
