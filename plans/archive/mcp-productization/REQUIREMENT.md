# MCP Productization — Implementation Specification

> **Status:** DRAFT for owner review (v2 — rewritten around the "replaceable rewrite module" decision). Do **not** start the Dynamic Delivery Workflow until the **Open Questions** are resolved.
> **Author:** Claude Code (supervisor), 2026-06-08.
> **One-line goal:** Expose the rewrite capability as a **replaceable module** behind a stable MCP contract that an external developer can adopt in one step — shipped fully automatically to `main`, frontend included.
> **Source-of-truth precedence:** `AGENTS.md` > this spec. If they disagree, `AGENTS.md` wins.

---

## Context

The rewrite product already exposes a Bearer-authenticated HTTP API and a credit-based billing system. A partial MCP package exists but is a non-functional skeleton. The owner has authorized making MCP a **live, self-serve product** (overrides the prior "MCP is a one-line future card" decision for `/developers`). The owner's framing: **the rewrite logic will change over time, so package it as a replaceable module and put MCP in front of it.**

Key source facts (each with a path; no secret values quoted):

- **API key auth is production-grade.** `rmv_live_`/`rmv_test_`, 32 random bytes Base62, SHA256 + `API_KEY_PEPPER` hash. `ApiKeyService.cs`; validated in `ApiKeyAuthResolver.cs`; entity `ApiKey.cs`; created via `POST /api/keys` (`ApiKeyHttpFunctions.cs`); UI `app/developers/keys/page.tsx`.
- **Billing is production-grade and live on Stripe.** Reservation model (`RewriteCredit` + `UsagePeriod` + `UsageReservation`) in `QuotaService.cs`. Exhaustion → `402 quota_exhausted`. Idempotency via `(UserId, IdempotencyKey)`. Grant in `StripeEventService.cs`. API path enforces `HasPaidApiEntitlementAsync()` (free users cannot call the API). `pro_api` (NZ$19.90/90) is the API plan.
- **`/api/v1/rewrite` is asynchronous.** Success → `202 Accepted` + `Location: /api/v1/rewrite/{id}`, body `{ id }` — `V1RewriteHttpFunctions.cs:164`. Final text fetched by polling `GET /api/v1/rewrite/{id}` (queue + worker). Next proxy `app/api/v1/rewrite/route.ts` forwards `Authorization` + `Idempotency-Key`.
- **The rewrite engine is ALREADY a replaceable module.** It sits behind `IRewriteProvider` (`backend-dotnet/src/ReplyInMyVoice.Infrastructure/Providers/IRewriteProvider.cs`); prod impl `FactReconstructRewriteProvider`, test impl `DeterministicRewriteProvider`; swapped at one DI line in `ServiceCollectionExtensions.cs:149-190`; the worker (`RewriteJobProcessor.cs`) calls it through the interface. The LLM sits behind `IRewriteModelClient` (OpenAI-compatible, base URL/model from env — swappable to any compatible API incl. the Claude API); the signal sits behind `IWritingSignalClient`. **Swapping the whole rewrite logic = new `IRewriteProvider` impl + one DI line; HTTP/worker/DB unchanged.**
- **The engine's pipeline (source of truth for the contract).** `RewriteJobProcessor` → `IRewriteProvider.RewriteAsync` → (pre) `RewriteInputAnalyzer.Analyze` (scenario + risk + **strategy routing**, internal only) + `FactLedgerExtractor` + measure draft signal once → adaptive loop [generate candidate → structure gate → fact gate → measure candidate Sapling signal → score] → return lowest-signal usable candidate. Success payload `RewriteProviderSuccessPayload` = `{ rewrittenText, changeSummary[], riskNotes[], naturalness?{draft%,rewrite%,label}, optimization?{strategy,scenario,attemptsUsed,...} }` (`FactReconstructRewriteProvider.cs:416-451`). True request fields: `roughDraftReply, messageToReplyTo, audience, purpose, whatHappened, factsToPreserve, tone` (`RewriteRequest.cs`).
- **The MCP package is a skeleton.** `packages/mcp-server` (`@replyinmyvoice/mcp-server` v0.0.1, official SDK, stdio, bin `replyinmyvoice-mcp`). `createServer` registers **no tool handlers**; `src/tools/index.ts` is **missing**. A full impl exists only as `dist/tools/index.js` — M9-002 was implemented + validated + `ready_to_commit` but **never committed** (`plans/codex-exec-M9-002.log`; git shows M9-001/M9-004/#230 only). The `dist` artifact also invents a `scenario` preset enum + a `/api/v1/analyze-signal` call **the engine does not actually expose** — do not trust it as the contract.
- **Remote MCP hosting target.** Worker `replyinmyvoice-app`, `nodejs_compat`, OpenNext. v1 routes are `force-dynamic` (Node-compat, not edge 50 ms cap). `LAUNCH_CONFIRMED=true`.
- **Auth provider = Microsoft Entra External ID.** **Entra does not support OAuth Dynamic Client Registration (DCR)** (external research) → OAuth needs pre-registered clients and is deferred; first version is static Bearer header.

---

## Goals (Definition of Done — every item demoable)

An external developer, without touching the repo, can:

1. Open `replyinmyvoice.com/developers`, understand the MCP offering and how to connect.
2. Sign in → create a `rmv_live_` key at `/developers/keys`, seeing their credit balance.
3. Copy a snippet into Claude Code / Codex / Claude Desktop / Cursor — for **local** (`npx`, stdio) **or** **remote** (HTTP URL + Bearer header).
4. Invoke `rewrite_email` and receive the **final rewritten text** (not an attempt id) — async polling solved.
5. Have credits deducted from purchased credits; on exhaustion get a clear error with a top-up link.
6. Rely on `npx @replyinmyvoice/mcp-server` resolving from the **public npm registry** (publish done).

**Architecture goal (load-bearing):**

7. **The rewrite capability behind MCP is a replaceable module.** Swapping the engine (new prompt, new pipeline, new provider, even a non-HTTP backend) requires **no change to the MCP tool contract and no change for any connected client.** Guaranteed by two stable seams: the `IRewriteProvider` interface (backend) and the `RewriteBackend` adapter inside the MCP package (client side).

---

## Non-Goals (explicitly out of scope this wave)

- **Exposing internal engine artifacts in the external contract** — no `signal_score` / AI-likeness %, no `strategy`, no `scenario`, no per-sentence scores in any MCP tool output. These are current-implementation details; exposing them would weld the public contract to today's engine and break Goal 7.
- **A standalone `analyze_signal` tool** and **a `list_scenarios` tool.** Rationale in *Replaceability & the two "analyses"* below. (Internal analysis stays inside the engine, untouched.)
- OAuth 2.1 / DCR / pre-registered MCP clients (Entra lacks DCR) — Bearer header only.
- Marketplace listing (Smithery / Glama / MCPize).
- Weighted / sub-credit / per-token metering — flat **1 credit per billable call**.
- Any change to the web `/app` flow, the rewrite engine itself, Stripe prices, `STRIPE_WEBHOOK_SECRET`, `LAUNCH_CONFIRMED`, or quota semantics. **This wave does not touch the C# backend** (the `/api/v1/rewrite` contract already suffices).
- Streaming partial output token-by-token.
- Initiating a real paid purchase. The commercial path (create key → buy credits → call tools) is wired and usable end-to-end, but the **first live transaction is the owner's manual test**; automation never triggers a real Stripe charge (`AGENTS.md` M7-001).

---

## Replaceability & the two "analyses" (the key design decision)

There are **two distinct things** both loosely called "analysis":

1. **Engine-internal analysis** — `RewriteInputAnalyzer` (scenario/strategy routing, pre-rewrite) + the Sapling signal gate (post-rewrite quality loop) + fact/structure gates. This is part of the rewrite engine, **always runs, is never removed**, and is already replaceable behind `IRewriteProvider`. Nothing in this wave changes it.
2. **A public `analyze_signal` tool** — exposing the Sapling AI-likeness score to external developers as its own MCP tool. **This is what we are NOT building.**

Why the tool is not built:
- **Redundant** — the score is an internal gate; `rewrite_email` already returns the finished result.
- **Wrong positioning** — a standalone "signal score" tool is an AI-likeness detector; the owner stopped chasing AI detection and never promises "passes detection". Shipping it as a product surface contradicts that.
- **Unreliable to sell** — the Sapling signal is bimodal/outlier-dominated and fails on short/pleasantry emails; it is internal scaffolding, not a product-grade score.
- **Anti-replaceable** — it leaks a current-engine artifact into the public contract. Swap Sapling out and the tool breaks. Keeping the contract to "draft → natural rewrite" is what makes the engine swappable (Goal 7).

**Consequence:** the public surface is the smallest stable contract — *give a draft, get a better reply* — and the engine underneath can change freely.

---

## Current System

```
Today:
  LLM host ──(nothing usable)──> @replyinmyvoice/mcp-server   [skeleton: 0 tools wired]
  Developer ── Bearer rmv_live_ ──> POST /api/v1/rewrite (Next proxy) ──> Azure Functions
                                       └─ 202 + Location ─> poll GET /api/v1/rewrite/{id} ─> rewritten text
                                                              (engine behind IRewriteProvider — already swappable)
  Web /app  ──> POST /api/rewrite  ──> same QuotaService reservation pool
  Keys UI   ──> /developers/keys ──> POST /api/keys (Entra-authenticated)
```

Reusable as-is (no changes): `ApiKeyAuthResolver`, `QuotaService`, Stripe grant, `IRewriteProvider` + the whole engine, `/api/v1/rewrite` (+ `/{id}`), `/api/v1/usage`, `/developers/keys`.

---

## Proposed Architecture

**Principle:** one shared tool core; two transports; a client-side replaceable backend adapter; one new docs page. No backend changes. stdio and remote differ **only** in transport + where the API key comes from.

```
packages/mcp-server/src/
  backend/              <- NEW replaceable adapter (the client-side seam for Goal 7)
    RewriteBackend       interface { submit(req) -> {attemptId}; poll(attemptId) -> {status, rewritten?, changes?} }
    HttpRewriteBackend   default impl -> POST /api/v1/rewrite + GET /api/v1/rewrite/{id}
                          (swap this one class to point MCP at a different engine/transport later)
  tools/                <- NEW shared core (pure, transport-agnostic)
    listTools()          -> tool definitions (rewrite_email, get_rewrite_result)
    callTool(name, args, {backend, apiKey}) -> ToolResult   (submit + poll-to-final inside rewrite_email)
  index.ts              <- createServer() registers ListTools/CallTool -> tools core   (FIX wiring)
  bin.ts                <- stdio entry (npm package); key from REPLY_IN_MY_VOICE_API_KEY env

app/api/mcp/route.ts    <- NEW remote entry: StreamableHTTPServerTransport (stateless),
                            key from Authorization: Bearer header, reuses the SAME tools core,
                            MCP progress-notification keepalive during rewrite polling

app/developers/mcp/page.tsx   <- NEW connect page (stdio + remote, 4 hosts, key, billing, errors)
```

**Two stable seams guarantee replaceability:**
- **Backend seam:** `IRewriteProvider` (C#) — already exists; swap the engine there.
- **Client seam:** `RewriteBackend` (TS, in the MCP package) — new; lets the MCP package itself retarget a different backend without touching tool definitions or transports.

**Hosting decision (lightweight architecture/cost review):**

| Option | Deploy units | Durable Objects | Reuses auth/secrets | Cost | Verdict |
|---|---|---|---|---|---|
| **A. Next route in existing Worker** | 1 (existing) | No (stateless HTTP) | Yes | ≈ $0 | **CHOSEN** |
| B. Standalone Worker + Agents SDK | 2 | Optional | Duplicate wiring | ≈ $0 | Rejected: extra ops, no benefit |
| C. Azure Functions (C#) MCP host | existing | n/a | Yes | ≈ $0 | Rejected: weaker C# MCP SDK; TS core already exists |

Chosen = **A** (reuses `replyinmyvoice-app` deploy/secrets/TS core; stateless Streamable HTTP fits Workers).

---

## Data Model

**No new tables; no EF migration.** Reuse `ApiKey`, `RewriteCredit`, `UsagePeriod`, `UsageReservation`, `RewriteAttempt`, `ApiKeyUsage`. The whole wave is frontend + npm package; the billing/quota path used is exactly today's `/api/v1/rewrite` path.

> Per `data-module-review`: if any issue proposes a schema change, flag it for review — none is expected.

---

## API and Job Contracts

### MCP tools (stable, minimal — stdio and remote identical)

| Tool | Input | Output | Backend |
|---|---|---|---|
| `rewrite_email` | `{ draft: string (10..2400 chars / ≤300 words), context?: string, tone?: "warm"\|"direct" }` | `{ rewritten: string, changes?: string[], attempt_id: string }` | submit + poll `/api/v1/rewrite` via `RewriteBackend` |
| `get_rewrite_result` | `{ attempt_id: string }` | `{ status: "working"\|"succeeded"\|"failed", rewritten?: string, changes?: string[] }` | `GET /api/v1/rewrite/{id}` |

- **Input MUST map to the engine's real `RewriteRequest` fields** (`roughDraftReply, messageToReplyTo, audience, purpose, whatHappened, factsToPreserve, tone`). The external surface is simplified (`draft`→`roughDraftReply`; `context`→an appropriate subset such as `messageToReplyTo`/`whatHappened`/`factsToPreserve`; `tone` passthrough). **Do NOT invent fields the engine ignores** (the `dist` artifact's `scenario` preset enum is not a real engine input — `scenario` is auto-routed internally). MCP-CORE must verify the exact mapping against `RewriteRequest.cs`.
- **Output is the minimal stable contract**: finished text + optional human-readable `changes` (from `changeSummary`). **No `signal_score`, `strategy`, `scenario`, or naturalness % in output** (see Replaceability).
- Tool `name`/`description`/`title` are user/LLM-facing → **must pass the banned-term gate** (`humanizer|bypass|undetect|detector|evade`). Positioning: "natural / concise / meaning + facts preserved", never "passes AI detection".

#### Per-tool billing (cost-true metering)

Bill the **call that incurs backend cost**, not every invocation (charging every call would double-charge one rewrite on the async-fallback path).

| Tool | Paid entitlement | Rate limit | Credits | Rationale |
|---|---|---|---|---|
| `rewrite_email` (submit) | required | yes | **1** (reserve → finalize on success) | LLM rewrite cost |
| `get_rewrite_result` | required | yes | **0** | same attempt already billed at submit |

- Billable call supports an **idempotency key** (reuse `(UserId, IdempotencyKey)`) so host/client retries do not double-charge.
- A rewrite that fails at the worker **releases its reservation** — never charge for an undelivered result. (All of this is already implemented server-side; the tool just sets the idempotency key and surfaces errors.)

### Async rewrite job contract (the hard part)

`rewrite_email` MUST return final text, not an attempt id. Inside `callTool` via `RewriteBackend`:

1. `submit()` → `POST /api/v1/rewrite` with `Idempotency-Key` = SHA256(canonical request) → `202 { id }`.
2. `poll()` → `GET /api/v1/rewrite/{id}` with bounded backoff until terminal.
3. **stdio:** poll up to a generous wall-clock budget (e.g. 120 s); return final text.
4. **remote:** poll up to ~50 s while emitting MCP **progress notifications** (keepalive vs client idle timeout `MCP_TOOL_TIMEOUT`). If not terminal by the cap, return `{ status: "working", attempt_id }` and tell the LLM to call `get_rewrite_result`. **[ASSUMPTION, see OQ2]**

---

## State and Error Handling

- **Rewrite lifecycle** (`state-machine-modeling`): `submitted(202) → working → {succeeded | failed}`; tool maps `succeeded → text`, `failed → clear error`, `timeout(remote) → attempt_id + get_rewrite_result`.
- **Quota:** `402 quota_exhausted` → tool returns a message containing the top-up URL (`https://replyinmyvoice.com/developers`). The LLM relays it verbatim — the "agent forwards the top-up prompt" UX.
- **Idempotency:** deterministic key from request content; `409 idempotency_conflict` surfaced clearly.
- **Rate limit:** `429` → retry guidance.
- **Remote no-auth:** `401` + `WWW-Authenticate: Bearer` (groundwork for future OAuth PRM discovery; not implemented now).

> `resilience-test-generation`: cover rewrite timeout, poll-until-terminal, remote-timeout fallback, idempotent retry no-double-charge, quota exhaustion.

---

## Security and Privacy

- Bearer key only; never log full keys (reuse `Last4` masking). No secrets in source; env validated at runtime in the handler.
- Banned-term gate MUST pass over `app components public lib packages/mcp-server` for all new copy (README, page, tool descriptions).
- Remote handler validates `Origin` (DNS-rebinding guard) and requires HTTPS.
- Logs PII-safe; do not log raw draft/context bodies beyond existing observability norms.

---

## Rollout Plan

1. DDW implements each issue on an **integration branch** (never `main`), ≤2 Codex attempts/issue, diff-scoped banned-term gate.
2. **Supervisor (Claude) reviews the full integration branch**: `npm run test`, banned grep, `cf:build` dry-run, e2e harness. (No `dotnet test` needed — backend untouched.)
3. Supervisor **merges integration → `main`** → `cloudflare-worker.yml` (typecheck/test/build → `cf:deploy`) deploys the Worker incl. `/api/mcp`.
4. **npm publish is a gated STOP.** Build to publish-ready (`npm pack`, `npm publish --dry-run` green), then **halt for the owner's `NPM_TOKEN` + explicit "publish" go**; on go run `npm publish --access public`. Single non-unattended step; honors irreversibility of public release.
5. Rollback: `wrangler rollback`; `npm deprecate` (cannot truly unpublish).

---

## Work Breakdown (DDW issues) + Machine-Checkable Acceptance

Ordering: `MCP-CORE` is the prerequisite for stdio + remote. Frontend can proceed in parallel. **No backend issues** (engine already replaceable + `/api/v1/rewrite` suffices).

| ID | Title | Acceptance (machine-checkable) |
|---|---|---|
| **MCP-CORE** | `RewriteBackend` adapter + shared transport-agnostic tool core (`rewrite_email`, `get_rewrite_result`) incl. async submit-and-poll. Verify input mapping against `RewriteRequest.cs`; output carries no signal/strategy/scenario | `npm --prefix packages/mcp-server run build` + unit tests green; `callTool('rewrite_email',…)` against a mock `RewriteBackend` returns `rewritten` text (not an attempt id); output object has no `signal_score`/`strategy`/`scenario` keys; banned-term grep clean |
| **MCP-STDIO** | Wire `ListTools`/`CallTool` in `createServer`; `runStdio` exposes the 2 tools | Programmatic MCP client: `tools/list` returns exactly `rewrite_email` + `get_rewrite_result`; `tools/call rewrite_email` returns final text; tests green |
| **MCP-REMOTE** | `app/api/mcp/route.ts` — stateless `StreamableHTTPServerTransport`, Bearer→key→`RewriteBackend`, progress keepalive during polling, timeout→`get_rewrite_result` | `curl -X POST -H "Authorization: Bearer rmv_test_…" -H "Accept: application/json, text/event-stream" …/api/mcp` completes `initialize` + `tools/list` + `tools/call`; missing key → 401 + `WWW-Authenticate`; `cf:build` succeeds |
| **PKG-RELEASE** | Publish-ready: version 0.0.1→0.1.0 (OQ3), LICENSE, `package.json` (`files`, `publishConfig.access=public`, `repository`, `bin`); rewrite README to the 2-tool contract; fix key link → `/developers/keys`; add remote `claude mcp add --transport http` / `codex mcp add --url` snippets; remove the stale `analyze_signal`/`list_scenarios`/`scenario`-enum copy | `npm publish --dry-run` passes; `npm pack` = dist + README + LICENSE only; README banned-term grep clean; README documents only the 2 real tools |
| **FE-DEVELOPERS-MCP** | New `app/developers/mcp/page.tsx` + entry from `/developers`: what it is, stdio vs remote, 4-host config blocks, "get a key" CTA → `/developers/keys`, billing/quota, 402 error UX | Page 200; Playwright asserts 4 host snippets + key CTA; mobile OK; banned-term grep clean; source-string pin tests updated (memory: workspace-copy tests pin UI copy) |
| **FE-KEYS-CHECK** | Verify/polish `/developers/keys`: create/copy/rotate key, show remaining credits, purchase CTA | Playwright: create → one-time plaintext → masked thereafter; balance visible |
| **QA-E2E** | E2E: spawn stdio server + programmatic MCP client with a real `rmv_test_` key against staging/prod; assert final text + credit decrement; repeat via remote `/api/mcp` | Both transports return final rewritten text; `/api/v1/usage` reflects the decrement; script exits 0 |

> Removed vs v1: `BACKEND-ANALYZE`, `PROXY-ANALYZE` (no analyze endpoint), and the `analyze_signal` / `list_scenarios` tools — replaced by the `RewriteBackend` replaceability seam.

---

## Verification Plan (skill-mapped)

- **`resilience-test-generation`** → MCP-CORE / MCP-REMOTE: poll-to-terminal, remote-timeout fallback, idempotent retry no-double-charge, quota exhaustion surfacing.
- **`state-machine-modeling`** → rewrite attempt + remote-timeout lifecycle explicit and boundary-tested.
- **`ui-browser-testing`** → FE-DEVELOPERS-MCP / FE-KEYS-CHECK: Playwright desktop+mobile, console/network clean, screenshots as proof.
- **`dotnet-backend-testing`** → **not triggered** (backend untouched). If MCP-CORE finds the `/api/v1/rewrite` contract insufficient, that becomes a flagged change requiring this skill + owner sign-off before touching C#.
- **Cross-cutting gates (block merge):** `npm run test`, `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib packages/mcp-server`, `cf:build`, `npm publish --dry-run`.
- **Manual final smoke (owner or computer-use):** add the published/remote server in a real Claude Desktop/Code and run one rewrite.

---

## Open Questions (owner decisions — resolve before DDW)

1. **Public tool surface.** ✅ **RESOLVED (owner direction, 2026-06-08):** keep it minimal — `rewrite_email` + `get_rewrite_result` only. No `analyze_signal`, no `list_scenarios`, no internal artifacts (signal/strategy/scenario) in output. Engine-internal analysis is untouched. (Reversible: if a real "score-only, no rewrite" customer need appears, add `analyze_signal` later as its own metered tool.)
2. **Remote async cap + fallback.** Default: poll up to ~50 s with progress keepalive; on cap, return `attempt_id` + require `get_rewrite_result`. Acceptable, or prefer always-immediate `attempt_id` + explicit poll? → **Decision needed.**
3. **npm version.** Default `0.1.0` (pre-1.0). Or `1.0.0`? → **Decision needed.**
4. **Public remote MCP path.** Default `/api/mcp`. Prefer `/mcp`? → **Decision needed.**
5. **Exact input mapping.** Confirm `draft`/`context`/`tone` → `RewriteRequest` fields during MCP-CORE; flag if the simplified surface drops a field the engine needs for quality. → **Verify in MCP-CORE.**
