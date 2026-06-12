import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { WebStandardStreamableHTTPServerTransport as StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/webStandardStreamableHttp.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";
import { NextResponse } from "next/server";

import {
  HttpRewriteBackend,
  type RewriteBackend,
} from "../../../packages/mcp-server/src/backend/RewriteBackend";
import {
  callTool,
  listTools,
  type ToolOutput,
} from "../../../packages/mcp-server/src/tools";
import { MCP_SERVER_VERSION } from "../../../packages/mcp-server/src/version";
import { isAllowedOrigin } from "../../../lib/security";

export const dynamic = "force-dynamic";

const REMOTE_POLL_MAX_ATTEMPTS = 27;
const REMOTE_POLL_INITIAL_DELAY_MS = 500;
const REMOTE_POLL_MAX_DELAY_MS = 2_000;
const AUTH_CHALLENGE = "Bearer";

type RequestHandlerExtra = {
  _meta?: {
    progressToken?: string | number;
  };
  requestId: string | number;
  sendNotification: (notification: {
    method: "notifications/progress";
    params: {
      progressToken: string | number;
      progress: number;
      total: number;
      message: string;
    };
  }) => Promise<void>;
};

type RemoteToolOutput =
  | ToolOutput
  | {
      status: "working";
      attempt_id: string;
    };

type BearerAuth =
  | {
      ok: true;
      apiKey: string;
    }
  | {
      ok: false;
    };

function parseBearerAuth(request: Request): BearerAuth {
  const authorization = request.headers.get("authorization");
  const match = authorization?.match(/^Bearer\s+(.+)$/i);
  const apiKey = match?.[1]?.trim();

  if (!apiKey) {
    return { ok: false };
  }

  return { ok: true, apiKey };
}

function unauthorizedResponse() {
  return NextResponse.json(
    { error: "A valid API key is required." },
    {
      headers: {
        "WWW-Authenticate": AUTH_CHALLENGE,
      },
      status: 401,
    },
  );
}

function originRejectedResponse() {
  return NextResponse.json({ error: "Cross-origin request rejected." }, { status: 403 });
}

function isSafeOrigin(request: Request): boolean {
  const origin = request.headers.get("origin");

  if (!origin) {
    return true;
  }

  return isAllowedOrigin(origin, {
    requestUrl: request.url,
  });
}

function backendBaseUrl(request: Request): string {
  return new URL(request.url).origin;
}

function createServer(apiKey: string, request: Request): Server {
  const server = new Server(
    {
      name: "replyinmyvoice-remote",
      version: MCP_SERVER_VERSION,
    },
    {
      capabilities: { tools: {} },
    },
  );

  server.setRequestHandler(ListToolsRequestSchema, () => ({
    tools: listTools(),
  }));

  server.setRequestHandler(CallToolRequestSchema, async (toolRequest, extra) => {
    const output = await callRemoteTool(toolRequest.params.name, toolRequest.params.arguments, {
      apiKey,
      backend: new HttpRewriteBackend(backendBaseUrl(request)),
      extra: extra as RequestHandlerExtra,
    });

    return toCallToolResult(output);
  });

  return server;
}

async function callRemoteTool(
  name: string,
  args: unknown,
  context: {
    apiKey: string;
    backend: RewriteBackend;
    extra: RequestHandlerExtra;
  },
): Promise<RemoteToolOutput> {
  let attemptId: string | null = null;
  const progress = createProgressSender(context.extra);
  const backend: RewriteBackend = {
    submit: async (request, options) => {
      const result = await context.backend.submit(request, options);
      attemptId = result.attemptId;
      await progress(0, "Rewrite request accepted.");
      return result;
    },
    poll: async (id, options) => {
      const result = await context.backend.poll(id, options);
      if (result.status === "working") {
        await progress.next("Rewrite still in progress.");
      }
      return result;
    },
  };

  try {
    return await callTool(name, args, {
      apiKey: context.apiKey,
      backend,
      poll: {
        initialDelayMs: REMOTE_POLL_INITIAL_DELAY_MS,
        maxAttempts: REMOTE_POLL_MAX_ATTEMPTS,
        maxDelayMs: REMOTE_POLL_MAX_DELAY_MS,
      },
    });
  } catch (error) {
    if (name === "rewrite_email" && attemptId && isPollLimitError(error)) {
      return {
        attempt_id: attemptId,
        status: "working",
      };
    }

    throw error;
  }
}

function createProgressSender(extra: RequestHandlerExtra) {
  const progressToken = extra._meta?.progressToken ?? extra.requestId;
  let pollCount = 0;

  async function send(progress: number, message: string) {
    await extra.sendNotification({
      method: "notifications/progress",
      params: {
        message,
        progress,
        progressToken,
        total: 1,
      },
    });
  }

  send.next = async (message: string) => {
    pollCount += 1;
    await send(Math.min(pollCount / REMOTE_POLL_MAX_ATTEMPTS, 0.95), message);
  };

  return send;
}

function isPollLimitError(error: unknown): boolean {
  return (
    error instanceof Error &&
    error.message === "Rewrite did not finish within the polling limit."
  );
}

function toCallToolResult(output: RemoteToolOutput) {
  return {
    content: [
      {
        text: toToolText(output),
        type: "text" as const,
      },
    ],
    structuredContent: output,
  };
}

function toToolText(output: RemoteToolOutput): string {
  if ("rewritten" in output && output.rewritten) {
    return output.rewritten;
  }

  if ("status" in output && "attempt_id" in output && output.status === "working") {
    return `Rewrite still working. Call get_rewrite_result with attempt_id ${output.attempt_id}.`;
  }

  if ("status" in output) {
    return `Rewrite status: ${output.status}`;
  }

  return "Rewrite request returned no text.";
}

async function handleMcpRequest(request: Request): Promise<Response> {
  if (!isSafeOrigin(request)) {
    return originRejectedResponse();
  }

  const auth = parseBearerAuth(request);
  if (!auth.ok) {
    return unauthorizedResponse();
  }

  const server = createServer(auth.apiKey, request);
  const transport = new StreamableHTTPServerTransport({
    enableJsonResponse: true,
    sessionIdGenerator: undefined,
  });

  await server.connect(transport);
  return transport.handleRequest(request);
}

export async function POST(request: Request) {
  return handleMcpRequest(request);
}
