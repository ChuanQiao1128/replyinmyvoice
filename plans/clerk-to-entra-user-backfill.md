# Clerk To Entra User Backfill Plan

Issue: M1-007
Date: 2026-05-23

## Goal

Add `User.entraUserId` as a nullable unique Entra External ID subject while keeping `User.clerkUserId` in place for the migration window. Existing users must continue to sign in and keep quota, subscription, API key, learning, and telemetry history tied to the same `User.id`.

## Migration Shape

- Add nullable `User.entraUserId`.
- Add a unique index on `User.entraUserId`.
- Leave `User.clerkUserId` required and unique during this step.
- Do not drop, rename, or rewrite existing user rows in this migration.

Postgres unique indexes allow multiple `NULL` values, so this migration is safe for existing rows before backfill.

## Zero-Downtime Backfill Steps

1. Deploy the additive Prisma migration.
2. Deploy auth code that writes both identifiers for any user session that can resolve both values.
3. For existing users, reconcile Entra sign-ins to the existing row by a verified account link such as the authenticated email address, then set `entraUserId` on that row.
4. During the migration window, keep reads compatible by looking up users by `entraUserId` first and falling back to `clerkUserId`.
5. Verify every active user with billing or usage history has exactly one `User` row and, after Entra sign-in, a populated `entraUserId`.
6. Switch primary lookup paths to `entraUserId` once the backfill is complete and verified.
7. Keep `clerkUserId` for a short rollback window.
8. In a later cleanup issue, make any final nullability decision and remove the old identifier only after rollback is no longer needed.

## Data Invariants

- `User.id` remains the stable foreign-key target for `RewriteUsage`, `RewriteLearningSample`, `RewriteCostLog`, and `ApiKey`.
- `User.entraUserId` must be unique whenever it is present.
- No backfill step may create a second user row for a person who already has quota, billing, telemetry, or API key records.
- Stripe customer and subscription fields stay on the existing `User` row.

## Operational Checks

- Count users with billing or quota history and `entraUserId IS NULL` before and after backfill.
- Check for duplicate proposed Entra subjects before writing them.
- Sample recently signed-in users and confirm their `User.id` did not change.
- Keep a rollback path that ignores `entraUserId` and continues using `clerkUserId` during the migration window.
