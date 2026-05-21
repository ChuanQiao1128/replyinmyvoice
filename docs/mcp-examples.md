# MCP Server Usage Examples

This document shows how to use `@replyinmyvoice/mcp-server` with popular LLM tools. The server exposes three tools:

- `rewrite_email` — rewrite a draft in a chosen tone/scenario
- `analyze_signal` — score a draft for clarity, warmth, and tone match
- `list_scenarios` — list supported scenario presets

Set `REPLY_IN_MY_VOICE_API_KEY` in the environment before connecting. Get a key from https://replyinmyvoice.com/app/api-keys.

## 1. Claude Desktop

Add the server to `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows):

```json
{
  "mcpServers": {
    "replyinmyvoice": {
      "command": "npx",
      "args": ["-y", "@replyinmyvoice/mcp-server"],
      "env": {
        "REPLY_IN_MY_VOICE_API_KEY": "rimv_live_xxx"
      }
    }
  }
}
```

Restart Claude Desktop. You can now ask:

> "I drafted an apology email about a late invoice. Can you rewrite it with a calmer, more accountable tone?"
> *(paste draft)*

Claude will call `rewrite_email` with `scenario: "apology"` and return three candidates with their signal scores.

## 2. Codex CLI

Add the server to `~/.codex/config.toml`:

```toml
[mcp_servers.replyinmyvoice]
command = "npx"
args = ["-y", "@replyinmyvoice/mcp-server"]

[mcp_servers.replyinmyvoice.env]
REPLY_IN_MY_VOICE_API_KEY = "rimv_live_xxx"
```

In a Codex session:

> "Here's my draft reply to the client about the project delay. Rewrite it in a calmer, more accountable tone — keep it under 120 words."
> *(paste draft)*

Codex will route through `rewrite_email` with `scenario: "delay-notification"` and `tone: "calm"`.

## 3. Cursor / Continue.dev (IDE)

Add to `.continue/config.json`:

```json
{
  "mcpServers": [
    {
      "name": "replyinmyvoice",
      "command": "npx",
      "args": ["-y", "@replyinmyvoice/mcp-server"],
      "env": {
        "REPLY_IN_MY_VOICE_API_KEY": "rimv_live_xxx"
      }
    }
  ]
}
```

Select an email draft in your editor, open the command palette, choose **Continue: Send to MCP tool → rewrite_email**, and Continue will inline-replace the selection with the rewritten version.

## 4. Programmatic Node.js client

```ts
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

const transport = new StdioClientTransport({
  command: "npx",
  args: ["-y", "@replyinmyvoice/mcp-server"],
  env: {
    ...process.env,
    REPLY_IN_MY_VOICE_API_KEY: process.env.REPLY_IN_MY_VOICE_API_KEY!,
  },
});

const client = new Client({ name: "rimv-demo", version: "0.1.0" }, { capabilities: {} });
await client.connect(transport);

const rewrite = await client.callTool({
  name: "rewrite_email",
  arguments: {
    draft: "Sorry the invoice is late, swamped this week.",
    scenario: "apology",
    tone: "warm",
  },
});
console.log(JSON.stringify(rewrite, null, 2));

const signal = await client.callTool({
  name: "analyze_signal",
  arguments: { draft: "Sorry the invoice is late, swamped this week." },
});
console.log(JSON.stringify(signal, null, 2));

await client.close();
```

## Troubleshooting

- **401 Unauthorized** — `REPLY_IN_MY_VOICE_API_KEY` missing or revoked. Re-issue at `/app/api-keys`.
- **429 Too Many Requests** — per-key rate limit hit (default 60/min). Wait or upgrade tier.
- **Unsupported scenario** — call `list_scenarios` to see valid values. Closest matches are returned in the error body.
