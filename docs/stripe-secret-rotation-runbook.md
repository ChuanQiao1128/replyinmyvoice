# Stripe Secret Rotation Runbook

Use this runbook for rotating the Stripe sandbox secret used by the Azure Functions backend. Do not record secret values in tickets, docs, logs, pull requests, or chat.

## Pre-Rotation

- Confirm the replacement key is for the intended Stripe test/sandbox account.
- Confirm the key can read account billing metadata, current balance, Checkout, Billing Portal, refunds, subscriptions, payment intents, prices, and webhook-related objects used by the backend.
- Confirm the current Azure Functions app setting name is `STRIPE_SECRET_KEY`.
- Confirm `/health/ready` is reachable before rotation and note the current `checks.stripeAuth.ok` and `checks.stripeAuth.authMode` values.
- Confirm no checkout, portal, webhook replay, or billing support operation is actively being tested during the rotation window.

## Rotation Steps

1. Add or reveal the replacement Stripe test/sandbox secret in the Stripe dashboard.
2. Update the Azure Functions app setting named `STRIPE_SECRET_KEY` with the replacement value.
3. Restart or let Azure Functions refresh app settings according to the current deployment process.
4. Do not print the value in terminal output, deployment logs, or screenshots.

## Post-Rotation Verification

1. Call `GET /health/ready` on the Azure Functions backend.
2. Confirm the response is HTTP 200.
3. Confirm the response body includes `checks.stripeAuth.ok: true`.
4. Confirm `checks.stripeAuth.authMode` is `secret_key`.
5. Confirm `checks.stripeAuth.error` is absent or null.
6. If the response is HTTP 503 with `checks.stripeAuth.error`, treat the new key as unhealthy and follow rollback.

## Rollback

1. Restore the previous Stripe test/sandbox secret value into the Azure Functions app setting named `STRIPE_SECRET_KEY`.
2. Restart or let Azure Functions refresh app settings according to the current deployment process.
3. Call `GET /health/ready`.
4. Confirm `checks.stripeAuth.ok: true` before retrying billing or webhook tests.
5. Revoke the failed replacement key only after rollback health is confirmed.

## Monitoring/Alerting

- Alert on `/health/ready` returning HTTP 503.
- Alert when `checks.stripeAuth.ok` is false.
- Alert when `checks.stripeAuth.error != null`.
- Treat `auth_failed` as a key revocation, rotation, permission, or account-mode mismatch incident.
- Treat `invalid_request` as a malformed or incompatible key/configuration incident.
- Treat `provider_error` or runtime exception names as dependency or SDK/runtime incidents and inspect backend logs without printing the secret value.
