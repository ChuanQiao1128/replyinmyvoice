# FEMCP: /developers/mcp connect page

## Context
A new public page teaching how to connect the MCP server. Read: `app/developers/page.tsx`, `app/developers/keys/page.tsx`, `components/developers/*`, `plans/mcp-productization/REQUIREMENT.md` (Goals + FE work package). Independent of the package internals (content comes from the spec), so no code deps.

## Constraints
- Must pass the banned-term gate; mobile-friendly; match the existing `/developers` design system.
- Copy positioning: "natural / concise / meaning + facts preserved", never "passes AI detection".
- If any source-string pin test asserts `/developers` copy (grep `tests/` for the strings), update it in the same change.

## Changes required (scope: `app/developers/mcp/**`, `app/developers/page.tsx` for the entry link, `components/developers/**`, `tests/**` for pin-test updates)
1. `app/developers/mcp/page.tsx` — what the MCP offering is; **local** (`npx` stdio) and **remote** (HTTP URL + Bearer header) connection; four host config blocks (Claude Code, Codex, Claude Desktop, Cursor); a "get a key" CTA → `/developers/keys`; billing/quota note (1 credit per rewrite, `402` → top-up link); error UX.
2. Add an entry link from `/developers`.
3. Update any pin tests that assert these pages' copy.

## Acceptance (machine-checkable, in worktree)
- `npm run typecheck` exits 0
- `npm run lint` exits 0
- `npm run test` exits 0
- `npm run build` exits 0
- `grep -RniE "humanizer|bypass|undetect|detector|evade" app components` prints nothing

## DO NOT
- Do NOT call the backend from the page (static docs only). Do NOT add new deps. Do NOT touch the C# backend. Do NOT push, open a PR, or touch `main`.
