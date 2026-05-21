# Launch Announcement Drafts

Drafts for the public launch of Reply In My Voice — both the consumer product (`replyinmyvoice.com`) and the developer platform (MCP server + Claude Code Skill + REST API once shipped).

**These are drafts only. Do NOT post automatically.** The supervisor never schedules or publishes social posts. Owner reviews and posts manually.

Audience by channel:

- **Hacker News** → "Show HN" — developer audience, expect deep technical questions on the rewrite engine, Sapling integration, and signal calibration.
- **Twitter / X** — mixed audience, thread format works for the rewrite-engine narrative.
- **Reddit r/SaaS** — solo-founder audience, value proposition + pricing focus.
- **Reddit r/programming or r/MachineLearning** — only if the technical post-mortem is fully written; defer for now.

---

## 1. Hacker News — Show HN submission

**Title (≤80 chars):**

```
Show HN: Reply In My Voice – AI email rewrites that stay in your voice
```

**URL field:**

```
https://replyinmyvoice.com
```

**Text field (optional follow-up comment posted by author, ≤2000 chars):**

```
Hi HN — I'm the solo founder. Reply In My Voice rewrites emails to be clearer
and more natural while preserving the writer's tone, facts, and intent.

What's different:

- Diagnosis-driven rewrites. Before drafting, the engine runs a Sapling-backed
  naturalness check on your input and the candidate. If the candidate looks
  less natural than the input it gets repaired with targeted diagnosis tags
  (over-formal, hedging, vague-greeting, missing-fact). No more "AI-flavored"
  output that you have to undo by hand.
- A "no bad result" gate. If every candidate fails the quality bar, the app
  refuses to ship a bad rewrite and shows you why instead of returning slop.
- Hard fact preservation. Names, prices, dates, references are extracted into
  a ledger and verified against the rewrite. Drift triggers a repair pass.

Stack: Next.js on Cloudflare Pages (OpenNext) + Cloudflare Workers, Neon
Postgres + Prisma, Entra External ID for auth, Stripe Checkout, DeepSeek for
generation, Sapling for naturalness signal.

Also shipping today for developers:

- MCP server at `npx @replyinmyvoice/mcp-server` — works in Claude Code,
  Codex CLI, Cursor, Continue.dev
- Claude Code Skill at `agent-skills/replyinmyvoice-rewrite/`
- REST API at `/api/v1/rewrite` (B2B subscription tiers)

NZ$9/month for consumer. Free trial. No card up front.

Happy to answer anything about the rewrite engine, signal calibration, or the
"no bad result" decision. — Chuan
```

**Posting reminders:**

- Submit between 7am–9am Pacific (best HN traffic window for global reach).
- Keep tab open and reply to every comment in the first 4 hours.
- Do NOT shill in unrelated threads. Do NOT ask friends to upvote (HN guidelines).

---

## 2. Twitter / X — launch thread

**Tweet 1 (announcement, ≤280 chars):**

```
Today I'm launching Reply In My Voice — AI email rewrites that actually sound
like you wrote them.

After 8 months of work on a rewrite engine that won't ship a bad result,
it's live at https://replyinmyvoice.com

NZ$9/month, free trial, no card up front.

Thread on what's inside ↓
```

**Tweet 2 (problem framing):**

```
Most AI email tools produce text that screams "AI wrote this."

You can feel it. Recipients can feel it. So you spend 5 minutes rewriting the
rewrite, and you might as well have started from scratch.

That defeats the entire point.
```

**Tweet 3 (the diagnosis loop):**

```
Reply In My Voice runs a naturalness signal on every candidate before showing
it to you.

If the candidate looks LESS natural than your raw input, it gets repaired
with specific diagnosis tags — over-formal, hedging, vague-greeting,
missing-fact.

Repaired until it passes, or refused.
```

**Tweet 4 (no bad result):**

```
"Refused" is the key word.

If every candidate fails the quality bar, the app tells you "I couldn't
produce a rewrite I'd send" instead of giving you slop.

You waste 0 time. The engine loses a quota point, not your trust.
```

**Tweet 5 (fact preservation):**

```
Hard fact preservation:

Names, prices, dates, ticket numbers, refund amounts — pulled into a ledger
before generation, verified after.

Drift = repair pass. Drift after repair = refusal.

No more "wait, did it just change the price from $50 to $500?"
```

**Tweet 6 (developer platform):**

```
Also today, for devs building agent workflows:

→ MCP server: npx @replyinmyvoice/mcp-server (Claude Code, Codex, Cursor)
→ Claude Code Skill: agent-skills/replyinmyvoice-rewrite/
→ REST API: /api/v1/rewrite (B2B tiers)

Embed the rewrite engine into whatever you're building.
```

**Tweet 7 (close + CTA):**

```
Solo founder, NZ-based, TimeAwake Ltd.

If you want emails that stay in your voice — try it free at
https://replyinmyvoice.com

If you're a dev building agents — read the developer docs at
https://replyinmyvoice.com/developers

Feedback, bug reports, questions all welcome. /end
```

**Posting reminders:**

- Post the whole thread in one go — do NOT space tweets out by minutes.
- Pin tweet 1 for 48 hours.
- Reply to every comment for the first 24 hours.

---

## 3. Reddit r/SaaS — launch post

**Title:**

```
Launched: Reply In My Voice (AI email rewrites that don't sound AI) — 8 months solo
```

**Body:**

```
Solo founder here. Today I shipped Reply In My Voice — an email rewrite tool
that runs a quality gate hard enough to refuse output when it isn't good
enough. NZ$9/month, free trial, no card up front.

**The problem I kept hitting**

Every AI email tool I tried gave me text that recipients could spot as
AI-written. So I spent 5+ minutes editing the "rewrite" back to sound like me.
Net time saved: zero.

**The bet**

Build a rewrite engine that:

1. Generates 3 candidates per request.
2. Runs each through a Sapling-backed naturalness signal compared against the
   user's raw input.
3. If a candidate looks LESS natural than the input, runs a targeted repair
   pass with diagnosis tags (over-formal, hedging, vague-greeting,
   missing-fact, drift).
4. If every candidate fails, refuses to ship a rewrite. Tells the user why.

That last step — refusing — was the hardest UX call to make. It feels like
failure. But it's what makes the product trustable. You don't spend time
re-editing slop because you never see slop.

**Tech stack (for the curious)**

- Next.js on Cloudflare Pages (OpenNext) + Cloudflare Workers
- Neon Postgres + Prisma
- Entra External ID for auth
- Stripe Checkout (NZ$9/month subscription)
- DeepSeek for generation, Sapling for naturalness signal

**What I'm shipping today**

- Consumer product at https://replyinmyvoice.com
- Developer platform: MCP server + Claude Code Skill + REST API at
  https://replyinmyvoice.com/developers — pricing tiers TBA

**What I'd love feedback on**

- Pricing: NZ$9/month for ~50 rewrites. Too low? Too high?
- Refusal copy: when the engine declines to ship a rewrite, what should the
  user see? Currently it explains which signals failed.
- B2B tier shape: I'm planning Starter (1k req/mo) / Pro (10k) / Growth (100k)
  — does that match your usage?

Happy to AMA on the rewrite engine, signal calibration, or the solo-founder
side. — Chuan, TimeAwake Ltd
```

**Posting reminders:**

- Post during US business hours (10am–2pm ET) for r/SaaS.
- Engage every comment for first 12 hours.
- No DMs to promoters — flagged immediately.

---

## 4. (Optional) LinkedIn — short post for professional network

**Body (≤1300 chars):**

```
After eight months of nights and weekends, Reply In My Voice is live.

It rewrites emails to be clearer and more natural while preserving the
writer's voice. The differentiator isn't another AI prompt — it's the
quality gate. If the engine can't produce a rewrite that stays in your
voice and keeps the facts intact, it refuses to ship one. That refusal is
the feature.

Today's launch covers:

→ Consumer product at replyinmyvoice.com (NZ$9/month, free trial)
→ Developer platform: MCP server + Claude Code Skill + REST API at
  replyinmyvoice.com/developers

Built on Next.js / Cloudflare / Neon / Stripe. Solo + TimeAwake Ltd.

Feedback always welcome.
```

**Posting reminders:**

- Tag connections only if they've explicitly opted in.
- Cross-post to relevant LinkedIn groups (Solo Founders, NZ Tech) only if
  rules allow self-promotion.

---

## Internal launch checklist (operator)

Before posting any of the above, confirm:

- [ ] `replyinmyvoice.com` loads with no CSP errors in production
- [ ] Stripe live checkout completes end-to-end for the operator's own card
      (M7-001 sign-off)
- [ ] `/developers` and `/launch` pages load
- [ ] `docs/rollback-plan.md` rollback procedure has been dry-run within 7 days
- [ ] Status page is live (M7-006 dependency)
- [ ] Support email (`support@replyinmyvoice.com`) returns autoresponder
- [ ] PostHog or equivalent analytics receives a test event from prod
- [ ] Sentry receives a synthetic error from prod

If any item is unchecked, **delay the post**. A launch with broken sign-up is
worse than a launch 3 days later.
