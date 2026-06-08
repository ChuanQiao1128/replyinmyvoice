# REMOTE: remote Streamable HTTP MCP endpoint at /api/mcp

## Context
A remote MCP server as a Next route on the existing Worker, reusing the CORE tool core. Read: `plans/mcp-productization/REQUIREMENT.md` (Proposed Architecture + Async rewrite job contract + State/Error remote-auth), `app/api/v1/rewrite/route.ts` (the proxy pattern), `lib/azure-api.ts`, and `packages/mcp-server/src/tools` (from CORE). Depends on **CORE**.

## Constraints
- **Stateless** `StreamableHTTPServerTransport` (`enableJsonResponse`); no Durable Objects, no sessions.
- `export const dynamic = "force-dynamic"` (Node-compat runtime, like the other v1 routes).
- API key from `Authorization: Bearer`; inject into the shared core's `RewriteBackend`.
- The root Next project may not have `@modelcontextprotocol/sdk` installed — if so, add it as a dependency (required for this route). No secret values in source.
- No OAuth. Banned terms never appear.

## Changes required (scope: `app/api/mcp/**`, `package.json` if the SDK dep is needed, `lib/**` if a small helper is needed, `tests/unit/mcp-remote.test.ts`)
1. `app/api/mcp/route.ts` — `POST` handler: stateless `StreamableHTTPServerTransport`, register `listTools`/`callTool` from the shared core with a backend targeting the existing rewrite API; `apiKey` from the `Authorization: Bearer` header. Missing/invalid key → `401` + `WWW-Authenticate: Bearer`.
2. During rewrite polling, emit MCP **progress notifications** (keepalive); cap polling at ~50s; on cap return `{status:'working', attempt_id}` instructing the caller to use `get_rewrite_result`.
3. Validate `Origin` (DNS-rebinding guard); only accept `POST` (+ the transport's required methods).
4. `tests/unit/mcp-remote.test.ts`: a request with no `Authorization` → `401` + `WWW-Authenticate`.

## Acceptance (machine-checkable, in worktree)
- `npm run typecheck` exits 0
- `npm run test` exits 0
- `npm run build` exits 0
- `grep -RniE "humanizer|bypass|undetect|detector|evade" app/api/mcp` prints nothing

## DO NOT
- Do NOT make it stateful or add Durable Objects. Do NOT implement OAuth. Do NOT add tools beyond the two. Do NOT touch the C# backend. Do NOT push, open a PR, or touch `main`.
