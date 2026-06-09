# P2-07: Developer dashboard shell + usage view (cards + 30-day SVG bar chart + recent table)

**Tier:** 1 (prereq, merged into base) · **Owner:** Codex · **Depends on:** P2-02

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. Spec: `plans/rewrite-api-v1/PHASE-2-SPEC.md` §B + Q3 (tabs) + Q5 (dependency-free SVG).
- Today `app/developers/keys/page.tsx` renders `components/developers/api-keys-panel.tsx` (key CRUD). Turn this page into a **developer dashboard with tabs: Keys | Usage | Billing** (Billing tab body is delivered by P2-08; here just create the tab shell + the Usage tab).
- Data endpoints (from P2-02): `GET /api/me/api-usage/summary`, `/series?days=30`, `/recent?limit=50`.
- Visual system: "Warm Writing Desk" (off-white paper, soft borders, restrained warm accents). Reuse existing classes (see `app/developers/page.tsx` `api-*` styles and existing card classes). All copy English.

## Changes required
1. **Tab shell** on `/developers/keys` (or rename route to a dashboard): Keys (existing panel), Usage (new), Billing (placeholder section that P2-08 fills). Keep the keys panel working unchanged.
2. **Usage view** (`components/developers/usage-panel.tsx` + a chart component):
   - Three summary cards: **Today**, **Yesterday**, **Month-to-date**, each showing calls with a succeeded/failed split.
   - A **dependency-free SVG bar chart** of the 30-day `series` (warm palette, accessible: `<title>`/aria, hover/focus shows the exact day + counts). No new chart library.
   - A **remaining-quota** readout from `summary` (`remaining` of `quota`, `periodEnd`); on exhaustion show the existing buy path.
   - A **recent-calls table** from `recent` (time, endpoint, status, latency, key ••••Last4).
   - Loading + empty states (a user with zero calls shows zeroed cards + "no calls yet", never an error).
   - Mobile responsive (cards stack; chart stays readable).

## Acceptance (machine-checkable)
- [ ] `npm run typecheck` green; `npm run test` green (update any source-string contract test that pins `/developers` copy — grep `tests/` first).
- [ ] No new chart/graph dependency added to `package.json`.
- [ ] Banned-term gate clean: `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib` → no new matches.

## Do NOT
- Do NOT add a charting npm package. Do NOT fetch usage without the user's session (use the same-origin `/api/me/*` routes).
- Do NOT break the existing key-management panel or its tests.
