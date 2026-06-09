# FEKEYS: verify/polish /developers/keys (create / copy / rotate + balance + buy CTA)

## Context
The API-key management page already exists; this issue verifies and lightly polishes the self-serve flow so a developer can get a key and see their balance. Read: `app/developers/keys/page.tsx`, `components/developers/api-keys-panel.tsx`, `app/api/keys/*` (read-only — do not change). Spec: `plans/mcp-productization/REQUIREMENT.md` (FE-KEYS-CHECK).

## Constraints
- UI/UX only. Do NOT change key generation, hashing, or any backend behavior.
- Banned-term gate; mobile-friendly; match the existing design system.

## Changes required (scope: `app/developers/keys/**`, `components/developers/**`, `tests/**`)
1. Verify (fix if broken): create → one-time plaintext shown with a copy affordance → masked thereafter; rotate; revoke.
2. Show remaining credits / balance and a purchase CTA to pricing.
3. Add a focused test for the create→mask render (`tests/unit/api-keys-panel.test.ts` or a Playwright spec under `tests/e2e/` if a dev server is available; prefer a unit/component test asserting the masked render so it runs in CI without a server).

## Acceptance (machine-checkable, in worktree)
- `npm run typecheck` exits 0
- `npm run test` exits 0
- `npm run build` exits 0
- `grep -RniE "humanizer|bypass|undetect|detector|evade" app components` prints nothing

## DO NOT
- Do NOT alter `ApiKeyService` / key hashing / any backend. Do NOT add new deps. Do NOT push, open a PR, or touch `main`.
