# Issue M1-007

Title: M1-007 Add entra_user_id to User Prisma model + migration
Milestone: M1-Entra

## Task

Add `entraUserId String? @unique` to the Prisma `User` model and create the forward-only migration needed for it.

## Scope

- Touch only `prisma/schema.prisma`, the generated migration under `prisma/migrations/`, and `plans/clerk-to-entra-user-backfill.md`.
- Document a zero-downtime backfill plan: dual-write during migration, then switch primary lookup to `entraUserId`.
- Do not run destructive Prisma commands.

## Constraints

- Do not modify `.env.local`, `.dev.vars`, secrets, live provider settings, or Stripe configuration.
- Follow the current banned-positioning list from `plans/codex-implementation-prompt.md`; do not quote the blocked words in new copy.

## Verification

- `npm run lint`
- `npm run typecheck`
- `npm run test`
