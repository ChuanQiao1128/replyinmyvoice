# M7-003 Sentry Prerequisite

Date: 2026-05-23

## Summary

M7-003 is not blocked by a product decision. It is blocked by package resolution in the current Codex sandbox: `npm view @sentry/nextjs version --json` fails with `ENOTFOUND registry.npmjs.org`, and `node_modules/@sentry` / the local npm cache do not contain `@sentry/nextjs`.

The previous implementation attempt added Sentry client/server/edge wiring and observability docs, but it could not produce a valid `package-lock.json` update. Do not commit `package.json` with `@sentry/nextjs` unless the matching lockfile is generated in the same change.

## Engineering Prerequisite

Run M7-003 from a networked npm environment that can resolve `registry.npmjs.org`, then install the dependency with the repo package manager so both manifests update together:

```bash
npm install @sentry/nextjs
```

After the lockfile is updated, implement or re-apply the Sentry wiring:

- `instrumentation-client.ts` for browser runtime initialization.
- `instrumentation.ts` for Next.js server and edge registration.
- `sentry.server.config.ts` and `sentry.edge.config.ts` for runtime config.
- Shared event filtering that sends only `error` / `fatal` events and masks personal fields before upload.
- `next.config.ts` `withSentryConfig` wrapping when source-map upload environment variables are present.
- `app/global-error.tsx` capture for app-level render errors.
- `docs/observability.md` setup, source-map, and verification notes.

## State Model

Entity: M7-003 issue-board item and its repair inbox item.

### States

- `pending`: M7-003 has not started.
- `in_progress`: Codex is attempting the Sentry implementation.
- `BLOCKED-AUTONOMY`: Codex reported a non-user blocker but the prerequisite was not yet narrowed.
- `BLOCKED-PROVIDER`: npm registry/DNS access is unavailable from the current sandbox, so the package lockfile cannot be generated.
- `ready_to_commit`: source, package manifest, lockfile, docs, tests, lint, typecheck, and banned-term scan are complete.
- `done`: the Sentry implementation PR has merged.

### Events

- `codex_attempt_started`: supervisor starts M7-003.
- `npm_registry_enotfound`: npm cannot resolve `registry.npmjs.org`.
- `networked_npm_available`: a runner can resolve and install from npm.
- `sentry_dependency_locked`: `package.json` and `package-lock.json` both include `@sentry/nextjs`.
- `validation_passed`: lint, typecheck, tests, and banned-term scan pass.
- `operator_env_ready`: Sentry project/runtime env/source-map upload env are configured outside source control.

### Transition Table

| From | Event | To | Side effect |
| --- | --- | --- | --- |
| `in_progress` | `npm_registry_enotfound` | `BLOCKED-PROVIDER` | Record the network prerequisite and stop before committing partial dependency changes. |
| `BLOCKED-AUTONOMY` | `npm_registry_enotfound` | `BLOCKED-PROVIDER` | Reclassify the vague autonomy block to a concrete provider/network prerequisite. |
| `BLOCKED-PROVIDER` | `networked_npm_available` | `in_progress` | Re-run M7-003 from a networked runner. |
| `in_progress` | `sentry_dependency_locked` | `in_progress` | Continue source implementation and docs. |
| `in_progress` | `validation_passed` | `ready_to_commit` | Write `plans/task-status.json` for the implementation branch. |
| `ready_to_commit` | PR merge | `done` | Mark M7-003 complete. |

### Invariants

- Do not commit `@sentry/nextjs` in `package.json` without the corresponding `package-lock.json` changes.
- Do not modify `.env.local`, `.dev.vars`, provider dashboards, Stripe live money, npm publish state, or secrets.
- Do not print Sentry auth tokens or any other secret values in docs, logs, status files, or PR text.
- Source implementation may name required environment variables, but values must stay outside source control.

### Illegal Transitions

- `BLOCKED-PROVIDER` -> `done` without a generated lockfile.
- `BLOCKED-PROVIDER` -> `BLOCKED-WAITING-USER` solely because npm DNS is unavailable in the sandbox.
- `in_progress` -> `ready_to_commit` when package manifests are inconsistent.
- Any state -> `done` after a source-map upload check that prints secret values.

### Persistence Implications

- `plans/issue-board.md` should show M7-003 as `BLOCKED-PROVIDER` until a networked npm runner is available.
- `plans/codex-worker-inbox.md` can mark this repair done because the blocker has been narrowed.
- `plans/blockers-log.md` should not frame this as a user decision; the engineering prerequisite is a networked npm install.

### Test Checklist

- Run `npm run lint`.
- Run `npm run typecheck`.
- Run `npm run test`.
- Run a scoped banned-term scan over the changed docs and any future `lib/**` Sentry helper.
- After the package lockfile exists, verify `npm ci` can install from the lockfile in CI.
