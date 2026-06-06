# Reply In My Voice TypeScript SDK

Official TypeScript client for the Reply In My Voice public API. Published on npm as `replyinmyvoice-api`.

## Install

```sh
npm install replyinmyvoice-api
```

## Quickstart

```ts
import { createClient } from "replyinmyvoice-api";

const client = createClient({
  apiKey: process.env.REPLY_IN_MY_VOICE_API_KEY!,
});

const result = await client.rewrite("Please send a warmer update about the delayed shipment.");

console.log(result.rewrittenText);
console.log(result.signal); // naturalness reference: { draft, rewrite }
```

`rewrite()` submits the draft, polls the API, and returns the completed rewrite:

```ts
const { rewrittenText, signal } = await client.rewrite(draft, {
  idempotencyKey: "request-2026-06-06-001",
  pollIntervalMs: 1_500,
  timeoutMs: 120_000,
});
```

## Auth

Create a client with an API key:

```ts
const client = createClient({
  apiKey: "rmv_live_...",
});
```

Every request sends:

```text
Authorization: Bearer <apiKey>
```

The default API base URL is `https://replyinmyvoice.com`. For local or preview testing, pass `baseUrl`.

## Methods

```ts
await client.submitRewrite(draft, { idempotencyKey: "request-2026-06-06-001" });
await client.getRewrite(id);
await client.rewrite(draft);
await client.getUsage();
```

`submitRewrite()` returns `{ id, status }`. `getRewrite()` returns the current job state. `getUsage()` returns `{ scope, quota, used, remaining, periodEnd }`, where `periodEnd` can be `null` for sandbox or free usage.

## Errors

Non-2xx responses throw `RimvApiError`:

```ts
import { RimvApiError } from "replyinmyvoice-api";

try {
  await client.rewrite(draft);
} catch (error) {
  if (error instanceof RimvApiError) {
    console.error(error.code, error.message, error.status);
  }
}
```

Common API error codes include `invalid_key`, `quota_exhausted`, and `rate_limited`.

## Polling

The API is asynchronous: submit first, then poll the rewrite result endpoint. `rewrite()` handles that loop with a fixed polling interval. Increase `pollIntervalMs` for lower request volume, and back off before retrying a new request after a `rate_limited` error.
