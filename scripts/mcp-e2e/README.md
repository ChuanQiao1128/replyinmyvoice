# MCP E2E Harness

This harness checks the Reply In My Voice MCP stdio server and remote `/api/mcp`
route against a real test API key. It calls `rewrite_email`, verifies the tool
returns final rewritten text, and confirms remaining usage drops by one after
each transport call.

Dry run:

```sh
node scripts/mcp-e2e/run.mjs --dry-run
```

The dry run sends no requests. If `REPLY_IN_MY_VOICE_API_KEY` is absent, the
script also exits as a dry run.

Live run:

```sh
REPLY_IN_MY_VOICE_API_KEY=<your test key> node scripts/mcp-e2e/run.mjs
```

The key must start with `rmv_test_`. Do not place key values in source files or
shell history you intend to share.

Options:

```sh
REPLY_IN_MY_VOICE_BASE_URL=https://replyinmyvoice.com node scripts/mcp-e2e/run.mjs
node scripts/mcp-e2e/run.mjs --base-url http://localhost:3000
node scripts/mcp-e2e/run.mjs --skip-remote
node scripts/mcp-e2e/run.mjs --timeout-ms 180000
```

The stdio check starts `packages/mcp-server` from `dist/bin.js` when it exists,
or from the TypeScript source through the repo-local `tsx` dependency.
