"use client";

import { ArrowRight, CheckCircle2, XCircle } from "lucide-react";
import { useEffect } from "react";

import { LinkButton } from "../ui/button";

export type CheckoutStatus = "success" | "cancelled";

type Props = {
  status: CheckoutStatus | null;
  usageLabel: string;
};

function stripCheckoutParam() {
  const params = new URLSearchParams(window.location.search);
  if (!params.has("checkout")) {
    return;
  }

  params.delete("checkout");
  const nextSearch = params.toString();
  const nextPath = `${window.location.pathname}${nextSearch ? `?${nextSearch}` : ""}${window.location.hash}`;
  window.history.replaceState(window.history.state, "", nextPath);
}

export function CheckoutBanner({ status, usageLabel }: Props) {
  useEffect(() => {
    if (!status) {
      return;
    }

    stripCheckoutParam();
  }, [status]);

  if (!status) {
    return null;
  }

  const confirmed = status === "success";

  return (
    <section
      aria-live="polite"
      className={`rounded-lg border p-4 text-ink shadow-soft sm:p-5 ${
        confirmed
          ? "border-sage/30 bg-mint/45"
          : "border-clay/25 bg-clay/10"
      }`}
      role="status"
    >
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex min-w-0 gap-3">
          <div
            className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-md border bg-white/70 ${
              confirmed
                ? "border-sage/20 text-sage"
                : "border-clay/20 text-clay"
            }`}
          >
            {confirmed ? (
              <CheckCircle2 className="h-5 w-5" aria-hidden="true" />
            ) : (
              <XCircle className="h-5 w-5" aria-hidden="true" />
            )}
          </div>
          <div className="min-w-0">
            <p
              className={`font-semibold ${
                confirmed ? "text-sage" : "text-clay"
              }`}
            >
              {confirmed ? "Purchase confirmed" : "Checkout cancelled"}
            </p>
            <p className="mt-1 text-sm leading-relaxed text-ink/70">
              {confirmed
                ? `Your rewrite balance has been updated: ${usageLabel}.`
                : "No charge was made. You can return to pricing when you are ready."}
            </p>
          </div>
        </div>
        {confirmed ? null : (
          <LinkButton className="w-full sm:w-auto" href="/pricing" variant="secondary">
            Back to pricing
            <ArrowRight className="h-4 w-4" aria-hidden="true" />
          </LinkButton>
        )}
      </div>
    </section>
  );
}
