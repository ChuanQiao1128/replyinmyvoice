# @replyinmyvoice/mcp-server

MCP server that exposes the Reply In My Voice rewrite tools to any MCP-compatible LLM host — Claude Desktop, Claude Code, Codex, Cursor, Continue.dev, and others.

## Install

    npx @replyinmyvoice/mcp-server

Or globally:

    npm install -g @replyinmyvoice/mcp-server

Node 18+ required.

## Configure your API key

Set `REPLY_IN_MY_VOICE_API_KEY` in the environment. Get a key from <https://replyinmyvoice.com/app/api-keys> after signing in.

## Host configuration

### Claude Desktop

Edit `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows):

```json
{
  "mcpServers": {
    "replyinmyvoice": {
      "command": "npx",
      "args": ["-y", "@replyinmyvoice/mcp-server"],
      "env": { "REPLY_IN_MY_VOICE_API_KEY": "rmv_live_xxx" }
    }
  }
}
```

### Codex

Edit `~/.codex/config.toml`:

```toml
[mcp_servers.replyinmyvoice]
command = "npx"
args = ["-y", "@replyinmyvoice/mcp-server"]
env = { REPLY_IN_MY_VOICE_API_KEY = "rmv_live_xxx" }
```

### Cursor

Edit `~/.cursor/mcp.json` (or use Settings → MCP):

```json
{
  "mcpServers": {
    "replyinmyvoice": {
      "command": "npx",
      "args": ["-y", "@replyinmyvoice/mcp-server"],
      "env": { "REPLY_IN_MY_VOICE_API_KEY": "rmv_live_xxx" }
    }
  }
}
```

### Continue.dev

Edit `~/.continue/config.yaml`:

```yaml
mcpServers:
  - name: replyinmyvoice
    command: npx
    args: ["-y", "@replyinmyvoice/mcp-server"]
    env:
      REPLY_IN_MY_VOICE_API_KEY: rmv_live_xxx
```

## Tools

The server exposes three tools (full implementations land in M9-002; the interface below is stable):

### `rewrite_email`

Rewrites a draft so it sounds more natural while preserving meaning and facts.

| Field      | Type                                                                                  | Required | Notes |
|------------|---------------------------------------------------------------------------------------|----------|-------|
| `draft`    | string                                                                                | yes      | Up to 5000 chars combined with context. |
| `scenario` | enum: `customer-support` \| `sales-outreach` \| `personal` \| `leadership` \| `recruitment` | no       | Activates scenario-specific guardrails. |
| `tone`     | enum: `warm` \| `neutral` \| `direct` \| `enthusiastic`                               | no       | Default: `neutral`. |

Returns: `{ rewritten: string, signal_score: number, diagnosis_tags: string[] }`

### `analyze_signal`

Runs the naturalness check on existing text without rewriting it.

| Field  | Type   | Required |
|--------|--------|----------|
| `text` | string | yes      |

Returns: `{ signal_score: number, diagnosis_tags: string[], suggestions: string[] }`

### `list_scenarios`

Returns the available scenario presets and their guardrails. Takes no arguments.

Returns: `{ scenarios: Array<{ id: string, label: string, guardrails: string }> }`

## Example tool calls

```json
{ "tool": "rewrite_email", "arguments": { "draft": "Dear Customer, we regret to inform you that...", "scenario": "customer-support", "tone": "warm" } }
```

```json
{ "tool": "analyze_signal", "arguments": { "text": "Per my last email, kindly find attached the requested documentation..." } }
```

```json
{ "tool": "list_scenarios", "arguments": {} }
```

## Status

This package is in active development (0.x). The skeleton ships first; the three tools above arrive in M9-002.

## Links

- Web app — <https://replyinmyvoice.com>
- API docs — <https://replyinmyvoice.com/developers>
- Repository — <https://github.com/ChuanQiao1128/replyinmyvoice>
- Issues — <https://github.com/ChuanQiao1128/replyinmyvoice/issues>

## License

MIT — see `LICENSE` in the repo root.
