# @replyinmyvoice/mcp-server

MCP server for Reply In My Voice. It gives MCP-compatible clients a small, stable interface for rewriting email replies while preserving the supplied facts and context.

The package can run locally over stdio with `npx`, or clients can connect to the hosted HTTP endpoint.

## Requirements

- Node.js 18 or newer for local stdio use.
- A Reply In My Voice API key from <https://replyinmyvoice.com/developers/keys>.

## Local stdio with npx

Run the server directly:

```sh
REPLY_IN_MY_VOICE_API_KEY=rmv_live_xxx npx -y @replyinmyvoice/mcp-server
```

The server uses `https://replyinmyvoice.com` by default. For development or staging, set `REPLY_IN_MY_VOICE_BASE_URL`.

### Claude Desktop

Edit `~/Library/Application Support/Claude/claude_desktop_config.json` on macOS, or `%APPDATA%\Claude\claude_desktop_config.json` on Windows:

```json
{
  "mcpServers": {
    "replyinmyvoice": {
      "command": "npx",
      "args": ["-y", "@replyinmyvoice/mcp-server"],
      "env": {
        "REPLY_IN_MY_VOICE_API_KEY": "rmv_live_xxx"
      }
    }
  }
}
```

### Codex

Add a local stdio server:

```toml
[mcp_servers.replyinmyvoice]
command = "npx"
args = ["-y", "@replyinmyvoice/mcp-server"]
env = { REPLY_IN_MY_VOICE_API_KEY = "rmv_live_xxx" }
```

## Remote HTTP

Use the hosted endpoint when your MCP client supports HTTP transport and custom authorization headers.

### Claude Code

```sh
claude mcp add --transport http replyinmyvoice https://replyinmyvoice.com/api/mcp \
  --header "Authorization: Bearer rmv_live_xxx"
```

### Codex

```sh
codex mcp add --url https://replyinmyvoice.com/api/mcp replyinmyvoice \
  --header "Authorization: Bearer rmv_live_xxx"
```

## Tools

This server exposes exactly two tools.

### `rewrite_email`

Rewrites a draft email reply and returns the finished text.

Input:

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `draft` | string | yes | Draft reply to rewrite. Use 10 to 2400 characters and keep it under about 300 words. |
| `context` | string | no | Original message, thread summary, or facts the reply should respect. |
| `tone` | `"warm"` or `"direct"` | no | Defaults to `warm`. |

Output:

```json
{
  "rewritten": "Thanks for sending this through...",
  "changes": ["Made the reply clearer and more natural."],
  "attempt_id": "rw_..."
}
```

`rewrite_email` submits the request, polls the Reply In My Voice API, and returns the final rewritten reply when the job succeeds.

### `get_rewrite_result`

Checks the status of a rewrite attempt.

Input:

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `attempt_id` | string | yes | Attempt id returned by `rewrite_email`. |

Output:

```json
{
  "status": "succeeded",
  "rewritten": "Thanks for sending this through...",
  "changes": ["Made the reply clearer and more natural."]
}
```

`status` is one of `working`, `succeeded`, or `failed`.

## Billing

`rewrite_email` uses one rewrite credit when a reply is successfully delivered. `get_rewrite_result` checks an existing attempt and does not use another credit.

If credits are exhausted, visit <https://replyinmyvoice.com/developers> to manage API access.

## Links

- Web app: <https://replyinmyvoice.com>
- Developer keys: <https://replyinmyvoice.com/developers/keys>
- Developer docs: <https://replyinmyvoice.com/developers>
- Repository: <https://github.com/ChuanQiao1128/replyinmyvoice>
- Issues: <https://github.com/ChuanQiao1128/replyinmyvoice/issues>

## License

MIT. See `LICENSE`.
