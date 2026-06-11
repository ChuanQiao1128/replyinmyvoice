import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

import {
  isDeveloperTierStatus,
  isNavItemActive,
  planLabelForStatus,
  SHELL_NAV,
} from "../../components/app/shell/shell-types";

const root = process.cwd();
const source = (path: string) => readFileSync(join(root, path), "utf8");

describe("app shell nav model", () => {
  it("shows every group to every signed-in user", () => {
    const ids = SHELL_NAV.map((group) => group.id);
    expect(ids).toEqual(["create", "developers", "account"]);
  });

  it("keeps developer features visible in the sidebar nav", () => {
    const developers = SHELL_NAV.find((group) => group.id === "developers");
    expect(developers?.items.map((item) => item.href)).toEqual([
      "/app/keys",
      "/app/usage",
      "/app/connect",
    ]);
  });

  it("marks /app active only on exact match", () => {
    expect(isNavItemActive("/app", "/app")).toBe(true);
    expect(isNavItemActive("/app/history", "/app")).toBe(false);
    expect(isNavItemActive("/app/history", "/app/history")).toBe(true);
  });

  it("treats only paid subscriptions as developer tier", () => {
    expect(isDeveloperTierStatus("active")).toBe(true);
    expect(isDeveloperTierStatus("Trialing")).toBe(true);
    expect(isDeveloperTierStatus("inactive")).toBe(false);
    expect(isDeveloperTierStatus("canceled")).toBe(false);
  });

  it("never labels a free account as Inactive", () => {
    expect(planLabelForStatus("inactive")).toBe("Free");
    expect(planLabelForStatus("canceled")).toBe("Free");
    expect(planLabelForStatus("active")).toBe("Pro/API");
    expect(planLabelForStatus("PastDue")).toBe("Payment issue");
  });
});

describe("app shell wiring", () => {
  it("gates auth and renders the shell in the app layout", () => {
    const layout = source("app/app/layout.tsx");
    expect(layout).toContain("fetchAzureAccountSummary");
    expect(layout).toContain("planLabelForStatus");
    expect(layout).toContain("planLabel: planLabelForStatus(account.subscriptionStatus)");
    expect(layout).toContain('redirect("/sign-in")');
    expect(layout).toContain("<AppShell");
  });

  it("passes the computed account plan label into the menu badge", () => {
    const shellUi = source("components/app/shell/app-shell.tsx");
    const accountMenu = source("components/app/shell/account-menu.tsx");

    expect(shellUi).toContain("planLabel={account.planLabel}");
    expect(accountMenu).toContain("planLabel");
    expect(accountMenu).toContain("{planLabel}");
    expect(accountMenu).not.toContain("Inactive");
  });

  it("upsells Pro/API on developer pages instead of erroring", () => {
    for (const page of [
      "app/app/keys/page.tsx",
      "app/app/usage/page.tsx",
      "app/app/connect/page.tsx",
    ]) {
      const code = source(page);
      expect(code).toContain("isDeveloperTierStatus");
      expect(code).toContain("DeveloperUpsell");
    }
    const upsell = source("components/app/shell/shell-primitives.tsx");
    expect(upsell).toContain('href="/pricing"');
    expect(upsell).toContain("NZ$19.90/mo");
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
    expect(ui).toContain("/api/me/rewrites?page=");
    expect(ui).toContain('method: "DELETE"');
  });

  it("keeps the rewrite page free of an embedded history list", () => {
    const workspace = source("components/app/rewrite-workspace.tsx");
    expect(workspace).not.toContain("RECENT REWRITES");
    expect(workspace).not.toContain("readLocalRewriteHistory");
    expect(workspace).not.toContain("writeLocalRewriteHistory");
  });
});
