# API-07: Key-management UI (signed-in portal)

**Tier:** 2 · **Owner:** Codex · **Depends on:** API-06

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`
- Spec: `plans/rewrite-api-v1/SPEC.md` — §"Key management".
- Endpoints from API-06: `POST/GET/DELETE /api/keys`.
- A FAMILY of source-string contract tests pins /app + auth + copy; a UI/copy change that doesn't update them fails `npm run test` (project rule). Search `tests/` for the relevant pin tests and update them.
- Existing portal/page style: `app/app/page.tsx`, `components/`.

## Constraints (AGENTS.md + SPEC)
- Banned terms (also scanned in `lib/` and component names/text): `humanizer|bypass|undetect|detector|evade`. No secrets. No push/touch `main`.

## Changes required
1. A signed-in page (e.g. `app/developers/keys/page.tsx` or a portal section) that:
   - lists the user's API keys (masked, last used, created, revoked state) from `GET /api/keys`;
   - has a "Create key" flow (name → `POST /api/keys` → shows the plaintext ONCE with a copy button + a clear "you won't see this again" notice);
   - has a per-key "Revoke" action (`DELETE /api/keys/{id}`) with a confirm step.
2. Wire it into navigation where appropriate (do not disturb unrelated nav).
3. Update any source-string contract tests that assert page copy.

## Acceptance (machine-checkable)
- [ ] `npm run typecheck` + `npm run test` green (including updated source-string pin tests).
- [ ] Banned-term grep clean on `app components public lib`.
- [ ] No backend changes.

## Do NOT
- Do NOT modify backend endpoints. Do NOT display the full key after creation or persist it client-side beyond the one-time reveal.
