export type PaymentFunnelEvent =
  | "checkout_started"
  | "checkout_redirected"
  | "payment_succeeded"
  | "payment_failed"
  | "webhook_failed";

export type PaymentErrorEvent =
  | PaymentFunnelEvent
  | "account_proxy_failed"
  | "billing_portal_failed"
  | "checkout_proxy_failed"
  | "webhook_proxy_failed";

type ObservabilityEnv = {
  NODE_ENV?: string;
  POSTHOG_API_KEY?: string;
  POSTHOG_HOST?: string;
  SENTRY_DSN?: string;
};

type PaymentPropertyValue = string | number | boolean | null;

export type PaymentEventProperties = Record<string, PaymentPropertyValue | undefined>;

export type CaptureResult = {
  delivered: boolean;
  reason?: string;
};

type SentryTarget = {
  endpoint: string;
  publicKey: string;
};

const defaultPostHogHost = "https://app.posthog.com";
const sentryClientName = "replyinmyvoice-worker/1.0";

export function initializePaymentObservability(
  env: ObservabilityEnv = process.env,
) {
  return {
    postHogEnabled: Boolean(env.POSTHOG_API_KEY?.trim()),
    sentryEnabled: parseSentryDsn(env.SENTRY_DSN).target !== null,
  };
}

export function createPaymentCorrelationId(prefix = "pay") {
  const id = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;
  return `${prefix}_${id}`;
}

export async function capturePaymentEvent(
  event: PaymentFunnelEvent,
  properties: PaymentEventProperties = {},
  env: ObservabilityEnv = process.env,
): Promise<CaptureResult> {
  const apiKey = env.POSTHOG_API_KEY?.trim();
  if (!apiKey) {
    return { delivered: false, reason: "missing_posthog_key" };
  }

  try {
    const response = await fetch(`${normalizeHost(env.POSTHOG_HOST)}/capture/`, {
      body: JSON.stringify({
        api_key: apiKey,
        distinct_id: properties.distinctId ?? "payment-funnel",
        event,
        properties: sanitizeProperties({
          ...properties,
          app: "replyinmyvoice",
          source: properties.source ?? "worker",
        }),
      }),
      headers: {
        "Content-Type": "application/json",
      },
      method: "POST",
    });

    if (!response.ok) {
      return { delivered: false, reason: "posthog_rejected" };
    }

    return { delivered: true };
  } catch {
    return { delivered: false, reason: "posthog_request_failed" };
  }
}

export async function capturePaymentError(
  error: unknown,
  context: {
    correlationId: string;
    event: PaymentErrorEvent;
    source: string;
    status?: number;
    properties?: PaymentEventProperties;
  },
  env: ObservabilityEnv = process.env,
): Promise<CaptureResult> {
  const { target, reason } = parseSentryDsn(env.SENTRY_DSN);
  if (!target) {
    return { delivered: false, reason };
  }

  const normalized = normalizeError(error);
  const eventId = createSentryEventId();
  const payload = {
    environment: env.NODE_ENV ?? "development",
    event_id: eventId,
    exception: {
      values: [
        {
          type: normalized.name,
          value: normalized.message,
        },
      ],
    },
    extra: sanitizeProperties(context.properties ?? {}),
    level: "error",
    logger: "payment",
    platform: "javascript",
    tags: sanitizeProperties({
      correlation_id: context.correlationId,
      payment_event: context.event,
      source: context.source,
      status: context.status ?? null,
    }),
    timestamp: new Date().toISOString(),
  };

  try {
    const response = await fetch(target.endpoint, {
      body: JSON.stringify(payload),
      headers: {
        "Content-Type": "application/json",
        "X-Sentry-Auth": [
          "Sentry sentry_version=7",
          `sentry_client=${sentryClientName}`,
          `sentry_key=${target.publicKey}`,
        ].join(", "),
      },
      method: "POST",
    });

    if (!response.ok) {
      return { delivered: false, reason: "sentry_rejected" };
    }

    return { delivered: true };
  } catch {
    return { delivered: false, reason: "sentry_request_failed" };
  }
}

function normalizeHost(host = defaultPostHogHost) {
  return host.trim().replace(/\/$/, "") || defaultPostHogHost;
}

function parseSentryDsn(dsn: string | undefined): {
  target: SentryTarget | null;
  reason: string;
} {
  if (!dsn?.trim()) {
    return { target: null, reason: "missing_sentry_dsn" };
  }

  try {
    const url = new URL(dsn);
    const publicKey = url.username;
    const projectId = url.pathname.split("/").filter(Boolean).at(-1);
    if (!publicKey || !projectId) {
      return { target: null, reason: "invalid_sentry_dsn" };
    }

    return {
      reason: "",
      target: {
        endpoint: `${url.protocol}//${url.host}/api/${projectId}/store/`,
        publicKey,
      },
    };
  } catch {
    return { target: null, reason: "invalid_sentry_dsn" };
  }
}

function createSentryEventId() {
  return (globalThis.crypto?.randomUUID?.() ?? `${Date.now()}${Math.random()}`)
    .replace(/[^a-f0-9]/gi, "")
    .padEnd(32, "0")
    .slice(0, 32);
}

function normalizeError(error: unknown) {
  if (error instanceof Error) {
    return {
      message: error.message,
      name: error.name || "Error",
    };
  }

  return {
    message: typeof error === "string" ? error : "Unknown payment error",
    name: "Error",
  };
}

function sanitizeProperties(properties: PaymentEventProperties): Record<string, PaymentPropertyValue> {
  return Object.fromEntries(
    Object.entries(properties)
      .filter(([, value]) => value !== undefined)
      .map(([key, value]) => [key, value ?? null]),
  );
}
