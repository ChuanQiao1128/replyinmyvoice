"use client";

import { useEffect, useRef, useState } from "react";

import {
  safeAuthRedirectIntent,
  safeAuthRedirectSku,
  type AuthRedirectSku,
} from "../../lib/auth-redirect-intent";

type PricingCheckoutResumeIntent = {
  sku: AuthRedirectSku;
};

type PricingCheckoutResumeStatus = "continuing" | "failed";

export function readPricingCheckoutResumeIntent(
  search: string,
): PricingCheckoutResumeIntent | null {
  const params = new URLSearchParams(search);
  const intent = safeAuthRedirectIntent(params.get("intent"));
  const sku = safeAuthRedirectSku(params.get("sku"));

  if (intent !== "buy" || !sku) {
    return null;
  }

  return { sku };
}

export function buildPricingPathWithoutCheckoutResume(
  location: Pick<Location, "hash" | "pathname" | "search">,
) {
  const params = new URLSearchParams(location.search);
  params.delete("intent");
  params.delete("sku");

  const search = params.toString();
  return `${location.pathname}${search ? `?${search}` : ""}${location.hash}`;
}

function clearPricingCheckoutResumeParams() {
  if (typeof window === "undefined") {
    return;
  }

  window.history.replaceState(
    window.history.state,
    "",
    buildPricingPathWithoutCheckoutResume(window.location),
  );
}

export function PricingCheckoutResume() {
  const startedRef = useRef(false);
  const abortRef = useRef<AbortController | null>(null);
  const [resumeIntent, setResumeIntent] =
    useState<PricingCheckoutResumeIntent | null>(null);
  const [status, setStatus] =
    useState<PricingCheckoutResumeStatus>("continuing");
  const [error, setError] = useState("");

  useEffect(() => {
    if (resumeIntent || startedRef.current || typeof window === "undefined") {
      return;
    }

    setResumeIntent(readPricingCheckoutResumeIntent(window.location.search));
  }, [resumeIntent]);

  useEffect(() => {
    if (!resumeIntent || startedRef.current || typeof window === "undefined") {
      return;
    }

    startedRef.current = true;
    const controller = new AbortController();
    abortRef.current = controller;
    const sku = resumeIntent.sku;
    clearPricingCheckoutResumeParams();

    async function resumeCheckout() {
      try {
        const response = await fetch("/api/stripe/checkout", {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({ sku }),
          signal: controller.signal,
        });
        const payload = (await response.json().catch(() => ({}))) as {
          error?: string;
          url?: string;
        };

        if (!response.ok || !payload.url) {
          throw new Error(payload.error ?? "Could not continue checkout.");
        }

        window.location.assign(payload.url);
      } catch (checkoutError) {
        if (controller.signal.aborted) {
          return;
        }

        setStatus("failed");
        setError(
          checkoutError instanceof Error
            ? checkoutError.message
            : "Could not continue checkout.",
        );
      }
    }

    void resumeCheckout();

    return () => {
      controller.abort();
    };
  }, [resumeIntent]);

  if (!resumeIntent) {
    return null;
  }

  function cancelResume() {
    abortRef.current?.abort();
    clearPricingCheckoutResumeParams();
    setResumeIntent(null);
  }

  return (
    <div className="pricing-resume-banner" aria-live="polite">
      <div>
        <p className="pricing-resume-title" role="status">
          {status === "continuing"
            ? "Continuing your purchase\u2026"
            : "Checkout did not continue."}
        </p>
        <p className="pricing-resume-detail">
          {status === "continuing"
            ? "We are reopening the Stripe checkout for the pack you selected."
            : error || "Use a pack button below to start checkout again."}
        </p>
      </div>
      <button
        className="btn btn-ghost pricing-resume-cancel"
        onClick={cancelResume}
        type="button"
      >
        Cancel
      </button>
    </div>
  );
}
