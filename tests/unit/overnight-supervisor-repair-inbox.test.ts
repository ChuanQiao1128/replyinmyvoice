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
});
