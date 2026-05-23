# Phase 1 Smoke Test — Lane Dispatcher Dry Run

Status: **READY — pending Codex completion of `plans/codex-briefs/phase1-dispatcher.md`**
Authored: 2026-05-23
Authority: `plans/lane-architecture-decisions.md` §8 Phase 1 smoke

---

## Test contract (verbatim from §8)

> Dispatcher runs one iteration with registry populated, prints "selected lane: <X>, item: <id>" and exits without executing. Verify: lane selected matches manual expectation for that item.

## Expected behavior on the registry as drafted 2026-05-23T15:15+12:00

### Iteration 1 — full registry, M8-001 in_progress

Predicted output:

```text
selected lane: epic, item: M1-002
```

Reasoning trace (what the selector should compute and Claude should verify in the smoke output):

1. Build the candidate set per lane:
   - epic candidates: 43 items (M1-002..M1-010, M2-001..M2-009 minus M2-007 done, M2.5-007, M3-001..M3-008, M4-011, M4-015, M8-002..M8-016)
   - evidence candidates: 14 items (M2.5-002, M4-001, M6-001..M6-008, M7-001..M7-003, M7-008, M9-006)
   - repair candidates: 0 (no `plans/repair-queue.json` yet — selector skips this branch and logs a one-line note)
   - direct candidates: 1 item (M8-001) — but `M8-001.status == "in_progress"`, NOT pending → excluded. M5-003 and M9-002 are `brief_state=manifest-only` → excluded by the direct gate. So direct candidates = 0.
2. Priority order: epic > evidence > repair > direct. Epic wins.
3. Within epic, sort by lowest M-number then lowest id. Natural-sort order: `M1-002, M1-003, ..., M1-010, M2-001, ..., M2-009, M2.5-007, M3-001, ..., M3-008, M4-011, M4-015, M8-002, ..., M8-016`. First is M1-002.

### Iteration 2 — synthetic: mark M1-Entra cluster done

If the operator removes all M1-* items from the registry (or marks them `status=done`), re-running the selector should produce:

```text
selected lane: epic, item: M2-001
```

(M2-001 is the next epic by natural M-number order.)

### Iteration 3 — synthetic: mark all epics done

If all 43 epic items are removed/done, re-running should produce:

```text
selected lane: evidence, item: M2.5-002
```

(Evidence-lane natural-sort: M2.5-002, M4-001, M6-001..M6-008, M7-001..M7-003, M7-008, M9-006. M2.5-002 is lowest M-number among evidence items; M4-001 is next.)

### Iteration 4 — synthetic: drain to direct-eligible

If all epics and evidence are removed, AND M5-003 / M9-002 have been promoted to `brief_state=detailed` (i.e., the brief writer has produced a brief for them), AND M8-001 transitions from `in_progress` to `done`, the selector should choose direct:

```text
selected lane: direct, item: M5-003
```

(M5 < M9 by M-number; both direct-medium-detailed.)

### Iteration 5 — synthetic: empty everything

If no items match any lane's gate, selector should produce:

```text
selected lane: none, item: -
```

---

## Verification checklist (run by Claude after Codex returns)

```bash
cd /Users/qc/Desktop/CloudFlare

# 1. Syntax check
bash -n plans/overnight-supervisor.sh || echo "FAIL syntax"

# 2. Unit test
bash tests/supervisor/test-lane-dispatch.sh || echo "FAIL unit test"

# 3. Iteration 1 — live registry
ACTUAL=$(LANE_DISPATCH=1 plans/overnight-supervisor.sh --selector-dry-run 2>&1 | grep -E '^selected lane:' | head -1)
EXPECTED="selected lane: epic, item: M1-002"
if [ "$ACTUAL" = "$EXPECTED" ]; then
  echo "PASS iteration-1"
else
  echo "FAIL iteration-1: got '$ACTUAL' expected '$EXPECTED'"
fi

# 4. LANE_DISPATCH=0 default-path byte-equivalence
LANE_DISPATCH=0 plans/overnight-supervisor.sh --selector-dry-run 2>&1 | head -5 > /tmp/after.txt
git stash push plans/overnight-supervisor.sh
plans/overnight-supervisor.sh --selector-dry-run 2>&1 | head -5 > /tmp/before.txt 2>/dev/null || true
git stash pop
diff /tmp/before.txt /tmp/after.txt || echo "FAIL byte-equivalence with LANE_DISPATCH=0"

# 5. Banned-term scan
hits=$(grep -RniE "humanizer|bypass|undetect|detector|evade" plans/overnight-supervisor.sh tests/supervisor/ 2>/dev/null || true)
if [ -z "$hits" ]; then
  echo "PASS banned-term"
else
  echo "FAIL banned-term: $hits"
fi

# 6. Diff scope
files_changed=$(git diff --name-only)
expected_set="plans/overnight-supervisor.sh
tests/supervisor/test-lane-dispatch.sh"
if [ "$files_changed" = "$expected_set" ] || [ "$files_changed" = "tests/supervisor/test-lane-dispatch.sh
plans/overnight-supervisor.sh" ]; then
  echo "PASS scope"
else
  echo "FAIL scope: $files_changed"
fi
```

All six checks must pass (PASS, no FAIL) for Phase 1 smoke = green.

---

## What "green" unlocks

- Phase 2 (direct-lane hardening) becomes the next session's work.
- The brief writer for M5-003 / M9-002 (`brief_state=manifest-only` → `detailed`) becomes the Phase 2 entry point.
- `LANE_DISPATCH=1` may stay opt-in until Phase 2 ships; the production overnight loop continues with the existing direct-only path.

## What "red" means

- Any FAIL above → Phase 1 not green. Codex retries up to 2× per the active sprint failure-handling policy. After 3rd failure: revert + escalate per `plans/codex-briefs/phase1-dispatcher.md` Rollback.
- Iteration-1 failure with the actual line printed in a different format is a Codex bug — re-brief with the exact expected format.
- Iteration-1 failure with a different item id is a selector logic bug — likely sort order or gate evaluation. Re-brief with the sort-order section copied verbatim from §7 of the decisions doc.
- Byte-equivalence failure means the new code accidentally executes in the default path — that is the only failure mode that risks affecting the live overnight loop, so highest-priority fix.

## Manual expectation override

If after running the smoke the user disagrees with `M1-002` as the first selection (e.g., wants M2.5-002 first because the LearningOps baseline is a closer launch dependency than the Entra migration), the override path is to either:

- Edit `plans/lane-architecture-decisions.md` §7 priority order via Amendment procedure (§12) — would change `epic > evidence > repair > direct` to something else.
- OR edit `plans/loop-registry.json` to move the lower-priority epic items to `status=blocked-by-user-deferral` so they fall out of the candidate set.

Either change should be made BEFORE flipping `LANE_DISPATCH=1` in the live overnight loop, not after.
