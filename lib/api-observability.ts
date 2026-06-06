type ApiObservabilityEnv = {
  NODE_ENV?: string;
  POSTHOG_API_KEY?: string;
  POSTHOG_HOST?: string;
  SENTRY_DSN?: string;
};

type ApiPropertyValue = string | number | boolean | null;

export type ApiEventProperties = Record<string, ApiPropertyValue | undefined>;

export type ApiEventInput = {
  endpoint: string;
  statusCode: number;
  latencyMs?: number;
  errorCode?: string;
  requestId?: string;
  error?: unknown;
};

export type ApiCaptureResult = {
  delivered: boolean;
  reason?: string;
  transport: "posthog" | "sentry";
};

type SentryTarget = {
  endpoint: string;
  publicKey: string;
};

const defaultPostHogHost = "https://app.posthog.com";
const sentryClientName = "replyinmyvoice-worker/1.0";

export async function captureApiEvent(
  input: ApiEventInput,
  env: ApiObservabilityEnv = process.env,
): Promise<ApiCaptureResult[]> {
  const apiKey = env.POSTHOG_API_KEY?.trim();
  const { target: sentryTarget } = parseSentryDsn(env.SENTRY_DSN);
  const tasks: Array<Promise<ApiCaptureResult>> = [];

  if (apiKey) {
    tasks.push(capturePostHogApiEvent(input, apiKey, env.POSTHOG_HOST));
  }

  if (sentryTarget && shouldReportToSentry(input)) {
    tasks.push(captureSentryApiError(input, sentryTarget, env));
  }

  return Promise.all(tasks);
}

export function parseApiErrorCode(body: string): string | undefined {
  try {
    const parsed = JSON.parse(body) as unknown;
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
      return undefined;
    }

    const error = (parsed as Record<string, unknown>).error;
    if (!error || typeof error !== "object" || Array.isArray(error)) {
      return undefined;
    }

    const code = (error as Record<string, unknown>).code;
    return typeof code === "string" && code.trim() ? code.trim() : undefined;
  } catch {
    return undefined;
  }
}

export function readApiRequestId(headers: Headers): string | undefined {
  return textHeader(headers, "x-request-id") ?? textHeader(headers, "x-correlation-id");
}

async function capturePostHogApiEvent(
  input: ApiEventInput,
  apiKey: string,
  host: string | undefined,
): Promise<ApiCaptureResult> {
  try {
    const response = await fetch(`${normalizeHost(host)}/capture/`, {
      body: JSON.stringify({
        api_key: apiKey,
        distinct_id: input.requestId ?? "api-v1",
        event: isApiError(input) ? "api_error" : "api_request",
        properties: eventProperties(input),
      }),
      headers: {
        "Content-Type": "application/json",
      },
      method: "POST",
    });

    if (!response.ok) {
      return { delivered: false, reason: "posthog_rejected", transport: "posthog" };
    }

    return { delivered: true, transport: "posthog" };
  } catch {
    return { delivered: false, reason: "posthog_request_failed", transport: "posthog" };
  }
}

async function captureSentryApiError(
  input: ApiEventInput,
  target: SentryTarget,
  env: ApiObservabilityEnv,
): Promise<ApiCaptureResult> {
  const normalized = normalizeError(input.error, input);
  const payload = {
    environment: env.NODE_ENV ?? "development",
    event_id: createSentryEventId(),
    exception: {
      values: [
        {
          type: normalized.name,
          value: normalized.message,
        },
      ],
    },
    extra: eventProperties(input),
    level: "error",
    logger: "api_v1",
    platform: "javascript",
    tags: sanitizeProperties({
      endpoint: input.endpoint,
      request_id: input.requestId,
      status_code: input.statusCode,
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
      return { delivered: false, reason: "sentry_rejected", transport: "sentry" };
    }

    return { delivered: true, transport: "sentry" };
  } catch {
    return { delivered: false, reason: "sentry_request_failed", transport: "sentry" };
  }
}

function isApiError(input: ApiEventInput) {
  return input.statusCode >= 400 || Boolean(input.errorCode);
}

function shouldReportToSentry(input: ApiEventInput) {
  return input.statusCode >= 500 || Boolean(input.error);
}

function eventProperties(input: ApiEventInput) {
  return sanitizeProperties({
    app: "replyinmyvoice",
    endpoint: input.endpoint,
    error_code: input.errorCode,
    latency_ms: input.latencyMs,
    request_id: input.requestId,
    source: "api_v1_proxy",
    status_code: input.statusCode,
  });
}

function normalizeHost(host = defaultPostHogHost) {
  return host.trim().replace(/\/$/, "") || defaultPostHogHost;
}

function textHeader(headers: Headers, name: string) {
  const value = headers.get(name);
  return value?.trim() || undefined;
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

function normalizeError(error: unknown, input: ApiEventInput) {
  if (error instanceof Error) {
    return {
      message: error.message,
      name: error.name || "Error",
    };
  }

  if (typeof error === "string") {
    return {
      message: error,
      name: "Error",
    };
  }

  const code = input.errorCode ? ` (${input.errorCode})` : "";
  return {
    message: `API request failed with status ${input.statusCode}${code}`,
    name: "Error",
  };
}

function sanitizeProperties(properties: ApiEventProperties): Record<string, ApiPropertyValue> {
  return Object.fromEntries(
    Object.entries(properties)
      .filter(([, value]) => value !== undefined)
      .map(([key, value]) => [key, value ?? null]),
  );
}
