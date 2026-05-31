"use client";

import type {
  PaymentEventProperties,
  PaymentFunnelEvent,
} from "./payment-observability";

const storageKey = "rimv_payment_observability_id";

export function initializePaymentBrowserObservability() {
  return {
    distinctId: getDistinctId(),
    initialized: typeof window !== "undefined",
  };
}

export function capturePaymentBrowserEvent(
  event: PaymentFunnelEvent,
  properties: PaymentEventProperties = {},
) {
  if (typeof window === "undefined") {
    return;
  }

  const payload = JSON.stringify({
    event,
    properties: {
      ...properties,
      distinctId: getDistinctId(),
      source: properties.source ?? "browser",
    },
  });

  try {
    if (navigator.sendBeacon) {
      const queued = navigator.sendBeacon(
        "/api/observability/payment",
        new Blob([payload], { type: "application/json" }),
      );
      if (queued) {
        return;
      }
    }

    void fetch("/api/observability/payment", {
      body: payload,
      headers: {
        "Content-Type": "application/json",
      },
      keepalive: true,
      method: "POST",
    }).catch(() => undefined);
  } catch {
    // Telemetry must never block checkout.
  }
}

function getDistinctId() {
  try {
    const existing = window.localStorage.getItem(storageKey);
    if (existing) {
      return existing;
    }

    const created = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;
    window.localStorage.setItem(storageKey, created);
    return created;
  } catch {
    return "browser-anonymous";
  }
}
