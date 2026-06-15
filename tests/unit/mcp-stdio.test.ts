import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createServer } from "@replyinmyvoiceashuman/mcp-server";

const apiKey = "rmv_test_stdio_key";
const baseUrl = "https://api.example.test";

interface RequestHandlerExtra {
  signal: AbortSignal;
  requestId: string;
  sendNotification: (notification: unknown) => Promise<void>;
  sendRequest: (
    request: unknown,
    resultSchema: unknown,
    options?: unknown,
  ) => Promise<unknown>;
}

type RegisteredRequestHandler = (
  request: { method: string; params?: Record<string, unknown> },
  extra: RequestHandlerExtra,
) => Promise<unknown> | unknown;

interface ServerWithRequestHandlers {
  _requestHandlers: Map<string, RegisteredRequestHandler>;
}

interface ListToolsResult {
  tools: Array<{ name: string }>;
}

interface ToolCallResult {
  content: Array<{ type: string; text: string }>;
  structuredContent?: {
    attempt_id?: string;
    rewritten?: string;
    changes?: string[];
  };
  isError?: boolean;
}

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

function jsonResponse(body: unknown, status = 200) {
  return Response.json(body, { status });
}

function getRequestHandler(
  server: unknown,
  method: string,
): RegisteredRequestHandler {
  const handler = (server as ServerWithRequestHandlers)._requestHandlers.get(method);

  expect(handler, `${method} handler`).toBeDefined();
  return handler as RegisteredRequestHandler;
}

function createExtra(): RequestHandlerExtra {
  return {
    requestId: "stdio-unit-test",
    sendNotification: vi.fn(async () => undefined),
    sendRequest: vi.fn(async () => ({})),
    signal: new AbortController().signal,
  };
}

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("MCP stdio server handlers", () => {
  it("registers the two rewrite tools on tools/list", async () => {
    const server = createServer({ config: { apiKey, baseUrl } });
    const handler = getRequestHandler(server, "tools/list");

    const result = (await handler(
      { method: "tools/list" },
      createExtra(),
    )) as ListToolsResult;

    expect(result.tools.map((tool) => tool.name)).toEqual([
      "rewrite_email",
      "get_rewrite_result",
    ]);
    expect(result.tools).toHaveLength(2);
  });

  it("returns final rewrite text from tools/call rewrite_email", async () => {
    fetchMock()
      .mockResolvedValueOnce(jsonResponse({ id: "attempt-stdio-1" }, 202))
      .mockResolvedValueOnce(
        jsonResponse({
          changes: ["Kept the promised timing."],
          rewritten: "Thanks for checking in. I can send the update today.",
          status: "succeeded",
        }),
      );

    const server = createServer({ config: { apiKey, baseUrl } });
    const handler = getRequestHandler(server, "tools/call");

    const result = (await handler(
      {
        method: "tools/call",
        params: {
          arguments: {
            draft: "Please tell them I will send the update today.",
          },
          name: "rewrite_email",
        },
      },
      createExtra(),
    )) as ToolCallResult;

    expect(result.isError).not.toBe(true);
    expect(result.structuredContent).toEqual({
      attempt_id: "attempt-stdio-1",
      changes: ["Kept the promised timing."],
      rewritten: "Thanks for checking in. I can send the update today.",
    });
    expect(result.content).toContainEqual({
      text: "Thanks for checking in. I can send the update today.",
      type: "text",
    });
  });
});
