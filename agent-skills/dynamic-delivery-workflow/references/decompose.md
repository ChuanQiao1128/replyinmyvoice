# Phase 1 — Decompose (for `dynamic-delivery-workflow`)

Turn a free-form requirement into the three inputs Phase 2 (the unattended daemon) needs:

1. **GitHub issues** — one mergeable unit each.
2. One **brief** markdown per issue under `BRIEF_DIR` (the authoritative spec the driver hands Codex; shape = `scripts/codex-brief.tmpl`).
3. A draft **`queue.tsv`** — one row per issue, dependency-first.

There is **exactly ONE human checkpoint**, at the end of this phase. After the operator approves, Phase 2 (`preflight.sh` → `start.sh`) runs the wave with no further interruption.

> Lineage: the *rules* below are borrowed from the (disabled) `delivery-pipeline` skill's decompose protocol; the *artifacts and launch path* are adapted to this skill (briefs + `queue.tsv` + tiers, launched via the detached daemon instead of an in-session `/goal`). See the "Borrowed vs changed" block at the end.

---

## Inputs

- **The requirement** (inline text, a file path like `plans/<x>/REQUIREMENT.md`, or a linked doc — read it before decomposing).
- **The repo structure** — `ls` the root; read `README.md`, `AGENTS.md`, `CLAUDE.md`; identify the package manager and the project's quality commands.
- **The wave config keys** you will need for the briefs and queue: the integration **`BASE`** branch (NEVER `main`), the **`BRANCH_PREFIX`**, the off-iCloud **`CONTROL_DIR`** (`~/.rimv-delivery/<wave>/`), and **`BRIEF_DIR`** (where briefs live, e.g. `plans/<wave>-issues/`).

If the requirement is too vague to write concrete acceptance criteria, STOP and ask — do not invent criteria. You MAY run `system-spec-synthesis` upstream first to turn rough notes into an implementation-ready spec, then decompose that.

---

## Decomposition rules

### Rule 1 — One issue = one mergeable unit
Each issue must produce a PR that could be reviewed and merged **into the integration branch `$BASE`** (never `main`) on its own without breaking it. If B depends on A, note `Depends on: #A`, but B must still leave the tree working when merged after A.

### Rule 2 — Issue size target
Aim for what one Codex invocation can finish without running out of context: **≤ 8 files**, **≤ 400 LOC** changed (excluding tests/generated), **≤ 5 acceptance criteria**. If an issue exceeds these, split it.

### Rule 3 — Scope declared as file paths
Every issue declares a `scope:` list of file paths/globs the worker may touch. Mirror it into the brief's **Changes required** / **DO NOT** so Codex sees it. Bad scope (`src/**`) means the issue is too big — split it.
> Honest note for our pipeline: unlike the legacy `/goal` path (which hard-rejected out-of-scope diffs), this skill's driver **surfaces the diffstat for human judgment** (Gate 3) rather than auto-rejecting. So scope discipline here is enforced by a tight brief + the operator reading the diffstat, not a hard gate. Write scope tightly anyway.

### Rule 4 — Acceptance criteria must be machine-verifiable
Every AC carries a `verify:` command that runs **inside the worktree** with the project's real commands. Examples:

| AC | `verify:` |
|---|---|
| "Version endpoint returns the SHA" | `dotnet test backend-dotnet/ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~VersionFunctionTests` |
| "TypeScript compiles" | `npm run typecheck` |
| "No new lint errors" | `npm run lint` |
| "Migration file exists" | `test -f backend-dotnet/.../Migrations/*_Thing.cs` |

**Reject** non-mechanical ACs — rewrite or drop them: "follows project style" → `npm run lint`; "handles edge cases" → enumerate each as its own AC + test; "performance is acceptable" → a measurable threshold + benchmark command, or drop; "looks good" → drop.

### Rule 5 — Dependencies form a DAG
No cycles (A↔B means the split is wrong — merge or re-split). Encode the topological order **directly as `queue.tsv` row order** (dependency-first) and the **`DEPS`** column (comma-separated issue numbers, `-` = none). A dependent **defers** until its deps are merged into `$BASE`.

### Rule 6 — Non-goals are explicit
Every issue lists what the worker must NOT do (don't refactor unrelated code, don't touch the README, don't add deps). Map these to the brief's **DO NOT** section.

### Rule 7 — Tier each issue
- **TIER-1** = a prerequisite others build on → `TIER=1, TIER1_MERGE=yes` (its branch is fast-merged into `$BASE` before dependents run, so they can build on it).
- Everything else → `TIER=2, TIER1_MERGE=no` (PR only, awaiting review).
Only issues with downstream dependents need to be tier-1.

### Rule 8 — Adaptive timeout
Set `TIMEOUT_MIN` per row deliberately: **~75** backend/feature, **~40** default, **~20** docs-only. A flat 40 killed 3 big features in v1 (exit 124). Codex commits incrementally, so a timeout loses only the uncommitted delta.

### Rule 9 — Project safety is inherited, never re-litigated
Every brief inherits the hard-safety block already enforced by `scripts/codex-brief.tmpl` + the driver: the `BANNED_TERMS` list (defined in `wave.conf` / the template) must never be ADDED under `BANNED_PATHS` (`app components public lib`); payments stay **sandbox-only** (never a live charge); no secret VALUES in tracked files (runtime env validation only); never push / open a PR / touch `main`; no deploy commands; no test-gutting (`@ts-ignore` / `eslint-disable` / loosened configs). You do not restate these per-issue — the template carries them — but your decomposition must **never produce an issue that requires violating them**.

### Rule 10 — Exclude owner-only work from the queue entirely
Real-charge / live-cutover / secret-provisioning items stay manual (e.g. a live-purchase verification, anything gated on `LAUNCH_CONFIRMED`, flipping a `*_LIVE_*` flag, writing a real secret). Do NOT create queue rows for them. List them in the checkpoint summary as **"excluded — owner-only"**.

---

## The brief artifact (one per issue)

For each issue write `$BRIEF_DIR/<TAG>-<slug>.md`. Its shape MUST match what `scripts/codex-brief.tmpl` expects — five sections:

- **Context** — what + why (1–3 sentences), with the real file anchors the worker should read first.
- **Constraints** — the project rules that bind THIS change (TFM, no new deps, naming, etc.).
- **Changes required** — the concrete edits, referencing the `scope:` paths.
- **Acceptance** — the machine-checkable `verify:` commands, one per line, runnable in the worktree.
- **DO NOT** — the non-goals + the standing "no push / no PR / never main".

Mini-example:
```markdown
# HARD-01: API load-test harness + atomic rate-limit assertions

## Context
... reads backend-dotnet/.../V1RewriteHttpFunctions.cs + ApiKeyRateLimiter.cs ...

## Constraints
- Match the test project's existing TFM. No new NuGet/npm deps. No secrets in source.

## Changes required
1. scripts/load-test/api-burst.mjs (Node, global fetch, --dry-run safe) ...
2. backend-dotnet/tests/.../ApiBurstRateLimitTests.cs ...

## Acceptance (machine-checkable)
- node scripts/load-test/api-burst.mjs --dry-run --url http://x --key rmv_test_x exits 0, sends nothing
- cd backend-dotnet && dotnet test ReplyInMyVoice.sln -c Release --filter FullyQualifiedName~ApiBurstRateLimitTests exits 0
- the project banned-term gate over changed files prints nothing

## DO NOT
- Do NOT call any live endpoint from a unit test. Do NOT push, open a PR, or touch main.
```

Name briefs so the `queue.tsv` `BRIEF_GLOB` column resolves them — the driver does `ls $BRIEF_DIR/$GLOB | head -1`, so `BRIEF_GLOB=<TAG>-*.md` is the safe convention.

---

## The queue artifact

Emit one **TAB-separated** `queue.tsv` row per issue (literal tabs, not spaces):

```
ISSUE␉TAG␉TIER␉TIER1_MERGE␉DEPS␉BRIEF_GLOB␉TIMEOUT_MIN
```

Dependency-first order. Copy `queue.example.tsv` for the column legend. `preflight.sh` validates every row parses **and** every `BRIEF_GLOB` resolves — so **write the briefs before preflight**. Generate the file with `printf '%s\t%s\t...\n'` (or copy the example and edit), then verify with `awk -F'\t' '{print NF}'` that every row has 7 fields.

---

## Procedure

1. Read the requirement (read the file/link if it is a path).
2. Survey the repo: `ls` root; read `README` / `AGENTS.md` / `CLAUDE.md`; identify the package manager + the real `test` / `lint` / `typecheck` / `build` commands — store them (they become the `verify:` commands).
3. Sketch the issue list **internally first** (don't create on GitHub yet). For each: title, scope (paths), ACs + verify, non-goals, deps, tier, timeout.
4. Self-check the draft against Rules 1–10. Fix every violation.
5. Create the issues with `gh issue create` (issue body = Context / Scope / AC+verify / Non-goals / Deps). Record the returned numbers. **Do NOT rely on labels** — the driver keys off `queue.tsv` + `done/<n>` markers + open-PR state, not labels (it only ADDS `blocked` on failure). A cosmetic label is harmless but never load-bearing.
6. Write each brief under `$BRIEF_DIR`; write the `queue.tsv` rows.
7. **Output the checkpoint summary and STOP** (template below).

> **STOP HERE.** This is the only human checkpoint. Do not run `preflight.sh` / `start.sh` until explicit approval.

### Checkpoint summary template
```
Decomposed <requirement> into N issues (dependency order):
  #NNN  TAG     title                                  [tier1, no deps]
  #NNN  TAG     title                                  [tier2, depends: #NNN]
Briefs:            $BRIEF_DIR/<...>.md   (one per issue)
Queue:             $CONTROL_DIR/queue.tsv
Integration base:  $BASE   (never main)
Excluded (owner-only): <list, or none>
Gates on every PR: dotnet test / npm typecheck+test (by diff) + diff-scoped banned-term + secret/suppression scan.

Reply "go" / "approved" / "proceed" to run preflight + start the unattended wave,
or tell me what to change (split / merge / drop / retier / re-time / fix a brief).
```

### Handling the approval response
| User says | Action |
|---|---|
| "go" / "approved" / "proceed" / clear affirmative | Proceed to Phase 2: confirm `wave.conf`, run `preflight.sh`, then `start.sh`. |
| "change #N …" | Edit the issue + its brief, re-show summary, wait. |
| "split #N" | Re-decompose #N (close/repurpose original), rewrite briefs + queue rows, re-show, wait. |
| "merge #N and #M" | Combine into one issue + brief + row, re-show, wait. |
| "drop #N" | Remove its queue row + brief, close the issue `wontfix`, re-show, wait. |
| "retier #N" / "timeout #N=90" | Edit the queue row only, re-show, wait. |
| "abort" / "cancel" | Close created issues `wontfix`, delete draft briefs/queue, exit, report what was discarded. |
| anything ambiguous | Ask ONE short clarifying question, then wait. |

Do not interpret silence — or "looks interesting" / "I'll review later" — as approval. Only an explicit affirmative starts the wave.

---

## Failure modes for this phase
- **Requirement too vague** — can't write concrete ACs → stop and ask. Don't invent ACs.
- **Out of scope** — asks for a deploy, a merge to main, prod data, or a live charge → report + exclude (Rule 10); never queue it.
- **Can't find test commands** — don't guess `npm test` in a .NET dir → ask, or read `AGENTS.md` / CI workflows for the real commands.

---

## Borrowed vs changed (lineage, for maintainers)
- **Borrowed verbatim from `delivery-pipeline/references/decompose.md`:** Rules 1–6 (mergeable unit, size, scope-as-paths, machine-verifiable AC + reject-list, DAG, non-goals); the one-checkpoint / no-silence-as-approval discipline; the vague / out-of-scope / no-test-commands failure modes.
- **Changed for this skill's daemon path:** emit **briefs (`codex-brief.tmpl` shape) + `queue.tsv`** (with tier, deps, adaptive timeout) instead of GitHub-label-driven issues; encode order/deps in `queue.tsv`, not just `Depends on:` prose; add **tiering**, **adaptive timeout**, **owner-only exclusion**; launch via **`preflight.sh` → `start.sh`** (detached daemon) instead of an in-session **`/goal`** loop (which burns context); scope is **surfaced for judgment** (diffstat), not hard-rejected; **no required labels**.
