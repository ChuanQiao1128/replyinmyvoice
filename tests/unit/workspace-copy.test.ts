import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const workspaceSource = readFileSync(
  new URL("../../components/app/rewrite-workspace.tsx", import.meta.url),
  "utf8",
);

describe("workspace V2 surface copy", () => {
  it("removes quick context and keeps only the broad scenario workflow", () => {
    expect(workspaceSource).not.toContain("Quick context");
    expect(workspaceSource).not.toContain("scenarioOptions");
    expect(workspaceSource).not.toContain("Blank / custom");
    expect(workspaceSource).not.toContain("Email or message reply");
    expect(workspaceSource).not.toContain("Customer support");
    expect(workspaceSource).not.toContain("Cover letter");
    expect(workspaceSource).not.toContain("Work update");
    expect(workspaceSource).not.toContain("Audience");
    expect(workspaceSource).not.toContain("Purpose");
    expect(workspaceSource).toContain("Context or message");
    expect(workspaceSource).toContain("Draft to rewrite");
  });

  it("renders tone choices from the reduced preset list", () => {
    expect(workspaceSource).toContain("tonePresetOptions.map");
    expect(workspaceSource).not.toContain("Firm but polite");
    expect(workspaceSource).not.toContain("Apologetic");
  });

  it("has a safe failure state when the signal does not improve", () => {
    expect(workspaceSource).toContain("Still high AI-like signal");
    expect(workspaceSource).toContain("We could not produce a better version yet");
  });
});
