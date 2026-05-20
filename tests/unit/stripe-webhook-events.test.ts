import { describe, expect, it } from "vitest";

import {
  buildStripeEventUpdate,
  shouldProcessStripeEvent,
} from "../../lib/stripe-events";

describe("Stripe webhook event lifecycle", () => {
  it("processes new and previously failed events", () => {
    expect(shouldProcessStripeEvent(null)).toBe(true);
    expect(shouldProcessStripeEvent({ status: "failed" })).toBe(true);
  });

  it("skips already processed or currently processing events", () => {
    expect(shouldProcessStripeEvent({ status: "processed" })).toBe(false);
    expect(shouldProcessStripeEvent({ status: "processing" })).toBe(false);
  });

  it("builds retry-safe processed and failed status updates", () => {
    expect(
      buildStripeEventUpdate({
        status: "processed",
        now: new Date("2026-05-20T01:00:00.000Z"),
      }),
    ).toMatchObject({
      status: "processed",
      lastError: null,
      processedAt: new Date("2026-05-20T01:00:00.000Z"),
    });

    expect(
      buildStripeEventUpdate({
        status: "failed",
        error: new Error("subscription sync failed"),
        now: new Date("2026-05-20T01:00:00.000Z"),
      }),
    ).toMatchObject({
      status: "failed",
      failedAt: new Date("2026-05-20T01:00:00.000Z"),
      lastError: "subscription sync failed",
    });
  });
});
