const DEFAULT_BASE_URL = "https://replyinmyvoice.com";
const DEFAULT_POLL_INTERVAL_MS = 1_500;
const DEFAULT_TIMEOUT_MS = 120_000;

export interface CreateClientOptions {
  apiKey: string;
  baseUrl?: string;
}

export interface SubmitRewriteResponse {
  id: string;
  status: string;
}

export interface NaturalnessSignal {
  draft: number;
  rewrite: number;
}

export interface RewriteResultResponse {
  id: string;
  status: string;
  rewrittenText?: string;
  signal?: NaturalnessSignal;
  error?: ApiErrorPayload;
}

export interface RewriteOptions {
  pollIntervalMs?: number;
  timeoutMs?: number;
}

export interface RewriteSuccess {
  rewrittenText: string;
  signal: NaturalnessSignal;
}

export interface UsageResponse {
  scope: string;
  quota: number;
  used: number;
  remaining: number;
  periodEnd: string;
}

export interface RimvClient {
  submitRewrite(draft: string): Promise<SubmitRewriteResponse>;
  getRewrite(id: string): Promise<RewriteResultResponse>;
  rewrite(draft: string, opts?: RewriteOptions): Promise<RewriteSuccess>;
  getUsage(): Promise<UsageResponse>;
}

export interface ApiErrorPayload {
  code: string;
  message: string;
}

interface ErrorResponseBody {
  error?: Partial<ApiErrorPayload>;
}

export class RimvApiError extends Error {
  readonly code: string;
  readonly status: number;

  constructor(error: { code: string; message: string; status: number }) {
    super(error.message);
    this.name = "RimvApiError";
    this.code = error.code;
    this.status = error.status;
    Object.setPrototypeOf(this, RimvApiError.prototype);
  }
}

export function createClient({ apiKey, baseUrl = DEFAULT_BASE_URL }: CreateClientOptions): RimvClient {
  const normalizedBaseUrl = normalizeBaseUrl(baseUrl);
  const authHeaders = {
    Authorization: `Bearer ${apiKey}`,
  };

  async function request<T>(path: string, init: RequestInit): Promise<T> {
    const response = await fetch(`${normalizedBaseUrl}${path}`, init);
    const body = await readJson(response);

    if (!response.ok) {
      throw toApiError(body, response.status);
    }

    return body as T;
  }

  async function submitRewrite(draft: string): Promise<SubmitRewriteResponse> {
    return request<SubmitRewriteResponse>("/api/v1/rewrite", {
      body: JSON.stringify({ draft }),
      headers: {
        ...authHeaders,
        "Content-Type": "application/json",
      },
      method: "POST",
    });
  }

  async function getRewrite(id: string): Promise<RewriteResultResponse> {
    return request<RewriteResultResponse>(`/api/v1/rewrite/${encodeURIComponent(id)}`, {
      headers: authHeaders,
      method: "GET",
    });
  }

  async function rewrite(draft: string, opts: RewriteOptions = {}): Promise<RewriteSuccess> {
    const pollIntervalMs = normalizeDuration(opts.pollIntervalMs, DEFAULT_POLL_INTERVAL_MS);
    const timeoutMs = normalizeDuration(opts.timeoutMs, DEFAULT_TIMEOUT_MS);
    const startedAt = Date.now();
    const { id } = await submitRewrite(draft);

    for (;;) {
      const result = await getRewrite(id);

      if (result.status === "succeeded") {
        if (typeof result.rewrittenText === "string" && result.signal) {
          return {
            rewrittenText: result.rewrittenText,
            signal: result.signal,
          };
        }

        throw new RimvApiError({
          code: "invalid_response",
          message: "Rewrite response was missing required fields.",
          status: 200,
        });
      }

      if (result.status === "failed") {
        throw new RimvApiError({
          code: result.error?.code ?? "rewrite_failed",
          message: result.error?.message ?? "Rewrite request failed.",
          status: 200,
        });
      }

      const elapsedMs = Date.now() - startedAt;
      if (elapsedMs >= timeoutMs) {
        throw timeoutError();
      }

      await sleep(Math.min(pollIntervalMs, timeoutMs - elapsedMs));

      if (Date.now() - startedAt >= timeoutMs) {
        throw timeoutError();
      }
    }
  }

  async function getUsage(): Promise<UsageResponse> {
    return request<UsageResponse>("/api/v1/usage", {
      headers: authHeaders,
      method: "GET",
    });
  }

  return {
    getRewrite,
    getUsage,
    rewrite,
    submitRewrite,
  };
}

function normalizeBaseUrl(baseUrl: string): string {
  const trimmed = baseUrl.trim();
  return (trimmed || DEFAULT_BASE_URL).replace(/\/+$/, "");
}

function normalizeDuration(value: number | undefined, fallback: number): number {
  if (typeof value !== "number" || !Number.isFinite(value) || value < 0) {
    return fallback;
  }

  return value;
}

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) {
    return undefined;
  }

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return undefined;
  }
}

function toApiError(body: unknown, status: number): RimvApiError {
  const error = readErrorPayload(body);
  return new RimvApiError({
    code: error.code,
    message: error.message,
    status,
  });
}

function readErrorPayload(body: unknown): ApiErrorPayload {
  if (isErrorResponseBody(body)) {
    return {
      code: typeof body.error.code === "string" ? body.error.code : "api_error",
      message:
        typeof body.error.message === "string"
          ? body.error.message
          : "Reply In My Voice API request failed.",
    };
  }

  return {
    code: "api_error",
    message: "Reply In My Voice API request failed.",
  };
}

function isErrorResponseBody(body: unknown): body is { error: Partial<ApiErrorPayload> } {
  return (
    typeof body === "object" &&
    body !== null &&
    "error" in body &&
    typeof (body as ErrorResponseBody).error === "object" &&
    (body as ErrorResponseBody).error !== null
  );
}

function timeoutError(): RimvApiError {
  return new RimvApiError({
    code: "timeout",
    message: "Rewrite request timed out.",
    status: 0,
  });
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
