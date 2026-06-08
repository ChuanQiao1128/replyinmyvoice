import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  HttpRewriteBackend,
  type RewriteBackend,
  type RewriteRequest,
} from "../../packages/mcp-server/src/backend/RewriteBackend";
import { callTool, listTools } from "../../packages/mcp-server/src/tools";

const apiKey = "rmv_live_unit_test_key";
const baseUrl = "https://api.example.test";
const draft = "Please send a warmer update about the delayed shipment.";
const context = "A customer asked why shipment 123 is delayed until Friday.";

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

function jsonResponse(body: unknown, status = 200) {
  return Response.json(body, { status });
}

function createBackend(
  responses: Array<Awaited<ReturnType<RewriteBackend["poll"]>>>,
): RewriteBackend {
  return {
    submit: vi.fn(async () => ({ attemptId: "attempt-123" })),
    poll: vi.fn(async () => {
      const next = responses.shift();
      if (!next) {
        throw new Error("poll called too many times");
      }
      return next;
    }),
  };
}

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe("MCP shared tool core", () => {
  it("lists exactly the rewrite tools", () => {
    expect(listTools().map((tool) => tool.name)).toEqual([
      "rewrite_email",
      "get_rewrite_result",
    ]);
    expect(listTools()).toHaveLength(2);
  });

  it("maps rewrite_email input to the domain rewrite request and returns final text", async () => {
    const backend = createBackend([
      {
        status: "succeeded",
        rewritten: "I can send a clearer update about the delayed shipment.",
        changes: ["Kept the shipment timing and made the reply warmer."],
      },
    ]);

    await expect(
      callTool(
        "rewrite_email",
        { context, draft, tone: "direct" },
        { apiKey, backend },
      ),
    ).resolves.toEqual({
      attempt_id: "attempt-123",
      changes: ["Kept the shipment timing and made the reply warmer."],
      rewritten: "I can send a clearer update about the delayed shipment.",
    });

    expect(backend.submit).toHaveBeenCalledWith(
      {
        messageToReplyTo: context,
        roughDraftReply: draft,
        tone: "direct",
      },
      { apiKey },
    );
    expect(backend.poll).toHaveBeenCalledWith("attempt-123", { apiKey });
  });

  it("defaults rewrite_email tone to warm and does not expose engine-only keys", async () => {
    const backend = createBackend([
      {
        status: "succeeded",
        rewritten: "I can send a warmer update about the delayed shipment.",
      },
    ]);

    const result = await callTool("rewrite_email", { draft }, { apiKey, backend });

    expect(backend.submit).toHaveBeenCalledWith(
      {
        roughDraftReply: draft,
        tone: "warm",
      },
      { apiKey },
    );
    expect(result).toEqual({
      attempt_id: "attempt-123",
      rewritten: "I can send a warmer update about the delayed shipment.",
    });
    expect(result).not.toHaveProperty("signal_score");
    expect(result).not.toHaveProperty("strategy");
    expect(result).not.toHaveProperty("scenario");
  });

  it("polls rewrite_email until the rewrite reaches a terminal state", async () => {
    vi.useFakeTimers();
    const backend = createBackend([
      { status: "working" },
      {
        status: "succeeded",
        rewritten: "I can send a warmer update about the delayed shipment.",
      },
    ]);

    const result = callTool("rewrite_email", { draft }, { apiKey, backend });

    await vi.advanceTimersByTimeAsync(500);

    await expect(result).resolves.toEqual({
      attempt_id: "attempt-123",
      rewritten: "I can send a warmer update about the delayed shipment.",
    });
    expect(backend.poll).toHaveBeenCalledTimes(2);
  });

  it("polls get_rewrite_result once", async () => {
    const backend = createBackend([{ status: "working" }]);

    await expect(
      callTool(
        "get_rewrite_result",
        { attempt_id: "attempt-123" },
        { apiKey, backend },
      ),
    ).resolves.toEqual({ status: "working" });

    expect(backend.submit).not.toHaveBeenCalled();
    expect(backend.poll).toHaveBeenCalledOnce();
    expect(backend.poll).toHaveBeenCalledWith("attempt-123", { apiKey });
  });
});

describe("HTTP rewrite backend", () => {
  it("uses a stable idempotency key for identical rewrite requests", async () => {
    fetchMock()
      .mockResolvedValueOnce(jsonResponse({ id: "attempt-1", status: "processing" }, 202))
      .mockResolvedValueOnce(jsonResponse({ id: "attempt-1", status: "processing" }, 202));
    const backend = new HttpRewriteBackend(`${baseUrl}/`);
    const request: RewriteRequest = {
      messageToReplyTo: context,
      roughDraftReply: draft,
      tone: "warm",
    };

    await backend.submit(request, { apiKey });
    await backend.submit(
      {
        roughDraftReply: draft,
        messageToReplyTo: context,
        tone: "warm",
      },
      { apiKey },
    );

    const firstHeaders = new Headers(fetchMock().mock.calls[0]?.[1]?.headers);
    const secondHeaders = new Headers(fetchMock().mock.calls[1]?.[1]?.headers);
    expect(firstHeaders.get("Idempotency-Key")).toEqual(
      secondHeaders.get("Idempotency-Key"),
    );
    expect(firstHeaders.get("Idempotency-Key")).toMatch(/^[a-f0-9]{64}$/);
  });
});
