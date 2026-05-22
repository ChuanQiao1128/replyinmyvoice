# M6-001 Cloudflare Worker Secret Name Diff

Checked: 2026-05-22T05:28:18Z

## Result

The live Worker secret-name diff could not be completed in this sandbox.

Attempted command:

```bash
npx wrangler secret list --name replyinmyvoice-app --format json
```

Wrangler failed before returning any Worker secret names:

```text
Unable to resolve Cloudflare's API hostname (api.cloudflare.com or dash.cloudflare.com).
```

No secret values were printed or written. `.env.local` was parsed only for variable names and was not modified.

## Architecture Cost Review

- Goal: verify Cloudflare Worker production binding names against local live configuration names before any secret push.
- Usage assumption: read-only production configuration audit for the existing `replyinmyvoice-app` Worker.
- Runtime requirements: no deploy, no Worker mutation, no secret values in logs or committed files.
- Options compared: read-only `wrangler secret list`; secret push with `wrangler secret put`; deploy or dashboard changes.
- Fixed monthly cost risks: none for the read-only check.
- Variable usage cost risks: none for the read-only check.
- Recommended option: rerun the read-only `wrangler secret list` command from a networked, authenticated shell and compare names only.
- Rejected options: `wrangler secret put`, deploy, dashboard mutation, and any paid-resource change during M6-001.
- Approval gates: pushing missing secrets belongs to M6-002 and must still avoid printing values.
- Verification needed: live Worker secret-name list from Cloudflare.
- Limitations: Cloudflare API DNS resolution is unavailable in this sandbox, so the three-way diff below is not authoritative yet.

## present-in-both

Not computed. The live Worker secret-name list was unavailable.

## missing-in-worker

Not computed. The live Worker secret-name list was unavailable.

## missing-in-local

Not computed. The live Worker secret-name list was unavailable.

## Local Runtime Names Already In `wrangler.jsonc` Vars

These names are configured as plain Wrangler vars in `wrangler.jsonc`; they may be absent from `wrangler secret list` by design.

```text
ADMIN_ALLOW_RAW_REWRITE_TEXT
ADMIN_CLERK_USER_IDS
ADMIN_EMAILS
ADMIN_NZD_PER_USD
LAUNCH_CONFIRMED
MAX_ESCALATIONS
NATURALNESS_THRESHOLD
NEXT_PUBLIC_APP_URL
NEXT_PUBLIC_ENTRA_API_SCOPE
NEXT_PUBLIC_ENTRA_AUTHORITY
NEXT_PUBLIC_ENTRA_CLIENT_ID
OPENAI_ENABLE_FINAL_STRONG_MODEL
OPENAI_MAX_MODEL_CALLS_PER_REWRITE
OPENAI_MODEL_CHEAP_STRUCTURED
OPENAI_MODEL_ESCALATION
OPENAI_MODEL_MID_WRITER
OPENAI_MODEL_PRIMARY
OPENAI_MODEL_REPAIR
OPENAI_MODEL_STRONG_ESCALATION
OPENAI_PRICE_CHEAP_INPUT_PER_1M
OPENAI_PRICE_CHEAP_OUTPUT_PER_1M
OPENAI_PRICE_MID_INPUT_PER_1M
OPENAI_PRICE_MID_OUTPUT_PER_1M
OPENAI_PRICE_STRONG_INPUT_PER_1M
OPENAI_PRICE_STRONG_OUTPUT_PER_1M
OPENAI_TIMEOUT_SEC
REWRITE_COST_LOG_ENABLED
REWRITE_LEARNING_LOG_ENABLED
SAPLING_PRICE_PER_1000_CHARS_USD
STRIPE_TIMEOUT_SEC
WRITING_SIGNAL_PROVIDER
WRITING_SIGNAL_TIMEOUT_SEC
```

## Local Runtime Secret Candidates From `.env.local`

These are names only. Values were not read into this report.

```text
AUTH_SESSION_SECRET
CLERK_SECRET_KEY
DATABASE_URL
DEEPSEEK_API_KEY
DIRECT_URL
ENTRA_CLIENT_SECRET
NEXT_PUBLIC_CLERK_AFTER_SIGN_IN_URL
NEXT_PUBLIC_CLERK_AFTER_SIGN_UP_URL
NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY
NEXT_PUBLIC_CLERK_SIGN_IN_URL
NEXT_PUBLIC_CLERK_SIGN_UP_URL
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY
NODE_ENV
OPENAI_API_KEY
OPENAI_BASE_URL
OPENAI_MODEL
OPENAI_MODEL_FINAL_STRONG
SAPLING_API_KEY
STRIPE_PRICE_ID
STRIPE_SECRET_KEY
STRIPE_WEBHOOK_SECRET
```

## Operator-Only Local Names Excluded From Worker Secret Candidates

The local `.env.local` also contains deployment tokens, Azure provisioning values, evaluation controls, and package/GitHub credentials. Those names were not treated as Worker runtime secret candidates for this issue.

## M6-001 Retry

Checked: 2026-05-22T06:01:36Z

The read-only Worker secret-name diff still could not be completed because Cloudflare API DNS resolution fails before Wrangler can return any Worker metadata.

Retried command with Wrangler logs redirected to writable temp storage:

```bash
XDG_CONFIG_HOME=/private/tmp/replyinmyvoice-wrangler-config npx --no-install wrangler secret list --name replyinmyvoice-app --format json
```

Result:

```text
Unable to resolve Cloudflare's API hostname (api.cloudflare.com or dash.cloudflare.com).
```

Direct DNS check from this shell also failed for both Cloudflare API hosts:

```text
api.cloudflare.com ENOTFOUND:getaddrinfo ENOTFOUND api.cloudflare.com
dash.cloudflare.com ENOTFOUND:getaddrinfo ENOTFOUND dash.cloudflare.com
```

No Worker secret names were returned, so the name-only diff remains unavailable. No secret values were printed or written, no secrets were pushed, no deploy ran, no dashboard state changed, and `.env.local` was not modified.

This is recorded as a provider/DNS blocker for the sandboxed shell rather than a hidden user-only action. The next automated attempt should use the same read-only command only after Cloudflare API DNS resolution is available.

## M6-002 Push Attempt

Checked: 2026-05-22T05:32:41Z

No Worker secrets were pushed.

M6-002 requires the `missing-in-worker` list from M6-001 before running `wrangler secret put` for any name. That list is still unavailable because Wrangler cannot resolve Cloudflare API hostnames from this sandbox:

```bash
npx --no-install wrangler secret list --name replyinmyvoice-app --format json
```

Result:

```text
Unable to resolve Cloudflare's API hostname (api.cloudflare.com or dash.cloudflare.com).
```

Secret values were not read, printed, written, or pushed. `.env.local` was not modified.
