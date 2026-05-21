# Rollback Plan — `replyinmyvoice.com` Custom Domain

Date: 2026-05-21
Owner: TimeAwake Ltd (ChuanQiao1128)
Scope: How to revert `replyinmyvoice.com` (and `www.replyinmyvoice.com`) from the live Cloudflare Worker `replyinmyvoice-app` back to the previous Cloudflare Pages holding page, in the event the live launch needs to be rolled back.

This plan satisfies M7-007 (rollback procedure documented + dry-run).

---

## Trigger conditions — when to roll back

Use rollback if **any** of the following is observed for ≥10 minutes after launch and cannot be hot-fixed in <30 minutes:

- Sustained 5xx error rate >5% on `/api/rewrite` (per Worker logs / Application Insights / Sentry).
- Sign-in (Entra External ID) failure rate >25% on `/auth/callback` for new sessions.
- Stripe live webhook delivery failure rate >5% sustained, or any signature-verification failure on a new event.
- DeepSeek/Sapling outage causing ≥80% of rewrite requests to return the safe-failure response (which still costs the user latency).
- Privacy / billing incident requiring the live site to go down (e.g., the user's email reports a security issue, or a refund-eligibility dispute that requires removing the checkout flow).
- Operator (ChuanQiao1128) explicitly requests rollback.

Do **not** roll back for:

- Single-user bug reports.
- Non-rewrite UI issues (footer alignment, copy typos) — patch forward instead.
- Stripe sandbox-only behavior (we're now on live; sandbox is not in scope).

---

## Pre-launch state to preserve (so we *can* roll back)

| Artifact | Where | Notes |
|---|---|---|
| Cloudflare Pages project `replyinmyvoice` | Cloudflare dashboard → Workers & Pages → `replyinmyvoice` (Pages) | DO NOT delete. This is the holding-page project that owned `replyinmyvoice.com` pre-launch. |
| Pages project custom-domain attach | Pages → `replyinmyvoice` → Custom domains | Will be detached at cutover. Re-attaching is the rollback action. |
| Worker `replyinmyvoice-app` | Workers & Pages → Workers → `replyinmyvoice-app` | We are NOT deleting it on rollback — we just detach its custom-domain routes. |
| DNS records for `replyinmyvoice.com` and `www.replyinmyvoice.com` | Cloudflare DNS → zone `replyinmyvoice.com` | Cloudflare manages these as part of custom-domain attach; reverting the attach restores them. |
| Last known-good Worker version SHA | `git log origin/main --oneline -1` at cutover time | Recorded in `docs/preflight-report.md` and in `plans/decisions-log.md` at the cutover entry. |

The rollback assumes the holding-page Pages project still exists. If it has been deleted, see the "deep rollback" section at the bottom.

---

## Fast rollback (≤10 minutes — preferred path)

This is the dashboard/API path that detaches the Worker from `replyinmyvoice.com` and reattaches Pages.

### Step 1 — Confirm authority

Operator must hold `CLOUDFLARE_API_TOKEN` (in `.env.local`) with `Account: Workers Scripts: Edit`, `Account: Workers Routes: Edit`, `Zone: DNS: Edit`, and `Zone: Custom Pages: Edit` permissions. This is the same token used by `wrangler` for deploy. Verify with:

```bash
npx wrangler whoami
```

### Step 2 — Detach Worker custom domain (dashboard)

1. Cloudflare dashboard → Workers & Pages → `replyinmyvoice-app` → Settings → Triggers → Custom Domains.
2. Remove `replyinmyvoice.com` and `www.replyinmyvoice.com`.
3. Cloudflare automatically removes the underlying DNS records.

API equivalent (faster, scriptable):

```bash
ACCOUNT_ID="$(grep '^CLOUDFLARE_ACCOUNT_ID=' .env.local | cut -d= -f2)"
TOKEN="$(grep '^CLOUDFLARE_API_TOKEN=' .env.local | cut -d= -f2)"

# List existing domains on the Worker
curl -sS "https://api.cloudflare.com/client/v4/accounts/${ACCOUNT_ID}/workers/domains" \
  -H "Authorization: Bearer ${TOKEN}" | jq '.result[] | select(.hostname | test("replyinmyvoice.com$"))'

# Delete each domain by id (apex + www)
curl -sS -X DELETE "https://api.cloudflare.com/client/v4/accounts/${ACCOUNT_ID}/workers/domains/<DOMAIN_ID>" \
  -H "Authorization: Bearer ${TOKEN}"
```

Don't echo the token. Variables only.

### Step 3 — Reattach the holding-page Pages project

1. Cloudflare dashboard → Workers & Pages → Pages → `replyinmyvoice` → Custom domains → **Set up a custom domain** → `replyinmyvoice.com`. Repeat for `www.replyinmyvoice.com`.
2. Cloudflare re-creates the DNS records pointing to the Pages project.

### Step 4 — Smoke test the holding page

```bash
curl -sSI https://replyinmyvoice.com | head -5
curl -sSI https://www.replyinmyvoice.com | head -5
```

Expected: `200` (or `301` apex→www if that's how the holding page is configured). HTML body should contain the pre-launch holding-page copy.

### Step 5 — Set a stop-the-bleed env flag

Edit `.env.local` and set:

```
LAUNCH_CONFIRMED=false
STRIPE_LIVE_CUTOVER_APPROVED=false
```

The current `lib/env.ts` runtime guard will refuse to start a checkout flow on a Worker preview, but the live custom-domain Worker is what we just detached, so this is belt-and-braces. **Do not** commit `.env.local` (it's gitignored). Update `plans/decisions-log.md` with the rollback timestamp.

Expected downtime: **2–4 minutes** while DNS propagation flips from Worker to Pages. Cloudflare edge cache shortens this for users hitting recent paths.

---

## Customer communications template

Post immediately at rollback start, on whichever channels exist. Update once stabilized.

### Status-page banner / X (Twitter) / Bluesky

> **Reply In My Voice is temporarily offline.** We hit an issue during today's launch and have rolled back to a holding page while we investigate. New sign-ups and rewrites are paused. We will post an update within 2 hours.

### Email to active users (use `support@replyinmyvoice.com`)

```
Subject: Reply In My Voice — temporary rollback today

Hi {{name}},

We launched the live version of Reply In My Voice today and uncovered an issue that needed an immediate rollback. The site is showing a holding page while we investigate.

If you signed up or were charged today, your subscription is paused — no further charges will be made. We'll email you when we're back online and refund any partial-period charges automatically.

Thanks for your patience. I'll send a fuller update within 24 hours.

— Chuan (TimeAwake Ltd)
chuanqiao1128@gmail.com
```

### Stripe — pause new charges

If the rollback is billing-related, in the Stripe live dashboard:
1. Products → archive the active price (do **not** delete — keep historical billing intact).
2. If active subscriptions need to be paused, use Subscriptions → bulk action → **Pause collection**.
3. Do not issue blanket refunds without operator approval — handle case-by-case from support email.

---

## Dry-run procedure (do this before live launch)

The dry-run cannot use the real domain, because the holding page is the live fallback. Instead:

1. **Pick a staging hostname.** Use `rollback-test.replyinmyvoice.com` (create it as a DNS-only record pointing to the Worker for the duration of the test).
2. **Attach it to the Worker** via `wrangler.jsonc` `routes` array temporarily, or via the dashboard.
3. **Detach it via API/dashboard** following Steps 2–3 above.
4. **Reattach to Pages** as in Step 3.
5. **Smoke test** with `curl` — confirm 200 and holding-page HTML.
6. **Reattach to Worker** to clean up (return to pre-test state).
7. **Document timing** in `plans/decisions-log.md` — note how long detach→reattach actually took, and any surprises (e.g., DNS cache misses, custom-domain provisioning lag).

If a separate hostname dry-run isn't feasible (e.g., no spare DNS), at minimum walk through the dashboard UI steps with no clicks-to-confirm — verify the "Remove custom domain" button is reachable and the Pages project still owns `replyinmyvoice.com` as an addable custom domain.

---

## Deep rollback (if Pages holding-page project no longer exists)

If, post-launch, someone accidentally deletes the `replyinmyvoice` Pages project, the fast path doesn't work. Instead:

1. **Detach the Worker custom domain** (Step 2 above).
2. **Create a new Pages project** named `replyinmyvoice-fallback` (or reuse one) with a one-file `index.html` that says: "Reply In My Voice is temporarily offline. We'll be back soon. Contact: chuanqiao1128@gmail.com". Deploy via `wrangler pages deploy ./fallback --project-name=replyinmyvoice-fallback`.
3. **Attach `replyinmyvoice.com`** to the new Pages project.
4. **Update DNS** as needed if Cloudflare doesn't auto-manage (it should, for proxied records on a Cloudflare-managed zone).

Expected downtime: **15–30 minutes** for this path (mostly Pages project creation + first deploy).

---

## Post-rollback follow-up

Within 4 hours of any rollback:

1. Append an incident entry to `plans/decisions-log.md` with `<ISO> | rollback | executed | <one-line cause>`.
2. Open a GitHub issue titled `Post-mortem: rollback YYYY-MM-DD` and link to the live evidence (Worker logs, Sentry events, Stripe events).
3. Decide whether to roll forward (patch + redeploy) or stay rolled back pending deeper investigation.
4. Re-attempt launch only after the root cause is patched, tested, and reviewed.

---

## Open items / known gaps

- This plan assumes only one Worker is in front of `replyinmyvoice.com`. If we later add a second Worker (e.g., the .NET Azure backend at M-Azure milestone fronts the API), this doc must be updated to describe rolling back each surface separately.
- Stripe live cutover (M7-001) has not yet happened as of doc creation date. Once it has, add a note here on what "pause Stripe live mode" looks like at the dashboard level.
- Dry-run timing data — to be appended to this file after the first dry-run is executed.
