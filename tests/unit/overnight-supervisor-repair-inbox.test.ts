import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function supervisorScript(): string {
  return readFileSync(
    join(root, "plans", "overnight-supervisor.sh"),
    "utf8",
  );
}

function implementationPrompt(): string {
  return readFileSync(
    join(root, "plans", "codex-implementation-prompt.md"),
    "utf8",
  );
}

describe("overnight supervisor repair inbox orchestration", () => {
  it("checks the Codex repair inbox before selecting the next issue-board item", () => {
    const script = supervisorScript();

    expect(script).toContain("INBOX=plans/codex-worker-inbox.md");
    expect(script).toContain("process_repair_inbox_once()");

    const repairCheck = script.indexOf("if process_repair_inbox_once; then");
    const issueSelection = script.indexOf("NEXT=$(find_next_pending_issue)");

    expect(repairCheck).toBeGreaterThanOrEqual(0);
    expect(issueSelection).toBeGreaterThanOrEqual(0);
    expect(repairCheck).toBeLessThan(issueSelection);
  });

  it("queues non-user Codex failures into the repair inbox", () => {
    const script = supervisorScript();

    expect(script).toContain("append_repair_inbox_item");
    expect(script).toContain("codex-no-status");
    expect(script).toContain("codex-needs-human:${BLOCK_STATUS}");
    expect(script).toContain("ci-failed");
    expect(script).toContain('[ "$BLOCK_STATUS" != "BLOCKED-WAITING-USER" ]');
  });

  it("blocks oversized frontend redesign before launching Codex", () => {
    const script = supervisorScript();

    const guard = script.indexOf("M4-011)");
    const taskBuild = script.indexOf("# Build current-task.md from manifest");

    expect(guard).toBeGreaterThanOrEqual(0);
    expect(taskBuild).toBeGreaterThanOrEqual(0);
    expect(guard).toBeLessThan(taskBuild);

    const guardedBlock = script.slice(guard, taskBuild);
    expect(guardedBlock).toContain('update_board_status "$ID" "BLOCKED-AUTONOMY"');
    expect(guardedBlock).toContain("plans/frontend-redesign-followups.md");
    expect(guardedBlock).toContain("append_decision");
  });

  it("requires Codex to write status before editing work that exceeds the timebox", () => {
    const prompt = implementationPrompt();

    expect(prompt).toContain("Timebox preflight");
    expect(prompt).toContain("before editing");
    expect(prompt).toContain('next_action": "needs_human"');
    expect(prompt).toContain("plans/frontend-redesign-followups.md");
  });

  it("preserves no-status work before returning to main", () => {
    const script = supervisorScript();

    const repairNoStatus = script.indexOf(
      "ERROR: repair codex did not produce task-status.json",
    );
    const repairStatusRead = script.indexOf(
      "next_action=$(parse_status_field next_action)",
    );
    const issueNoStatus = script.indexOf(
      "ERROR: codex did not produce task-status.json",
    );
    const issueStatusRead = script.indexOf(
      "NEXT_ACTION=$(parse_status_field next_action)",
    );

    expect(script.slice(repairNoStatus, repairStatusRead)).toContain(
      'git stash push -u -m "repair-no-status-${id}-$(date +%s)"',
    );
    expect(script.slice(issueNoStatus, issueStatusRead)).toContain(
      'git stash push -u -m "no-status-${ID}-$(date +%s)"',
    );
  });

  it("creates issue branches before writing current-task and board state", () => {
    const script = supervisorScript();

    const branchCreate = script.indexOf('git checkout -b "$BRANCH"');
    const taskBuild = script.indexOf(
      "Build current-task.md from manifest or detailed brief on the issue branch",
    );
    const markInProgress = script.indexOf('update_board_status "$ID" "in_progress"');
    const codexRun = script.indexOf('run_codex_implementation "$ID"');

    expect(branchCreate).toBeGreaterThanOrEqual(0);
    expect(taskBuild).toBeGreaterThan(branchCreate);
    expect(markInProgress).toBeGreaterThan(taskBuild);
    expect(codexRun).toBeGreaterThan(markInProgress);
  });

  it("blocks ready_to_commit when dirty files are outside files_changed", () => {
    const script = supervisorScript();

    expect(script).toContain("verify_status_declares_all_changes");
    expect(script).toContain("files_changed");
    expect(script).toContain("undeclared-files-in-diff");
    expect(script).toContain("repair changed files outside");

    const repairGuard = script.indexOf(
      "repair changed files outside plans/task-status.json files_changed",
    );
    const repairAdd = script.indexOf("stage declared files && commit repair");
    const issueGuard = script.indexOf(
      "changed files outside plans/task-status.json files_changed",
    );
    const issueAdd = script.indexOf("stage declared files && commit");

    expect(repairGuard).toBeGreaterThanOrEqual(0);
    expect(repairGuard).toBeLessThan(repairAdd);
    expect(issueGuard).toBeGreaterThanOrEqual(0);
    expect(issueGuard).toBeLessThan(issueAdd);
  });

  it("ignores supervisor runtime ledgers when checking declared files", () => {
    const script = supervisorScript();

    expect(script).toContain("SUPERVISOR_RUNTIME_FILES");
    expect(script).toContain('"plans/current-task.md"');
    expect(script).toContain('"plans/issue-board.md"');
    expect(script).toContain('"plans/overnight-progress.md"');
    expect(script).toContain('"plans/codex-worker-inbox.md"');
    expect(script).toContain("is_supervisor_runtime_file(path)");
  });

  it("stages only files declared by task-status before committing", () => {
    const script = supervisorScript();

    expect(script).toContain("stage_declared_changes()");
    expect(script).toContain("paths = list(dict.fromkeys([*declared, *sys.argv[1:]]))");
    expect(script).toContain('subprocess.run(["git", "add", "--", path], check=True)');
    expect(script).toContain(
      "stage_declared_changes plans/codex-worker-inbox.md plans/current-task.md plans/current-repair-meta.json",
    );
    expect(script).toContain(
      "stage_declared_changes plans/current-task.md plans/decisions-log.md plans/issue-board.md",
    );
    expect(script).toContain("stage declared files && commit repair");
    expect(script).toContain("stage declared files && commit");
    expect(script).not.toContain("git add -A");
  });

  it("stashes needs_human and abort diffs before returning to main", () => {
    const script = supervisorScript();

    expect(script).toContain('stash_dirty_worktree "needs-human-${ID}"');
    expect(script).toContain('stash_dirty_worktree "abort-${ID}"');
    expect(script).toContain('stash_dirty_worktree "repair-needs-human-${id}"');
    expect(script).toContain('stash_dirty_worktree "repair-abort-${id}"');
  });

  it("clears stale git index locks before stash operations", () => {
    const script = supervisorScript();

    expect(script).toContain("clear_stale_git_index_lock()");
    expect(script).toContain("rm -f .git/index.lock");
    expect(script).toContain("Git index: removing stale .git/index.lock");

    const stashFunction = script.indexOf("stash_dirty_worktree()");
    const stashPush = script.indexOf("git stash push", stashFunction);
    const lockClear = script.indexOf("clear_stale_git_index_lock || return 1", stashFunction);

    expect(stashFunction).toBeGreaterThanOrEqual(0);
    expect(lockClear).toBeGreaterThan(stashFunction);
    expect(stashPush).toBeGreaterThan(lockClear);
  });
});
