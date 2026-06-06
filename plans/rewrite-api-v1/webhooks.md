# API Result Webhooks

API keys can have one result webhook URL. When a rewrite submitted with that key reaches a terminal state, Reply In My Voice posts a signed JSON payload to the configured URL. Delivery is asynchronous and does not block the rewrite result path.

## Configure

Use the developer key manager to set or clear the webhook URL for a key. Setting a URL generates a signing secret and shows it once. Store it with the receiving service.

## Payload

Successful rewrite:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "status": "succeeded",
  "rewrittenText": "Hi Sam, the report is ready.",
  "signal": {
    "draft": 78,
    "rewrite": 24
  }
}
```

Failed or expired rewrite:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "status": "failed",
  "error": {
    "code": "provider_failed",
    "message": "The rewrite could not be completed. Please try again."
  }
}
```

The `id` value is the rewrite attempt id returned by `POST /api/v1/rewrite`.

## Signature Header

Each delivery includes:

```text
X-RIMV-Signature: sha256=<hex HMAC-SHA256(signing secret, raw request body)>
```

Verify the signature against the exact raw request body bytes before parsing JSON.

```ts
import { createHmac, timingSafeEqual } from "node:crypto";

export function verifyRimvWebhook(rawBody: Buffer, headerValue: string) {
  const signingSecret = process.env.RIMV_WEBHOOK_SIGNING_SECRET;
  if (!signingSecret) {
    return false;
  }

  const expected =
    "sha256=" +
    createHmac("sha256", signingSecret).update(rawBody).digest("hex");

  const received = Buffer.from(headerValue, "utf8");
  const expectedBytes = Buffer.from(expected, "utf8");

  return (
    received.length === expectedBytes.length &&
    timingSafeEqual(received, expectedBytes)
  );
}
```

## Retry Behavior

Reply In My Voice treats any 2xx response as delivered. Non-2xx responses and network failures are retried with bounded backoff. After the retry limit is reached, the delivery is marked failed. The signing secret is never logged by the sender.
