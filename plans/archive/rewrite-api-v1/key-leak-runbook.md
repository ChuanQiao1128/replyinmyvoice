# Key Leak Runbook

Use this runbook when the API-key usage-spike signal flags a key for review.

## Signal

- Alert event name: `api_key_usage_spike_flagged`.
- Source: `ApiKeyUsageAnomalyService` compares the current `ApiKeyUsage` count for one key against the prior comparable window and the configured absolute ceiling.
- Initial checks:
  - Confirm the flagged `ApiKeyId`, `ObservedCount`, `ExpectedCount`, `ThresholdCount`, `WindowMinutes`, and `Reason` in Application Insights or backend logs.
  - Compare the alert time with recent product launches, test traffic, or owner-requested load checks.
  - Treat unexplained spikes as a possible key leak until reviewed.

## Immediate Containment

Revoke access for the affected key before investigating wider usage.

- App route: `DELETE /api/keys/{id}`.
- Backend function: `RevokeApiKey` in `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiKeyHttpFunctions.cs`.
- Backend operation: `ApiKeyService.RevokeAsync(userId, keyId, cancellationToken)`.
- Expected result: HTTP `204` when the key belongs to the authenticated owner, or `404` when it does not exist for that owner.

Record the UTC time of the revoke action and the operator who performed it.

## Replacement Key

Use rotate when the owner needs a replacement and the old key has not already been revoked separately.

- App route: `POST /api/keys/{id}/rotate`.
- Backend function: `RotateApiKey` in `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ApiKeyHttpFunctions.cs`.
- Backend operation: `ApiKeyService.RotateAsync(userId, keyId, cancellationToken)`.
- Expected result: HTTP `201` with the replacement key plaintext shown once.

Operational ordering:

- If the owner needs uninterrupted access, run rotate first. The rotate operation revokes the old key and creates the replacement in one transaction.
- If the key was already revoked through `DELETE /api/keys/{id}`, do not retry rotate for the same key id. Create a new key with `POST /api/keys` using the owner-approved name and settings, then record that it replaced the revoked key.

## Notify The Owner

Send the owner a short incident note:

- State that a usage-spike alert flagged their API key.
- Confirm the old key was revoked or rotated.
- Provide the replacement key only through the existing one-time key display flow.
- Ask the owner to update all calling systems and remove the old key from local config, CI variables, scripts, and vendor dashboards.

Do not include full key values in tickets, chat, email, logs, or docs.

## Audit Recent Usage

Review recent `ApiKeyUsage` rows for the affected key:

```sql
SELECT TOP (200)
    CreatedAt,
    RequestId,
    Endpoint,
    StatusCode,
    LatencyMs,
    CostUsdEstimate
FROM ApiKeyUsages
WHERE ApiKeyId = '<api-key-id>'
ORDER BY CreatedAt DESC;
```

Check:

- First and last unusual request times.
- Endpoints hit and whether requests reached `200` or `202`.
- Failed status patterns such as auth failures, rate limits, quota exhaustion, or repeated server errors.
- Latency and estimated cost outliers.
- Whether traffic continued after revoke or rotate; any post-action usage requires immediate escalation.

## Closeout

- Link the alert log entry and the `ApiKeyUsage` audit query result in the incident ticket.
- Record the old key id, replacement key id if created, revoke or rotate time, owner notification time, and any cost impact.
- If the same owner or endpoint repeats this pattern, tune the threshold only after confirming the usage is legitimate.
