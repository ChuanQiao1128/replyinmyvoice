import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";

import {
  readServerConfig,
  type ConfigEnv,
  type McpServerConfig,
} from "./config.js";

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
