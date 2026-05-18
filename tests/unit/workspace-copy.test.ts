import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const workspaceSource = readFileSync(
  new URL("../../components/app/rewrite-workspace.tsx", import.meta.url),
  "utf8",
);

describe("workspace V2 surface copy", () => {
  it("removes quick context and keeps only the broad scenario workflow", () => {
    expect(workspaceSource).not.toContain("Quick context");
    expect(workspaceSource).not.toContain("Audience");
    expect(workspaceSource).not.toContain("Purpose");
    expect(workspaceSource).toContain("Blank / custom");
    expect(workspaceSource).toContain("Email or message reply");
    expect(workspaceSource).toContain("Customer support");
    expect(workspaceSource).toContain("Cover letter");
    expect(workspaceSource).toContain("Work update");
  });

  it("renders tone choices from the reduced preset list", () => {
    expect(workspaceSource).toContain("tonePresetOptions.map");
    expect(workspaceSource).not.toContain("Firm but polite");
    expect(workspaceSource).not.toContain("Apologetic");
  });
});
