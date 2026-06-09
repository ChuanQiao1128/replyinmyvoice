# PKGREL: publish-ready metadata + README rewritten to the 2-tool contract

## Context
`@replyinmyvoice/mcp-server` is v0.0.1; its README documents a stale 3-tool interface (`analyze_signal`/`list_scenarios`/`scenario` enum) and points the key link at the wrong path. Read: `packages/mcp-server/package.json`, `packages/mcp-server/README.md`, `plans/mcp-productization/REQUIREMENT.md` (API and Job Contracts). Depends on **STDIO** (the package must actually expose the real tools before docs/publish).

## Constraints
- **No real publish** — only `npm publish --dry-run`. Version → `0.1.0`. `publishConfig.access=public`.
- README documents ONLY `rewrite_email` + `get_rewrite_result`, with both local (`npx`) and remote (`claude mcp add --transport http …` / `codex mcp add --url …`) connection snippets; key link → `https://replyinmyvoice.com/developers/keys`.
- Banned terms never appear in README/source.

## Changes required (scope: `packages/mcp-server/package.json`, `packages/mcp-server/README.md`, `packages/mcp-server/LICENSE`)
1. `package.json`: version `0.1.0`; `publishConfig {"access":"public"}`; `repository`; keep `files`/`bin`.
2. Rewrite `README.md` to the 2-tool contract; remove all `analyze_signal`/`list_scenarios`/`scenario`-enum copy; fix the key link; add the remote connect snippets.
3. Add `LICENSE` (MIT, matching the repo root) if missing.

## Acceptance (machine-checkable, in worktree)
- `npm --prefix packages/mcp-server run build` exits 0
- `cd packages/mcp-server && npm publish --dry-run` exits 0 and the tarball lists only `dist` + `README` + `LICENSE`
- `grep -RniE "humanizer|bypass|undetect|detector|evade" packages/mcp-server/README.md` prints nothing
- `grep -Eci "analyze_signal|list_scenarios" packages/mcp-server/README.md` prints `0`

## DO NOT
- Do NOT run `npm publish` (only `--dry-run`). Do NOT change tool behavior. Do NOT touch the C# backend. Do NOT push, open a PR, or touch `main`.
