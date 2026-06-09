# GA-03: API legal & data pages (Terms of Use + Acceptable Use + Data/Retention) — DRAFT

**Tier:** 2 · **Owner:** Codex · **Depends on:** none

## Context
- Repo root: `/Users/qc/Desktop/CloudFlare`. No API legal pages exist (`app/developers/` has only `keys/` + `page.tsx`). Operator: TimeAwake Ltd, `replyinmyvoice.com`.
- Product framing (AGENTS.md): "Replies that still sound like you"; natural / concise / faithful; **never** detector-bypass/evasion. Data retention (SPEC.md §Security): **bounded 30-day** retention of API-originated request/result, deletion available; never claim "we don't store your data". Metering: pay per **succeeded** rewrite, shared quota pool, **no free tier**.
- Style: English, "Warm Writing Desk" (off-white, soft borders); reuse the existing page/section classes in `app/developers/page.tsx`.

## Changes required
1. **Three new pages**, each clearly headed "Draft — pending review":
   - `app/developers/terms/page.tsx` — **API Terms of Use** (eligibility, key responsibility/security, per-succeeded-call metering + shared quota + no free tier, acceptable-use by reference, disclaimer that `signal` is an informational naturalness reference and not a guarantee, limitation of liability, changes/termination).
   - `app/developers/acceptable-use/page.tsx` — **Acceptable Use Policy** (no illegal/deceptive/abusive/harassing content; no attempts to overload/reverse-engineer; no reselling raw access; user is responsible for content they submit/send).
   - `app/developers/data/page.tsx` — **Data & Retention** (what's stored: input/output on `RewriteAttempt`; **bounded 30-day retention** then purged; deletion; processors: the rewrite + naturalness providers; no overclaim).
2. **Link** all three from `/developers` (a footer or "Legal" section).

## Acceptance (machine-checkable)
- [ ] The 3 routes render (files exist, default-export a component); `/developers` links to `/developers/terms`, `/developers/acceptable-use`, `/developers/data`.
- [ ] `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib` → CLEAN (these pages are scanned).
- [ ] `npm run typecheck` + `npm run test` green (update any pinned `/developers` source-string test if present).

## Do NOT
- Do NOT claim the documents are legally reviewed/final (mark DRAFT). Do NOT use any banned term even to negate it (describe `signal` only positively). Do NOT invent legal entities beyond TimeAwake Ltd / replyinmyvoice.com.
