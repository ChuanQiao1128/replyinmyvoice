import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { cloudflareMock } = vi.hoisted(() => ({
  cloudflareMock: {
    getCloudflareContext: vi.fn(),
  },
}));

vi.mock("@opennextjs/cloudflare", () => cloudflareMock);

import { getCloudflareContext } from "@opennextjs/cloudflare";
import { captureApiEvent, scheduleApiEvent } from "../../lib/api-observability";

const originalPostHogApiKey = process.env.POSTHOG_API_KEY;
const originalPostHogHost = process.env.POSTHOG_HOST;
const originalSentryDsn = process.env.SENTRY_DSN;

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

beforeEach(() => {
  delete process.env.POSTHOG_API_KEY;
  delete process.env.POSTHOG_HOST;
  delete process.env.SENTRY_DSN;
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 200 })));
  vi.mocked(getCloudflareContext).mockReset();
});

afterEach(() => {
  if (originalPostHogApiKey === undefined) {
    delete process.env.POSTHOG_API_KEY;
  } else {
    process.env.POSTHOG_API_KEY = originalPostHogApiKey;
  }

  if (originalPostHogHost === undefined) {
    delete process.env.POSTHOG_HOST;
  } else {
    process.env.POSTHOG_HOST = originalPostHogHost;
  }

  if (originalSentryDsn === undefined) {
    delete process.env.SENTRY_DSN;
  } else {
    process.env.SENTRY_DSN = originalSentryDsn;
  }

  vi.unstubAllGlobals();
});

describe("api observability", () => {
  it("keeps missing server keys as a quiet no-op", async () => {
    await expect(
      captureApiEvent({
        endpoint: "GET /api/v1/usage",
        statusCode: 200,
      }),
    ).resolves.toEqual([]);

    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("captures api error events and server error details", async () => {
    process.env.POSTHOG_API_KEY = "ph_unit_key";
    process.env.POSTHOG_HOST = "https://posthog.example.test";
    process.env.SENTRY_DSN = "https://public@example.sentry.test/42";

    await captureApiEvent({
      endpoint: "POST /api/v1/rewrite",
      errorCode: "azure_unavailable",
      latencyMs: 42,
      requestId: "req_unit_123",
      statusCode: 503,
    });

    expect(fetchMock()).toHaveBeenCalledTimes(2);

    const postHogCall = fetchMock().mock.calls.find(([url]) =>
      String(url).startsWith("https://posthog.example.test/capture/"),
    );
    expect(postHogCall).toBeDefined();
    const postHogBody = JSON.parse(String(postHogCall?.[1]?.body));
    expect(postHogBody).toEqual({
      api_key: "ph_unit_key",
      distinct_id: "req_unit_123",
      event: "api_error",
      properties: {
        app: "replyinmyvoice",
        endpoint: "POST /api/v1/rewrite",
        error_code: "azure_unavailable",
        latency_ms: 42,
        request_id: "req_unit_123",
        source: "api_v1_proxy",
        status_code: 503,
      },
    });

    const sentryCall = fetchMock().mock.calls.find(([url]) =>
      String(url).startsWith("https://example.sentry.test/api/42/store/"),
    );
    expect(sentryCall).toBeDefined();
    const sentryBody = JSON.parse(String(sentryCall?.[1]?.body));
    expect(sentryBody).toMatchObject({
      extra: {
        endpoint: "POST /api/v1/rewrite",
        error_code: "azure_unavailable",
        latency_ms: 42,
        request_id: "req_unit_123",
        status_code: 503,
      },
      level: "error",
      logger: "api_v1",
      tags: {
        endpoint: "POST /api/v1/rewrite",
        request_id: "req_unit_123",
        status_code: 503,
      },
    });
  });

  it("schedules api telemetry through the Cloudflare waitUntil context", async () => {
    process.env.POSTHOG_API_KEY = "ph_unit_key";
    process.env.POSTHOG_HOST = "https://posthog.example.test";
    const waitUntil = vi.fn();
    vi.mocked(getCloudflareContext).mockReturnValue({
      ctx: {
        waitUntil,
      },
    } as never);

    scheduleApiEvent({
      endpoint: "GET /api/v1/usage",
      latencyMs: 12,
      requestId: "req_unit_wait",
      statusCode: 200,
    });

    expect(waitUntil).toHaveBeenCalledTimes(1);
    const scheduled = waitUntil.mock.calls[0][0] as Promise<unknown>;
    await scheduled;
    expect(fetchMock()).toHaveBeenCalledWith("https://posthog.example.test/capture/", {
      body: expect.stringContaining('"request_id":"req_unit_wait"'),
      headers: {
        "Content-Type": "application/json",
      },
      method: "POST",
    });
  });
});
