import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

type BoardRow = {
  id: string;
  milestone: string;
  status: string;
};

function boardRows(): BoardRow[] {
  return readFileSync(join(root, "plans", "issue-board.md"), "utf8")
    .split("\n")
    .map((line) => line.trim())
    .filter((line) => /^\| M[0-9.]+-[0-9]+ \|/.test(line))
    .map((line) => {
      const [, id, milestone, , , status] = line
        .split("|")
        .map((cell) => cell.trim());
      return { id, milestone, status };
    });
}

describe("overnight supervisor status taxonomy", () => {
  it("reserves BLOCKED-WAITING-USER for issues that require an external user action", () => {
    const userBlocked = boardRows().filter((row) =>
      row.status.startsWith("BLOCKED-WAITING-USER"),
    );

    expect(userBlocked.map((row) => row.id).sort()).toEqual([
      "M7-001",
      "M9-006",
    ]);
  });

  it("does not label autonomous prerequisite or coordination work as waiting on the user", () => {
    const rows = boardRows();
    const internallyBlocked = rows.filter((row) =>
      ["M1-Entra", "M2-Quality", "M3-V2"].includes(row.milestone),
    );

    expect(internallyBlocked).not.toContainEqual(
      expect.objectContaining({
        status: expect.stringMatching(/^BLOCKED-WAITING-USER/),
      }),
    );
  });
});
