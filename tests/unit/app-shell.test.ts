import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

import {
  isNavItemActive,
  visibleNavGroups,
} from "../../components/app/shell/shell-types";

const root = process.cwd();
const source = (path: string) => readFileSync(join(root, path), "utf8");

describe("app shell nav model", () => {
  it("hides the developers group from consumers", () => {
    const ids = visibleNavGroups(false, false).map((group) => group.id);
    expect(ids).toContain("create");
    expect(ids).toContain("account");
    expect(ids).not.toContain("developers");
  });

  it("shows the developers group for Pro/API tiers", () => {
    expect(visibleNavGroups(true, false).map((group) => group.id)).toContain(
      "developers",
    );
  });

  it("shows the developers group when developer mode is on", () => {
    expect(visibleNavGroups(false, true).map((group) => group.id)).toContain(
      "developers",
    );
  });

  it("marks /app active only on exact match", () => {
    expect(isNavItemActive("/app", "/app")).toBe(true);
    expect(isNavItemActive("/app/history", "/app")).toBe(false);
    expect(isNavItemActive("/app/history", "/app/history")).toBe(true);
  });
});

describe("app shell wiring", () => {
  it("gates auth and renders the shell in the app layout", () => {
    const layout = source("app/app/layout.tsx");
    expect(layout).toContain("fetchAzureAccountSummary");
    expect(layout).toContain('redirect("/sign-in")');
    expect(layout).toContain("<AppShell");
    expect(layout).toContain("devModeDefault");
  });

  it("hides the marketing footer on the app shell and admin", () => {
    const gate = source("components/chrome-gate.tsx");
    expect(gate).toContain('"/app"');
    expect(gate).toContain('"/admin"');
  });

  it("surfaces server-backed history through a same-origin proxy", () => {
    const proxy = source("app/api/me/rewrites/route.ts");
    expect(proxy).toContain("/api/me/rewrites");
    expect(proxy).toContain("requireSameOrigin");

    const ui = source("components/app/history-list.tsx");
    expect(ui).toContain('"/api/me/rewrites?page=1&pageSize=20"');
    expect(ui).toContain('method: "DELETE"');
  });
});
