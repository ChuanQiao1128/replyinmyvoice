# STDIO: wire ListTools/CallTool into the stdio server

## Context
The stdio entry exists but `createServer` (`packages/mcp-server/src/index.ts`) registers no request handlers, so the server exposes zero tools. The tool core lands in CORE. Read: `packages/mcp-server/src/index.ts`, `src/bin.ts`, `src/config.ts`, and `src/tools/index.ts` (from CORE). Depends on **CORE**.

## Constraints
- Official `@modelcontextprotocol/sdk` stdio transport (`StdioServerTransport`).
- API key from `REPLY_IN_MY_VOICE_API_KEY` env via `src/config.ts` (already reads it); base URL from config.
- No new deps. Banned terms never appear.

## Changes required (scope: `packages/mcp-server/src/index.ts`, `src/bin.ts`, `tests/unit/mcp-stdio.test.ts`)
1. In `createServer`, register `ListToolsRequestSchema` → `listTools()` and `CallToolRequestSchema` → `callTool(name, args, { backend: new HttpRewriteBackend(config.baseUrl), apiKey: config.apiKey })`.
2. Ensure `runStdio` connects this server over `StdioServerTransport`; `bin.ts` behavior unchanged.
3. `tests/unit/mcp-stdio.test.ts` (root vitest): drive the registered handlers in-process — `tools/list` returns `rewrite_email` + `get_rewrite_result`; `tools/call rewrite_email` against a mock backend returns final text.

## Acceptance (machine-checkable, in worktree)
- `npm --prefix packages/mcp-server run build` exits 0
- `npm run typecheck` exits 0
- `npm run test` exits 0
- `grep -RniE "humanizer|bypass|undetect|detector|evade" packages/mcp-server/src tests/unit/mcp-stdio.test.ts` prints nothing

## DO NOT
- Do NOT add tools beyond the two. Do NOT add a non-stdio transport here (remote is a separate issue). Do NOT touch the C# backend. Do NOT push, open a PR, or touch `main`.
