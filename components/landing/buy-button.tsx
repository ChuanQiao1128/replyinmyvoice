"use client";

import { useState } from "react";

import {
  capturePaymentBrowserEvent,
  initializePaymentBrowserObservability,
} from "../../lib/payment-observability-client";
import { buildAuthRedirectSearchParams } from "../../lib/auth-redirect-intent";

type BuyButtonProps = {
  sku: string;
  label: string;
  className?: string;
};

export function BuyButton({
  sku,
  label,
  className = "btn btn-primary btn-lg",
}: BuyButtonProps) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function startCheckout() {
    setLoading(true);
    setError("");
    initializePaymentBrowserObservability();
    capturePaymentBrowserEvent("checkout_started", {
      sku,
      source: "buy_button",
    });

    let failureCaptured = false;
    try {
      const response = await fetch("/api/stripe/checkout", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ sku }),
      });

      if (response.status === 401) {
        const params = buildAuthRedirectSearchParams({
          intent: "buy",
          redirectTo: "/pricing",
          sku,
        });
        window.location.assign(
          `/sign-in?${params.toString()}`,
        );
        return;
      }

      const payload = (await response.json().catch(() => ({}))) as {
        url?: string;
        error?: string;
      };

      if (!response.ok || !payload.url) {
        capturePaymentBrowserEvent("payment_failed", {
          sku,
          source: "buy_button",
          status: response.status,
        });
        failureCaptured = true;
        throw new Error(payload.error ?? "Could not start checkout.");
      }

      window.location.assign(payload.url);
    } catch (checkoutError) {
      if (!failureCaptured) {
        capturePaymentBrowserEvent("payment_failed", {
          sku,
          source: "buy_button",
        });
      }
      setError(
        checkoutError instanceof Error
          ? checkoutError.message
          : "Could not start checkout.",
      );
      setLoading(false);
    }
  }

  return (
    <div style={{ width: "100%" }}>
      <button
        className={className}
        disabled={loading}
        onClick={startCheckout}
        style={{ width: "100%", justifyContent: "center" }}
        type="button"
      >
        {loading ? "Starting..." : label}
        {!loading ? (
          <>
            {" "}
            <span className="btn-arrow">-&gt;</span>
          </>
        ) : null}
      </button>
      {error ? (
        <p style={{ color: "var(--accent)", fontSize: 13, marginTop: 10 }}>
          {error}
        </p>
      ) : null}
    </div>
  );
}
