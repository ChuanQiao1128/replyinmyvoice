# M6-004 Custom Domain Attach Check

Date: 2026-05-22

## Result

The current Codex shell could not confirm the live Cloudflare Workers custom-domain API state because DNS resolution for `api.cloudflare.com` and `replyinmyvoice.com` failed before any Cloudflare response was returned.

Local repo evidence still points to the intended configuration:

- `wrangler.jsonc` declares Worker `replyinmyvoice-app`.
- `wrangler.jsonc` declares `replyinmyvoice.com` and `www.replyinmyvoice.com` as Worker custom domains.
- `docs/manual-setup.md` records that the apex domain was attached to `replyinmyvoice-app` on 2026-05-18 and that `www.replyinmyvoice.com` was moved to the Worker on 2026-05-20.
- `docs/preflight-report.md` records a prior Workers custom domains API read as successful and a prior `replyinmyvoice.com` smoke as successful.

This issue still needs a networked rerun to produce current API evidence.

## Repair Rerun Evidence

Date: 2026-05-22T18:48:06+12:00

- `node` DNS lookup returned `ENOTFOUND` for `api.cloudflare.com`, `replyinmyvoice.com`, and `example.com`.
- `curl` to `https://api.cloudflare.com/client/v4/` returned `Could not resolve host: api.cloudflare.com`.
- `curl` to `https://replyinmyvoice.com/` returned `Could not resolve host: replyinmyvoice.com`.

### Repair Queue State Model

State list:

- `in_progress`: the shell supervisor has handed the repair item to Codex.
- `not_actionable`: Codex reproduced the blocker and determined the local sandbox cannot resolve the required external DNS names.
- `ready_to_commit`: Codex has written the repair evidence and status file for the shell supervisor to commit.

Event list:

- `sandbox_dns_failure_reproduced`: secret-free DNS and curl checks fail before reaching Cloudflare.
- `networked_verification_available`: an external shell can resolve Cloudflare and the formal domain.

Transition table:

| From | Event | To | Side effect |
| --- | --- | --- | --- |
| `in_progress` | `sandbox_dns_failure_reproduced` | `not_actionable` | Record the current DNS evidence and point to the networked verification commands. |
| `not_actionable` | `networked_verification_available` | external rerun | Run the read-only Workers domains API and formal-domain smoke checks outside this sandbox. |

Invariants:

- Do not mark M6-004 verified without live API or HTTP evidence.
- Do not attach, detach, deploy, mutate DNS, push secrets, or change dashboards from this repair.
- Do not write or print secret values.

Illegal transitions:

- `in_progress -> done` without current Cloudflare API evidence.
- `not_actionable -> done` based only on local repo configuration.
- Any transition that performs a live Cloudflare mutation from this repair.

Persistence implications:

- `plans/codex-worker-inbox.md` records the repair item as not autonomously actionable.
- `plans/task-status.json` records this repair as ready for the shell supervisor to commit.

Test checklist:

- Secret-free DNS lookup for `api.cloudflare.com` and `replyinmyvoice.com` reproduces the sandbox failure.
- Secret-free curl to the Cloudflare API and formal domain fails before HTTP status evidence.
- Required npm validation commands pass after the docs-only repair.

## Architecture Cost Review

- Goal: confirm `replyinmyvoice.com` is attached to the existing Cloudflare Worker `replyinmyvoice-app`.
- Usage assumption: read-only production verification for launch readiness.
- Runtime requirements: no deploy, no DNS mutation, no Worker secret change, no paid-resource creation.
- Options compared: read-only Workers domains API check, dashboard visual confirmation, `wrangler deploy` from `wrangler.jsonc`.
- Fixed monthly cost risks: none for read-only verification.
- Variable usage cost risks: none for Cloudflare API and route smoke checks.
- Recommended option: run the read-only Workers domains API check first, then smoke test the formal domain if it shows `replyinmyvoice.com -> replyinmyvoice-app`.
- Rejected options: redeploying solely to recreate domains, changing DNS records, or detaching Pages/Worker domains without current API evidence.
- Approval gates: any attach, detach, DNS deletion, or dashboard mutation should be performed by the user or a networked supervisor with explicit production-change authorization.
- Verification needed: Workers domains API response plus formal-domain HTTP status checks.
- Limitations: this sandbox could not resolve the Cloudflare API or formal domain.

## Networked API Verification

Run this from a shell that can resolve Cloudflare hostnames. Do not print token values.

```bash
set -a
. ./.env.local >/dev/null 2>&1
set +a

curl -sS "https://api.cloudflare.com/client/v4/accounts/${CLOUDFLARE_ACCOUNT_ID}/workers/domains?hostname=replyinmyvoice.com&service=replyinmyvoice-app" \
  -H "Authorization: Bearer ${CLOUDFLARE_API_TOKEN}" \
  -H "Content-Type: application/json" \
  | jq '.result[] | {id, hostname, service, environment, zone_name}'
```

Expected evidence:

```json
{
  "hostname": "replyinmyvoice.com",
  "service": "replyinmyvoice-app"
}
```

Repeat for `www.replyinmyvoice.com` if the issue is treated as covering both hostnames:

```bash
curl -sS "https://api.cloudflare.com/client/v4/accounts/${CLOUDFLARE_ACCOUNT_ID}/workers/domains?hostname=www.replyinmyvoice.com&service=replyinmyvoice-app" \
  -H "Authorization: Bearer ${CLOUDFLARE_API_TOKEN}" \
  -H "Content-Type: application/json" \
  | jq '.result[] | {id, hostname, service, environment, zone_name}'
```

## Formal Domain Smoke

If the API confirms the apex domain is attached to `replyinmyvoice-app`, run:

```bash
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice.com/
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice.com/pricing
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice.com/sign-in
curl -sS -o /dev/null -w "%{http_code} %{redirect_url}\n" https://replyinmyvoice.com/app
curl -sS -o /dev/null -w "%{http_code}\n" https://replyinmyvoice.com/api/stripe/webhook
curl -sS -o /dev/null -w "%{http_code}\n" https://replyinmyvoice.com/api/health/db
curl -sS -X POST -o /dev/null -w "%{http_code}\n" https://replyinmyvoice.com/api/rewrite
```

Expected statuses for M6-004 scope:

- `/`: 200
- `/pricing`: 200
- `/sign-in`: 200
- `/app`: 307 signed-out redirect to `/sign-in`
- `/api/stripe/webhook`: 200
- `/api/health/db`: 200
- unauthenticated `/api/rewrite`: 401

## Attach Steps If Missing

Use these only if the read-only API check returns no `replyinmyvoice.com` domain attached to `replyinmyvoice-app`.

Dashboard path:

1. Open Cloudflare dashboard.
2. Go to Workers & Pages.
3. Select Worker `replyinmyvoice-app`.
4. Open Settings -> Domains & Routes.
5. Select Add -> Custom Domain.
6. Enter `replyinmyvoice.com`.
7. Confirm the domain is created for Worker `replyinmyvoice-app`.
8. Repeat for `www.replyinmyvoice.com` only if the launch target requires `www` to serve the same Worker.
9. Run the formal domain smoke commands above.

API path:

```bash
set -a
. ./.env.local >/dev/null 2>&1
set +a

curl -sS -X PUT "https://api.cloudflare.com/client/v4/accounts/${CLOUDFLARE_ACCOUNT_ID}/workers/domains" \
  -H "Authorization: Bearer ${CLOUDFLARE_API_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "hostname": "replyinmyvoice.com",
    "service": "replyinmyvoice-app",
    "zone_name": "replyinmyvoice.com"
  }' \
  | jq '{success, result: .result | {id, hostname, service, environment, zone_name}}'
```

Optional `www` attach if needed:

```bash
curl -sS -X PUT "https://api.cloudflare.com/client/v4/accounts/${CLOUDFLARE_ACCOUNT_ID}/workers/domains" \
  -H "Authorization: Bearer ${CLOUDFLARE_API_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "hostname": "www.replyinmyvoice.com",
    "service": "replyinmyvoice-app",
    "zone_name": "replyinmyvoice.com"
  }' \
  | jq '{success, result: .result | {id, hostname, service, environment, zone_name}}'
```

Cloudflare's current Workers domains API documents `GET /accounts/{account_id}/workers/domains` for listing domains and `PUT /accounts/{account_id}/workers/domains` for attaching a domain to a Worker.
