import { createHash } from "node:crypto";

export interface RewriteRequest {
  // The v1 API (V1RewriteSubmitRequest) accepts exactly this one field.
  draft: string;
}

export interface RewriteBackend {
  submit(
    request: RewriteRequest,
    options: { apiKey: string },
  ): Promise<{ attemptId: string }>;
  poll(
    attemptId: string,
    options: { apiKey: string },
  ): Promise<{
    status: "working" | "succeeded" | "failed";
    rewritten?: string;
    changes?: string[];
  }>;
}

interface ApiErrorPayload {
  code?: string;
  message?: string;
}

interface ErrorResponseBody {
  error?: ApiErrorPayload;
}

interface SubmitResponseBody {
  id?: unknown;
}

interface PollResponseBody {
  status?: unknown;
  rewritten?: unknown;
  rewrittenText?: unknown;
  changes?: unknown;
  changeSummary?: unknown;
}

export class RewriteBackendError extends Error {
  readonly code: string;
  readonly status: number;

  constructor(error: { code: string; message: string; status: number }) {
    super(error.message);
    this.name = "RewriteBackendError";
    this.code = error.code;
    this.status = error.status;
    Object.setPrototypeOf(this, RewriteBackendError.prototype);
  }
}

export class HttpRewriteBackend implements RewriteBackend {
  private readonly baseUrl: string;

  constructor(baseUrl: string) {
    this.baseUrl = normalizeBaseUrl(baseUrl);
  }

  async submit(
    request: RewriteRequest,
    options: { apiKey: string },
  ): Promise<{ attemptId: string }> {
    const response = await fetch(`${this.baseUrl}/api/v1/rewrite`, {
      body: JSON.stringify(request),
      headers: {
        Authorization: `Bearer ${options.apiKey}`,
        "Content-Type": "application/json",
        "Idempotency-Key": buildIdempotencyKey(request),
      },
      method: "POST",
    });
    const body = await readJson(response);

    if (!response.ok) {
      throw toBackendError(body, response.status);
    }

    if (response.status !== 202 || !isSubmitResponseBody(body)) {
      throw new RewriteBackendError({
        code: "invalid_response",
        message: "Rewrite submit response was missing required fields.",
        status: response.status,
      });
    }

    return { attemptId: body.id };
  }

  async poll(
    attemptId: string,
    options: { apiKey: string },
  ): Promise<{
    status: "working" | "succeeded" | "failed";
    rewritten?: string;
    changes?: string[];
  }> {
    const response = await fetch(
      `${this.baseUrl}/api/v1/rewrite/${encodeURIComponent(attemptId)}`,
      {
        headers: {
          Authorization: `Bearer ${options.apiKey}`,
        },
        method: "GET",
      },
    );
    const body = await readJson(response);

    if (!response.ok) {
      throw toBackendError(body, response.status);
    }

    if (!isPollResponseBody(body)) {
      throw new RewriteBackendError({
        code: "invalid_response",
        message: "Rewrite result response was missing required fields.",
        status: response.status,
      });
    }

    const status = normalizeStatus(body.status);
    const rewritten = readString(body.rewritten) ?? readString(body.rewrittenText);
    const changes = readStringArray(body.changes) ?? readStringArray(body.changeSummary);

    return {
      status,
      ...(rewritten ? { rewritten } : {}),
      ...(changes ? { changes } : {}),
    };
  }
}

export function buildIdempotencyKey(request: RewriteRequest): string {
  return createHash("sha256").update(canonicalJson(request)).digest("hex");
}

function normalizeBaseUrl(baseUrl: string): string {
  const trimmed = baseUrl.trim();
  return (trimmed || "https://replyinmyvoice.com").replace(/\/+$/, "");
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

function toBackendError(body: unknown, status: number): RewriteBackendError {
  const error = readErrorPayload(body);
  return new RewriteBackendError({
    code: error.code,
    message: error.message,
    status,
  });
}

function readErrorPayload(body: unknown): { code: string; message: string } {
  if (
    typeof body === "object" &&
    body !== null &&
    "error" in body &&
    typeof (body as ErrorResponseBody).error === "object" &&
    (body as ErrorResponseBody).error !== null
  ) {
    const error = (body as ErrorResponseBody).error;
    return {
      code: typeof error?.code === "string" ? error.code : "api_error",
      message:
        typeof error?.message === "string"
          ? error.message
          : "Reply In My Voice API request failed.",
    };
  }

  return {
    code: "api_error",
    message: "Reply In My Voice API request failed.",
  };
}

function isSubmitResponseBody(body: unknown): body is { id: string } {
  if (typeof body !== "object" || body === null) {
    return false;
  }

  const id = (body as SubmitResponseBody).id;
  return typeof id === "string" && id.trim().length > 0;
}

function isPollResponseBody(body: unknown): body is PollResponseBody {
  return (
    typeof body === "object" &&
    body !== null &&
    typeof (body as PollResponseBody).status === "string"
  );
}

function normalizeStatus(status: unknown): "working" | "succeeded" | "failed" {
  if (status === "succeeded" || status === "failed") {
    return status;
  }

  if (status === "working" || status === "processing" || status === "pending") {
    return "working";
  }

  throw new RewriteBackendError({
    code: "invalid_response",
    message: "Rewrite result response had an unknown status.",
    status: 200,
  });
}

function readString(value: unknown): string | undefined {
  return typeof value === "string" && value.trim().length > 0 ? value : undefined;
}

function readStringArray(value: unknown): string[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const items = value.filter((item): item is string => typeof item === "string");
  return items.length > 0 ? items : undefined;
}

function canonicalJson(value: unknown): string {
  return JSON.stringify(canonicalValue(value));
}

function canonicalValue(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map((item) => canonicalValue(item));
  }

  if (value && typeof value === "object") {
    const entries = Object.entries(value)
      .filter(([, entryValue]) => entryValue !== undefined)
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, entryValue]) => [key, canonicalValue(entryValue)]);

    return Object.fromEntries(entries);
  }

  return value;
}
