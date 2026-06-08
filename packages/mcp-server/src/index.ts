import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";

import { HttpRewriteBackend } from "./backend/RewriteBackend.js";
import {
  readServerConfig,
  type ConfigEnv,
  type McpServerConfig,
} from "./config.js";
import { callTool, listTools, type ToolOutput } from "./tools/index.js";

export interface CreateServerOptions {
  name?: string;
  version?: string;
  config?: McpServerConfig;
  env?: ConfigEnv;
}

const serverConfigs = new WeakMap<Server, McpServerConfig>();

export function createServer(options: CreateServerOptions = {}): Server {
  const config = options.config ?? readServerConfig(options.env);
  const server = new Server(
    {
      name: options.name ?? "replyinmyvoice",
      version: options.version ?? "0.0.1",
    },
    {
      capabilities: { tools: {} },
    },
  );

  serverConfigs.set(server, config);
  server.setRequestHandler(ListToolsRequestSchema, () => ({
    tools: listTools(),
  }));
  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const output = await callTool(request.params.name, request.params.arguments, {
      apiKey: config.apiKey,
      backend: new HttpRewriteBackend(config.baseUrl),
    });

    return toCallToolResult(output);
  });

  return server;
}

export function getServerConfig(server: Server): McpServerConfig {
  const config = serverConfigs.get(server);

  if (!config) {
    throw new Error("MCP server config has not been initialized.");
  }

  return config;
}

export async function runStdio(env: ConfigEnv = process.env): Promise<void> {
  const server = createServer({ env });
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

function toCallToolResult(output: ToolOutput) {
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

function toToolText(output: ToolOutput): string {
  if ("status" in output) {
    return output.rewritten ?? `Rewrite status: ${output.status}`;
  }

  return output.rewritten;
}
