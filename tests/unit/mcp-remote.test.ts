import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { POST, dynamic } from "../../app/api/mcp/route";

const appUrl = "https://replyinmyvoice.com";
const authorization = "Bearer rmv_test_remote_key";

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

function jsonResponse(body: unknown, status = 200) {
  return Response.json(body, { status });
}

function request(init: RequestInit = {}) {
  return new Request(`${appUrl}/api/mcp`, {
    ...init,
    method: init.method ?? "POST",
  });
}

function mcpHeaders(headers: Record<string, string> = {}) {
  return {
    accept: "application/json, text/event-stream",
    authorization,
    "content-type": "application/json",
    origin: appUrl,
    ...headers,
  };
}

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe("/api/mcp remote route", () => {
  it("is dynamic", () => {
    expect(dynamic).toBe("force-dynamic");
  });

  it("returns a Bearer challenge when the caller sends no authorization", async () => {
    const response = await POST(
      request({
        body: JSON.stringify({
          id: 1,
          jsonrpc: "2.0",
          method: "tools/list",
        }),
        headers: {
          "content-type": "application/json",
          origin: appUrl,
        },
      }),
    );

    await expect(response.json()).resolves.toEqual({
      error: "A valid API key is required.",
    });
    expect(response.status).toBe(401);
    expect(response.headers.get("WWW-Authenticate")).toBe("Bearer");
  });

  it("rejects requests from untrusted browser origins before forwarding", async () => {
    const response = await POST(
      request({
        body: JSON.stringify({
          id: 1,
          jsonrpc: "2.0",
          method: "tools/list",
        }),
        headers: mcpHeaders({ origin: "https://client.example.test" }),
      }),
    );

    await expect(response.json()).resolves.toEqual({
      error: "Cross-origin request rejected.",
    });
    expect(response.status).toBe(403);
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("lists the shared rewrite tools for authorized callers", async () => {
    const response = await POST(
      request({
        body: JSON.stringify({
          id: 1,
          jsonrpc: "2.0",
          method: "tools/list",
        }),
        headers: mcpHeaders(),
      }),
    );

    const body = await response.json();
    expect(response.status).toBe(200);
    expect(body.result.tools.map((tool: { name: string }) => tool.name)).toEqual([
      "rewrite_email",
      "get_rewrite_result",
    ]);
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("returns a working result when remote rewrite polling reaches the cap", async () => {
    vi.useFakeTimers();
    fetchMock().mockResolvedValueOnce(jsonResponse({ id: "attempt-remote-1" }, 202));
    for (let index = 0; index < 27; index += 1) {
      fetchMock().mockResolvedValueOnce(jsonResponse({ status: "processing" }));
    }

    const responsePromise = POST(
      request({
        body: JSON.stringify({
          id: 1,
          jsonrpc: "2.0",
          method: "tools/call",
          params: {
            arguments: {
              draft: "Please let them know I can send the update tomorrow.",
            },
            name: "rewrite_email",
          },
        }),
        headers: mcpHeaders(),
      }),
    );

    await vi.advanceTimersByTimeAsync(60_000);
    const response = await responsePromise;
    const body = await response.json();

    expect(response.status).toBe(200);
    expect(body.result.structuredContent).toEqual({
      attempt_id: "attempt-remote-1",
      status: "working",
    });
    expect(body.result.content).toContainEqual({
      text: "Rewrite still working. Call get_rewrite_result with attempt_id attempt-remote-1.",
      type: "text",
    });
  });
});
