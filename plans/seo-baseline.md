# SEO Baseline — Google Search Console + sitemap

Owner: Chuan / TimeAwake Ltd. Goal: get `replyinmyvoice.com` indexed by Google and capture a clean baseline of impressions / clicks / position before launch traffic arrives, so post-launch SEO movement is measurable.

This is a one-time setup plus a quarterly checkpoint.

---

## Prerequisites

- [ ] `replyinmyvoice.com` is live and serving on Cloudflare (Worker + Pages)
- [ ] `https://replyinmyvoice.com/sitemap.xml` returns a valid sitemap (M4-010 shipped this via `app/sitemap.ts`)
- [ ] `https://replyinmyvoice.com/robots.ts` route returns disallow rules for `/app`, `/admin`, `/api`, `/sign-in`, `/sign-up`, `/auth` (M4-010 also shipped this)
- [ ] Owner has access to a Google account that will own the GSC property (use the same account as the Stripe / domain owner so future audits stay simple)

---

## Step 1 — Add the GSC property

1. Visit <https://search.google.com/search-console>.
2. Click "Add property". Choose **Domain** (not URL prefix) — Domain covers all subdomains and both http/https without per-prefix duplication.
3. Enter `replyinmyvoice.com`.
4. Google will display a TXT record value to add to DNS. Note it down. Format looks like `google-site-verification=<random>`.

## Step 2 — Add the TXT record on Cloudflare DNS

Cloudflare is the authoritative DNS for `replyinmyvoice.com`.

1. Visit <https://dash.cloudflare.com> → select the `replyinmyvoice.com` zone → DNS → Records.
2. Click "Add record":
   - Type: `TXT`
   - Name: `@` (apex)
   - Content: `google-site-verification=<value-from-step-1>`
   - TTL: Auto
   - Proxy status: DNS only (TXT records cannot be proxied)
3. Save.
4. Wait 1–5 minutes for the record to propagate (apex TXT typically resolves fast through Cloudflare).
5. Verify on a terminal: `dig TXT replyinmyvoice.com +short` — the new value should appear.

## Step 3 — Complete verification in GSC

1. Back in GSC, click "Verify" on the pending property.
2. If verification fails, wait 2 more minutes and retry. Cloudflare TTL is fast but Google's resolver may cache a NXDOMAIN response briefly.

## Step 4 — Submit the sitemap

1. In GSC, sidebar → Sitemaps.
2. Add `https://replyinmyvoice.com/sitemap.xml` (the path Next.js exports from `app/sitemap.ts`).
3. Confirm status reads "Success". If it reads "Couldn't fetch", check `curl -I https://replyinmyvoice.com/sitemap.xml` returns `200`. If it returns `404`, the Next route is broken — re-check the `app/sitemap.ts` deploy.

## Step 5 — Request indexing for the priority URLs

Force-prime the index on the highest-value pages so Google doesn't wait until its next crawl:

1. In GSC → URL Inspection.
2. For each URL below, paste it in, then click "Request indexing" if not already indexed:
   - `https://replyinmyvoice.com/`
   - `https://replyinmyvoice.com/pricing`
   - `https://replyinmyvoice.com/developers`
   - `https://replyinmyvoice.com/launch` (post-launch only)
3. Google rate-limits to ~10 indexing requests per day per property. That covers the public surface.

`/app`, `/admin`, `/api/*`, `/sign-in`, `/sign-up`, `/auth/*` are intentionally disallowed via `robots.ts` — do NOT request indexing for these.

---

## Baseline metrics to record

Wait 7 days after submission, then capture a baseline screenshot or CSV export of:

| Metric | Source | Why it matters |
|---|---|---|
| Total impressions (last 7 days) | GSC → Performance → Search results | Tells you whether you're showing up at all |
| Total clicks (last 7 days) | Same | The actual entry-rate to the site |
| Average position | Same | Where Google ranks you on average across all queries |
| Top 10 queries | Same → Queries tab | What people are actually searching to find you |
| Indexed pages count | GSC → Pages → Indexed | Should be 5 after sitemap submit (`/`, `/pricing`, `/developers`, `/launch`, no others public) |
| Sitemap fetch status | GSC → Sitemaps | Must be "Success" |

Save the export as `docs/seo-baseline-<YYYY-MM-DD>.csv` (or an equivalent screenshot) so future quarters have a comparable snapshot.

---

## Common gotchas

**Verification stuck "Pending" for >24h**
Likely the TXT record name was set wrong. Cloudflare can interpret `replyinmyvoice.com` as a subdomain entry. Re-edit and set Name to `@` (or leave the name blank — Cloudflare expands `@` to apex either way). Confirm with `dig TXT replyinmyvoice.com +short`.

**Sitemap reads "Couldn't fetch"**
Three causes, in order of likelihood:
1. The deployed Next app does not export `app/sitemap.ts` (verify: `curl -I https://replyinmyvoice.com/sitemap.xml` returns 200, not 404).
2. The Worker route is wrong — `sitemap.xml` is being intercepted before reaching Next. Check `wrangler.toml` routes.
3. Cloudflare WAF is challenging Googlebot. Cloudflare → Security → Bots — confirm Googlebot is allowed (verified-bot rule).

**Pages indexed = 0 after 14 days**
- Check `robots.ts` isn't blocking everything.
- Check that the canonical URL points to `https://replyinmyvoice.com` (not the Cloudflare Pages preview URL).
- Manually request indexing on `/` from GSC and wait another 7 days.

**`/launch` indexed but you haven't launched yet**
That page is reachable in production once `app/launch/page.tsx` ships (M9-010). It's intentionally public — the launch announcement page is meant to be discoverable. No action needed.

---

## What this does NOT cover

- **Bing Webmaster Tools** — equivalent setup at <https://www.bing.com/webmasters>. Same TXT verification flow. Lower priority than GSC because Bing share of NZ traffic is small. Set up only if a customer reports finding the site via Bing.
- **Schema.org structured data** — add `Organization` and `Product` JSON-LD blocks to `app/layout.tsx` once the post-launch content is final. Out of scope for the baseline.
- **Backlink monitoring** — Ahrefs / Semrush integrations are a paid-tool decision; defer until post-launch traffic is measurable.

---

## Quarterly checkpoint

Once a quarter (set a calendar reminder):

1. Export current 90-day Performance CSV from GSC.
2. Diff against the previous quarter's snapshot in `docs/seo-baseline-<previous>.csv`.
3. If impressions or position dropped >25% on any priority URL, open a ticket — that's a signal of either a deployment regression (broken render) or a Google algorithm change worth investigating.
