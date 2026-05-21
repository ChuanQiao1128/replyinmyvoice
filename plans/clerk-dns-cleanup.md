# Clerk DNS Record Cleanup — Manual User Action

## Context

Post-Entra cutover, the Clerk-managed DNS records on replyinmyvoice.com are orphaned. They were created by Clerk Dashboard during the original auth setup. The supervisor will NOT delete them — this is user-run only, executed after 7 days of verified Entra-only operation with zero auth regressions.

## Orphaned records

- clerk.replyinmyvoice.com  (CNAME → frontend-api.clerk.services)
- accounts.replyinmyvoice.com  (CNAME → accounts.clerk.services)
- clkmail.replyinmyvoice.com  (CNAME → mail.clerk.services)
- clk._domainkey.replyinmyvoice.com  (CNAME)
- clk2._domainkey.replyinmyvoice.com  (CNAME)

## Method A — Cloudflare Dashboard UI

1. Sign in to dash.cloudflare.com.
2. Select the replyinmyvoice.com zone.
3. Open DNS → Records.
4. In the search box type "clerk", then "clk", to list both clusters.
5. For each matching record, click the three-dot menu → Delete.
6. Repeat search to confirm zero matches remain.

## Method B — wrangler CLI

```bash
# List candidate records
npx wrangler --account-id <ACCOUNT_ID> dnsrecords list --zone replyinmyvoice.com \
  | grep -iE 'clerk|clkmail|clk\._domainkey|clk2\._domainkey'

# For each id printed above:
npx wrangler --account-id <ACCOUNT_ID> dnsrecords delete <RECORD_ID> --zone replyinmyvoice.com
```

## Method C — Cloudflare REST API (curl + jq)

```bash
# Identify records (uses CLOUDFLARE_API_TOKEN from your shell env — never paste it in chat)
curl -sS -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
  "https://api.cloudflare.com/client/v4/zones/<ZONE_ID>/dns_records?per_page=100" \
  | jq '.result[] | select(.name | test("clerk|clkmail|clk\\._domainkey|clk2\\._domainkey")) | {id,name,type,content}'

# Delete each id:
curl -sS -X DELETE -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
  "https://api.cloudflare.com/client/v4/zones/<ZONE_ID>/dns_records/<RECORD_ID>"
```

## Verification

After deletion, each host should NXDOMAIN:

```bash
for host in clerk accounts clkmail clk._domainkey clk2._domainkey; do
  dig +short "${host}.replyinmyvoice.com" || echo "NXDOMAIN — good"
done
```

## When to run

Only after Entra cutover has been verified live for 7 days with zero auth regressions tracked in the launch readiness checklist. Supervisor does NOT execute this.
