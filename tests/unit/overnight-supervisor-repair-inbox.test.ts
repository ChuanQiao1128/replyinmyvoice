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
});
