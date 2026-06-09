# API Burst Load Test

`scripts/load-test/api-burst.mjs` is an operator-run harness for the async public rewrite API. It submits concurrent `POST /api/v1/rewrite` requests, reports submit latency and status-code mix, and can optionally poll accepted request ids until each reaches a terminal state.

Real production load runs are owner-run only. CI and worker verification should use `--dry-run` plus the .NET burst tests instead of calling live endpoints.

## Dry Run

Use dry run before any staging or production target. It parses the arguments, prints the planned run, and sends no network traffic.

```bash
node scripts/load-test/api-burst.mjs \
  --dry-run \
  --url https://<staging-host> \
  --key "$RIMV_API_KEY" \
  --concurrency 5 \
  --requests 10
```

The key may be supplied with `--key`, `RIMV_API_KEY`, or `API_KEY`. The harness does not print the key in its JSON output.

## Staging Run

Use a small staging burst first, then increase request volume only after the JSON summary shows the expected status-code mix.

```bash
node scripts/load-test/api-burst.mjs \
  --url https://<staging-host> \
  --key "$RIMV_API_KEY" \
  --concurrency 20 \
  --requests 120 \
  --poll
```

## Production Run

Production runs are performed by the owner after staging passes. Use the production API key at runtime only; do not write it into source, docs, shell history snippets, or committed config.

```bash
node scripts/load-test/api-burst.mjs \
  --url https://<production-host> \
  --key "$RIMV_API_KEY" \
  --concurrency 20 \
  --requests 120 \
  --poll
```

## Expected Pass Thresholds

- RPM is enforced: for a key with a 60 RPM limit, a same-minute burst should report no more than 60 `202` submits. The remainder should be `429`, not extra accepted work.
- Submit path remains cheap: submit p95 should stay in the low hundreds of milliseconds for staging-sized bursts because the API only reserves work and returns `202`.
- Queue backlog stays bounded: when `--poll` is enabled, accepted ids should continue reaching terminal states without a growing timeout count over repeated runs.
- Poll latency is bounded: poll p95 should stay within the current worker service-level target for the environment under test. Investigate if terminal-state p95 grows run over run while submit p95 remains stable.
- Rejected requests do not reserve rewrite quota. The machine-checkable invariant is covered by `ApiBurstRateLimitTests`.

## Output

The harness prints one JSON object to stdout. The important fields are:

- `submit.statusHistogram`: status-code counts for all submit attempts.
- `submit.accepted202`, `submit.rateLimited429`, `submit.other`: quick pass/fail counters.
- `submit.latencyMs.p50` and `submit.latencyMs.p95`: submit latency percentiles.
- `poll.terminalStateHistogram`: terminal-state counts for accepted ids when `--poll` is used.
- `poll.latencyMs.p95`: end-to-terminal poll latency for accepted ids.
