# Scheduled Jobs Audit

Date audited: 2026-06-05

Scope: Azure Functions timer triggers under `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/`.

Cron format: Azure Functions NCRONTAB, with the seconds field first.

| Function name | Timer class | Cron | What it does | File |
| --- | --- | --- | --- | --- |
| `SendCreditExpiryReminders` | `CreditExpiryReminderTimerFunction` | `0 0 9 * * *` | Sends reminder notifications for unconsumed rewrite credits expiring inside the configured reminder window. | `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/CreditExpiryReminderTimerFunction.cs` |
| `ReleaseExpiredReservations` | `ExpiredReservationCleanupTimerFunction` | `0 */5 * * * *` | Releases expired rewrite usage reservations through `ExpiredReservationCleanupService`, preserving quota correctness for abandoned attempts. | `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/ExpiredReservationCleanupTimerFunction.cs` |
| `DispatchOutboxMessages` | `OutboxDispatcherTimerFunction` | `*/15 * * * * *` | Dispatches due outbox messages to the rewrite job publisher and records sent or retry state. | `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/OutboxDispatcherTimerFunction.cs` |
| `ExpirePaymentGrace` | `PaymentGraceExpiryFunction` | `0 0 14 * * *` | Downgrades accounts whose payment grace window has expired by calling `StripeEventService.ProcessExpiredPaymentGraceAsync`. | `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/PaymentGraceExpiryFunction.cs` |
| `PurgeRewriteAttemptPayloads` | `RetentionPurgeFunction` | `0 30 2 * * *` | Nulls `RequestJson` and `ResultJson` on terminal `RewriteAttempt` rows older than 30 days while preserving the row, idempotency key, hash, timestamps, and status. | `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/RetentionPurgeFunction.cs` |
| `ReconcileStripePayments` | `StripeReconciliationTimerFunction` | `0 15 2 * * *` | Runs daily Stripe payment reconciliation for the previous day and logs discrepancy counts. | `backend-dotnet/src/ReplyInMyVoice.Functions/Functions/StripeReconciliationTimerFunction.cs` |

## Notes

- Payment grace expiry is scheduled daily at `0 0 14 * * *`.
- Stripe reconciliation is scheduled daily at `0 15 2 * * *`.
- Credit expiry reminders are scheduled daily at `0 0 9 * * *`.
- Retention purge is scheduled daily at `0 30 2 * * *`.

## Deviations

- `RewriteAttempt` does not currently carry a persisted source flag that distinguishes API-originated attempts from website-originated attempts. The retention purge therefore applies the 30-day payload scrub to all terminal attempts older than 30 days, as allowed by P2-06, and leaves non-terminal `Pending` and `Processing` attempts untouched.
- P2-06 replaced the former 90-day `ContentRetentionTimerFunction` (`ScrubExpiredRewriteContent`) with the 30-day `RetentionPurgeFunction` above. The legacy `REWRITE_CONTENT_RETENTION_DAYS` app setting is therefore **no longer consumed by a scheduled job** (`RetentionService.ScrubExpiredRawContentAsync` remains available but is not timer-wired); the effective content-retention window is now the 30-day purge.
