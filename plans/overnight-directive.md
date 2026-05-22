# Overnight Autonomous Directive — Reply In My Voice Commercialization Sprint

You are codex, working autonomously on the Reply In My Voice commercialization roadmap. You have full authority granted by the project owner. The supervisor (Claude) reviews your output in the morning.

## Working directory
`/Users/qc/Desktop/CloudFlare` (already cwd)

## Sources of truth (read these first)
- `docs/commercialization-north-star.md` — durable commercial target and Claude/Codex division of labor
- `CLAUDE.md` — "Active Commercialization Sprint" section is the authoritative posture
- `AGENTS.md` — project rules
- `plans/commercialization-roadmap.md` — milestone plan
- `plans/issue-board.md` — current state of all 105 issues
- `plans/issue-manifest.md` and `plans/issue-manifest-additions.md` — issue specs
- `plans/issues/M0-*.md` — detailed M0 briefs

## Your task this session
Work the next **3-5 pending issues** from `plans/issue-board.md` in milestone order, lowest M-number / lowest id first. Then exit.

Skip M0-001 through M0-005 — they were completed in commit `bff864b`. Mark them "done" in the board if still showing pending, then continue.

Start at **M0-006** (verify AGENTS.md Stripe live section + `## Active Commercialization Sprint` already in CLAUDE.md; if both already present, just commit/push CLAUDE.md change since Claude staged it locally).

After M0-006: M1-001 → M1-014 → M2 → M2.5 → M3 → M4 → M5 → M6 → M7 → M8 → M9.

## Per-issue loop
1. **Read state**: `gh issue view <number>` and `cat plans/issues/<id>.md` (if exists) or the manifest entry
2. **Plan**: write 1-3 sentences of approach (think before edit)
3. **Branch**: `git checkout -b chore/<id>` or `feat/<id>` from main
4. **Implement**: minimal change satisfying the acceptance criteria in the brief
5. **Validate**:
   ```
   npm run lint && npm run typecheck && npm run test
   ```
   If issue touches `backend-dotnet/`, also run `dotnet test backend-dotnet/ReplyInMyVoice.sln`
6. **Banned-term scan**: `grep -RniE "humanizer|bypass|undetect|detector|evade" app components public lib` — must be empty. If non-empty: revert, mark issue blocked, move on
7. **Commit**: conventional commit message, body explains rationale + closes #N
8. **Push**: `git push -u origin <branch>`
9. **PR**: `gh pr create --base main --head <branch> --title "<original issue title>" --body "Closes #<number>. <one-paragraph summary>."`
10. **Wait for CI** up to 3 min: `gh pr checks <pr-number> --watch` (poll, don't block forever)
11. **Merge** if green: `gh pr merge <pr-number> --squash --delete-branch`
12. **Close issue**: `gh issue close <number> --comment "Implemented in PR #<pr-number>"`
13. **Update board**: edit `plans/issue-board.md` — change this row's status from `pending` to `done`
14. **Log decision**: append to `plans/decisions-log.md` (create if missing): `<ISO date> | <issue-id> | <decision> | <rationale>`

## Hard limits (do not cross under any circumstance)

- **Banned terms** in user-facing copy + `lib/**` + comments + filenames: `humanizer | bypass | undetect | detector | evade`
- **No real Stripe charges** — never call `stripe.PaymentIntents.create` against live keys. M7-001 is for the user, not for you.
- **No `npm publish`** — M9-006 stays BLOCKED-WAITING-USER (user provides NPM_TOKEN)
- **No printing `.env.local` / `.dev.vars` / `globalapikey/`** values in logs, commits, or PR bodies
- **No modification of**: `LAUNCH_CONFIRMED`, `STRIPE_LIVE_CUTOVER_APPROVED`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_ID`
- **Eval spend cap**: DeepSeek + Sapling cumulative ≤ NZ$5 per issue, ≤ NZ$20 across this session. Track in `plans/sleep-run-budget.md`.
- **Azure new resources**: respect `AZURE_BUDGET_LIMIT` and `AZURE_ALLOW_PAID_RESOURCES` flags

## Failure handling

- **Test/lint fails**: attempt 1 fix. If 2nd attempt also fails, mark issue `BLOCKED` in `plans/issue-board.md` with reason, append to `plans/blockers-log.md`, move to next issue.
- **External dependency missing** (POSTHOG_API_KEY, SENTRY_DSN, NPM_TOKEN, AZURE_SQL_ADMIN_USER+PASSWORD): mark `BLOCKED-WAITING-USER`, move to next.
- **Banned term found in diff**: revert the offending change, mark BLOCKED with note, move on.
- **Network failure on gh / DeepSeek / Azure / Stripe**: retry once after 30s. If still failing, mark BLOCKED-NETWORK, move on.

## Stop conditions for this session

Exit cleanly when ANY of these true:
- ≥3 issues moved to `done`
- ≥5 issues processed (regardless of outcome)
- Time elapsed > 25 min within this codex exec call
- ≥3 consecutive issues hit BLOCKED (systemic problem — let supervisor investigate)

On exit, write `plans/overnight-progress.md` with format:
```
# Overnight Progress — <ISO date>

## Done this iteration
- M0-006 (#N): summary, PR #X, commit Y
- ...

## Blocked
- M1-002: reason
- ...

## Next pending
- M1-003 — next iteration picks up here
```

## Decision policy

When a brief is ambiguous, **make a sensible call** and document why in the commit message AND `plans/decisions-log.md`. Don't ask the supervisor — make the call.

Examples of acceptable autonomous decisions:
- Library choice when not specified (pick the lightest reasonable option)
- File naming conventions
- Test framework usage (use what the repo already uses: vitest for TS, xunit for .NET)
- Architectural patterns (prefer existing patterns in the repo)

Examples that need supervisor attention (mark BLOCKED-WAITING-USER):
- API tier pricing for B2B (Stripe products) — if not in the brief, mark BLOCKED
- Production env var values
- Anything involving real money flow

## Begin
Read CLAUDE.md, then plans/issue-board.md, then start.
