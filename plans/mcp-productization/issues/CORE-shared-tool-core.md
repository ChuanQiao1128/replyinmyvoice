# CORE: shared transport-agnostic MCP tool core + RewriteBackend adapter

## Context
`packages/mcp-server` is a skeleton — `src/index.ts` `createServer` registers no tool handlers and `src/tools/` is missing. A full but **uncommitted** reference impl exists at `packages/mcp-server/dist/tools/index.js`: use it as a GUIDE only. It invents a `scenario` preset enum and a `/api/v1/analyze-signal` call **the engine does not expose** — do NOT carry those over. Read first: `packages/mcp-server/src/index.ts`, `packages/mcp-server/src/config.ts`, `packages/mcp-server/dist/tools/index.js`, `plans/mcp-productization/REQUIREMENT.md` (API and Job Contracts + Async rewrite job contract), and the real request shape `backend-dotnet/src/ReplyInMyVoice.Domain/Contracts/RewriteRequest.cs`.

## Constraints
- TypeScript, ESM, Node ≥18, official `@modelcontextprotocol/sdk` already in the package. No new deps.
- Transport-agnostic: the core must NOT import any stdio/http transport. `apiKey` + `baseUrl` are injected by the caller.
- Public output is the MINIMAL stable contract — no `signal_score`, `strategy`, `scenario`, or naturalness % in any tool output (replaceability: the engine behind `/api/v1/rewrite` must stay swappable).
- Banned terms (`humanizer|bypass|undetect|detector|evade`) must never appear in source/comments/names.

## Changes required (scope: `packages/mcp-server/src/**`, `tests/unit/mcp-core.test.ts`)
1. `src/backend/RewriteBackend.ts` — interface `RewriteBackend { submit(req,{apiKey}): Promise<{attemptId}>; poll(attemptId,{apiKey}): Promise<{status:'working'|'succeeded'|'failed', rewritten?, changes?}> }` + default `HttpRewriteBackend(baseUrl)` calling `POST /api/v1/rewrite` (202 → `{id}`) and `GET /api/v1/rewrite/{id}`, Bearer `apiKey`, header `Idempotency-Key` = sha256(canonical request).
2. `src/tools/index.ts` — `listTools()` returns EXACTLY two: `rewrite_email` (input `{draft, context?, tone?}`; output `{rewritten, changes?, attempt_id}`) and `get_rewrite_result` (input `{attempt_id}`; output `{status, rewritten?, changes?}`). `callTool(name, args, {backend, apiKey})`: `rewrite_email` submits then polls to terminal (bounded backoff) and returns FINAL text; `get_rewrite_result` polls once.
3. Map the external input to the engine's real `RewriteRequest` fields, verified against `RewriteRequest.cs` (`draft`→`roughDraftReply`; `context`→the appropriate field(s) such as `messageToReplyTo`/`whatHappened`/`factsToPreserve`; `tone` passthrough, default `warm`). Do NOT invent fields the engine ignores.
4. `tests/unit/mcp-core.test.ts` (root vitest): against a mock `RewriteBackend`, `rewrite_email` returns `rewritten` text (NOT an attempt id); the output object has no `signal_score`/`strategy`/`scenario` keys; `listTools()` returns exactly `rewrite_email` + `get_rewrite_result`; idempotency key is stable for identical input.

## Acceptance (machine-checkable, in worktree)
- `npm --prefix packages/mcp-server run build` exits 0
- `npm run typecheck` exits 0
- `npm run test` exits 0
- `grep -RniE "humanizer|bypass|undetect|detector|evade" packages/mcp-server/src tests/unit/mcp-core.test.ts` prints nothing

## DO NOT
- Do NOT add `analyze_signal` or `list_scenarios`. Do NOT expose signal/strategy/scenario in output. Do NOT invent a `scenario` input enum. Do NOT call `/api/v1/analyze-signal` (it does not exist). Do NOT touch the C# backend. Do NOT push, open a PR, or touch `main`.
