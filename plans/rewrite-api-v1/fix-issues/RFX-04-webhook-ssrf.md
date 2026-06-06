# RFX-04: Webhook SSRF hardening (FIX-02)

**Tier:** 1 (merged to base) · **Owner:** Codex · **Depends on:** RFX-03
Detailed finding: `plans/rewrite-api-v1/CROSS-REVIEW.md` (#2). Confirmed by BOTH reviewers (verified).

## Context
- `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/ApiKeyService.cs` (~308-325) `TryNormalizeWebhookUrl` accepts ANY absolute http/https URL. The dispatcher `WebhookDispatcherService.cs` (`HttpWebhookDeliverySender`, ~32-51) POSTs server-side to it; the `HttpClient` is registered in `ServiceCollectionExtensions.cs` (~81) with default config (follows redirects). → a tenant can point the webhook at `http://169.254.169.254/...` (cloud metadata), `localhost`, or RFC1918 and turn the backend into an SSRF probe; a public host can also 302 → internal.

## Changes required
1. In `TryNormalizeWebhookUrl`: **require https**; reject hosts that resolve to loopback (`IsLoopback`), link-local (`169.254.0.0/16`, `fe80::/10`), private (RFC1918 `10/8`,`172.16/12`,`192.168/16`, ULA `fc00::/7`), and carrier-grade NAT (`100.64.0.0/10`); reject obvious metadata hosts. Resolve the host and validate the resolved IP(s), not just the literal.
2. Register the webhook `HttpClient` with a `SocketsHttpHandler` that sets `AllowAutoRedirect = false` and a `ConnectCallback` that re-validates the connected IP at connect time (defeats DNS-rebind), plus a short connect/overall timeout.
3. Re-validate the URL at **send time** as well (not only at save time), so a record saved before this change can't bypass it.

## Acceptance (machine-checkable)
- [ ] xUnit: `TryNormalizeWebhookUrl` rejects `http://...` (non-https), `https://127.0.0.1`, `https://169.254.169.254`, `https://10.0.0.5`, `https://localhost`, and accepts a normal public https URL; the sender does not follow redirects (test the handler config or a redirect target is refused).
- [ ] `cd backend-dotnet && dotnet test` green; banned-term grep clean.

## Do NOT
- Do NOT log the webhook secret. Do NOT break delivery to legitimate public https endpoints. Keep changes additive to the existing webhook entities/flow (RFX-05 handles delivery reliability).
