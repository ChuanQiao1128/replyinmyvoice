# QAE2E: end-to-end harness (stdio + remote) against a real test key

## Context
Prove both transports return the FINAL rewritten text and decrement credits. Read: `packages/mcp-server` (stdio, from STDIO), `app/api/mcp` (remote, from REMOTE), `app/api/v1/usage/route.ts`. Depends on **STDIO** and **REMOTE**.

## Constraints
- Uses a real `rmv_test_` key from env (`REPLY_IN_MY_VOICE_API_KEY`); a `--dry-run` default that sends NOTHING when no key is present. Never hardcode a key. No secret values in source.
- Banned terms never appear.

## Changes required (scope: `scripts/mcp-e2e/**`)
1. `scripts/mcp-e2e/run.mjs` (Node, global `fetch`) — spawn the stdio server + a programmatic MCP client; call `rewrite_email`; assert the response carries `rewritten` text (NOT an attempt id). Optionally exercise the remote `/api/mcp` path the same way. Read `/api/v1/usage` to assert the credit decrement. `--dry-run` (or absence of the key) exits 0 and performs no network I/O.
2. A short `scripts/mcp-e2e/README.md` documenting how to run it with a key.

## Acceptance (machine-checkable, in worktree)
- `node scripts/mcp-e2e/run.mjs --dry-run` exits 0 and performs no network request
- `npm run typecheck` exits 0
- `npm run lint` exits 0
- `grep -RniE "humanizer|bypass|undetect|detector|evade" scripts/mcp-e2e` prints nothing

## DO NOT
- Do NOT embed a real key. Do NOT call live endpoints without the `--dry-run`/no-key guard. Do NOT touch the C# backend. Do NOT push, open a PR, or touch `main`.
