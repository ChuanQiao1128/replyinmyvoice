import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";

export interface CreateServerOptions {
  name?: string;
  version?: string;
}

export function createServer(options: CreateServerOptions = {}): Server {
  return new Server(
    {
      name: options.name ?? "replyinmyvoice",
      version: options.version ?? "0.0.1",
    },
    {
      capabilities: { tools: {} },
    },
  );
}

export async function runStdio(): Promise<void> {
  const server = createServer();
  const transport = new StdioServerTransport();
  await server.connect(transport);
}
