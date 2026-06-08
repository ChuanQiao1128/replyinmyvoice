import type { RewriteBackend, RewriteRequest } from "../backend/RewriteBackend.js";

export interface ToolDefinition {
  name: "rewrite_email" | "get_rewrite_result";
  description: string;
  inputSchema: {
    type: "object";
    properties: Record<string, unknown>;
    required: string[];
    additionalProperties: false;
  };
}

export interface CallToolContext {
  backend: RewriteBackend;
  apiKey: string;
  poll?: {
    initialDelayMs?: number;
    maxDelayMs?: number;
    maxAttempts?: number;
  };
}

export type ToolOutput =
  | {
      rewritten: string;
      changes?: string[];
      attempt_id: string;
    }
  | {
      status: "working" | "succeeded" | "failed";
      rewritten?: string;
      changes?: string[];
    };

const DEFAULT_INITIAL_DELAY_MS = 500;
const DEFAULT_MAX_DELAY_MS = 2_000;
const DEFAULT_MAX_ATTEMPTS = 60;

const tools: ToolDefinition[] = [
  {
    name: "rewrite_email",
    description:
      "Rewrite a draft email reply, returning a more natural version that preserves the meaning.",
    inputSchema: {
      type: "object",
      additionalProperties: false,
      required: ["draft"],
      properties: {
        draft: {
          type: "string",
          description: "Draft reply to rewrite (10–2400 characters).",
        },
      },
    },
  },
  {
    name: "get_rewrite_result",
    description: "Get the current result for a rewrite attempt.",
    inputSchema: {
      type: "object",
      additionalProperties: false,
      required: ["attempt_id"],
      properties: {
        attempt_id: {
          type: "string",
          description: "Rewrite attempt id returned by rewrite_email.",
        },
      },
    },
  },
];

export function listTools(): ToolDefinition[] {
  return tools.map((tool) => ({
    ...tool,
    inputSchema: {
      ...tool.inputSchema,
      properties: { ...tool.inputSchema.properties },
      required: [...tool.inputSchema.required],
    },
  }));
}

export async function callTool(
  name: string,
  args: unknown,
  context: CallToolContext,
): Promise<ToolOutput> {
  if (name === "rewrite_email") {
    const request = toRewriteRequest(args);
    const { attemptId } = await context.backend.submit(request, {
      apiKey: context.apiKey,
    });
    const result = await pollUntilTerminal(attemptId, context);

    if (result.status === "failed") {
      throw new Error("Rewrite failed.");
    }

    if (result.status !== "succeeded" || !result.rewritten) {
      throw new Error("Rewrite did not finish within the polling limit.");
    }

    return {
      attempt_id: attemptId,
      rewritten: result.rewritten,
      ...(result.changes ? { changes: result.changes } : {}),
    };
  }

  if (name === "get_rewrite_result") {
    const attemptId = readRequiredString(args, "attempt_id");
    const result = await context.backend.poll(attemptId, { apiKey: context.apiKey });

    return {
      status: result.status,
      ...(result.rewritten ? { rewritten: result.rewritten } : {}),
      ...(result.changes ? { changes: result.changes } : {}),
    };
  }

  throw new Error(`Unknown MCP tool: ${name}`);
}

function toRewriteRequest(args: unknown): RewriteRequest {
  return { draft: readRequiredString(args, "draft") };
}

async function pollUntilTerminal(
  attemptId: string,
  context: CallToolContext,
): Promise<Awaited<ReturnType<RewriteBackend["poll"]>>> {
  const maxAttempts = normalizeCount(
    context.poll?.maxAttempts,
    DEFAULT_MAX_ATTEMPTS,
  );
  const maxDelayMs = normalizeDelay(context.poll?.maxDelayMs, DEFAULT_MAX_DELAY_MS);
  let delayMs = normalizeDelay(
    context.poll?.initialDelayMs,
    DEFAULT_INITIAL_DELAY_MS,
  );

  for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
    const result = await context.backend.poll(attemptId, { apiKey: context.apiKey });
    if (result.status !== "working") {
      return result;
    }

    if (attempt === maxAttempts - 1) {
      break;
    }

    await sleep(delayMs);
    delayMs = Math.min(delayMs * 2, maxDelayMs);
  }

  return { status: "working" };
}

function readRequiredString(args: unknown, key: string): string {
  const value = readStringProperty(args, key);
  if (!value) {
    throw new Error(`${key} is required.`);
  }

  return value;
}

function readStringProperty(args: unknown, key: string): string | undefined {
  if (typeof args !== "object" || args === null || !(key in args)) {
    return undefined;
  }

  const value = (args as Record<string, unknown>)[key];
  return typeof value === "string" && value.trim().length > 0
    ? value.trim()
    : undefined;
}

function normalizeCount(value: number | undefined, fallback: number): number {
  if (typeof value !== "number" || !Number.isFinite(value) || value < 1) {
    return fallback;
  }

  return Math.floor(value);
}

function normalizeDelay(value: number | undefined, fallback: number): number {
  if (typeof value !== "number" || !Number.isFinite(value) || value < 0) {
    return fallback;
  }

  return value;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}
