# Cloudflare WAF Rate Limit Artifact

This artifact adds an edge layer for rewrite submit traffic. It is config-only and is not applied by CI.

Layer order:

1. Cloudflare edge rule limits raw POST traffic to `/api/rewrite` and `/api/v1/rewrite*` by IP address and Cloudflare location.
2. The in-process API-key pre-check sheds repeated v1 arrivals on the current instance before the database limiter runs.
3. The database window remains authoritative for v1 API keys and consumer users across instances.
4. Quota reservation stays last, so refused requests do not consume rewrite quota.

Manual apply template:

```bash
curl --request PUT \
  "https://api.cloudflare.com/client/v4/zones/${CLOUDFLARE_ZONE_ID}/rulesets/phases/http_ratelimit/entrypoint" \
  --header "Authorization: Bearer ${CLOUDFLARE_API_TOKEN}" \
  --header "Content-Type: application/json" \
  --data @infra/cloudflare/waf-rate-limit.json
```

Notes:

- `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ZONE_ID` are referenced by name only.
- This is a config artifact only. It is not wired into any GitHub Action, deployment script, or local automation.
- The owner applies it manually after review.
- It does not change DNS, custom domains, `LAUNCH_CONFIRMED`, or any payment setting.
- If the zone plan does not support a custom JSON response body for this rule, keep the same match and rate settings and allow Cloudflare's default 429 response.
