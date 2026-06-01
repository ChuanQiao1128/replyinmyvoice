import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  capturePaymentError,
  capturePaymentEvent,
  initializePaymentObservability,
} from "../../lib/payment-observability";

const originalSentryDsn = process.env.SENTRY_DSN;
const originalPostHogApiKey = process.env.POSTHOG_API_KEY;

beforeEach(() => {
  delete process.env.SENTRY_DSN;
  delete process.env.POSTHOG_API_KEY;
  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  if (originalSentryDsn === undefined) {
    delete process.env.SENTRY_DSN;
  } else {
    process.env.SENTRY_DSN = originalSentryDsn;
  }

  if (originalPostHogApiKey === undefined) {
    delete process.env.POSTHOG_API_KEY;
  } else {
    process.env.POSTHOG_API_KEY = originalPostHogApiKey;
  }

  vi.unstubAllGlobals();
});

describe("payment observability", () => {
  it("keeps missing telemetry keys as a graceful no-op", async () => {
    expect(initializePaymentObservability()).toEqual({
      postHogEnabled: false,
      sentryEnabled: false,
    });

    await expect(
      capturePaymentEvent("payment_failed", {
        correlationId: "corr-missing-keys",
        source: "unit-test",
      }),
    ).resolves.toEqual({
      delivered: false,
      reason: "missing_posthog_key",
    });

    await expect(
      capturePaymentError(new Error("checkout failed"), {
        correlationId: "corr-missing-keys",
        event: "payment_failed",
        source: "unit-test",
      }),
    ).resolves.toEqual({
      delivered: false,
      reason: "missing_sentry_dsn",
    });

    expect(globalThis.fetch).not.toHaveBeenCalled();
  });
});
