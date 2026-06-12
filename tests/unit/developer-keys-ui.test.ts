import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

describe("developer key management UI source", () => {
  const pageSource = source("app/developers/keys/page.tsx");
  const headerSource = source("components/site-header.tsx");

  it("hosts the developer console inside the app shell", () => {
    const keysPage = source("app/app/keys/page.tsx");
    expect(keysPage).toContain('from "next/link"');
    expect(keysPage).toContain(
      'from "../../../components/developers/developer-dashboard"',
    );
    expect(keysPage).toContain('from "../../../lib/azure-api"');
    expect(keysPage).toContain('export const dynamic = "force-dynamic"');
    expect(keysPage).toContain('href="/developers/api"');
    expect(keysPage).toContain("API docs");
    expect(keysPage).toContain("fetchAzureAccountSummary");
    expect(keysPage).toContain("<DeveloperDashboard");
    expect(keysPage).toContain('initialTab="keys"');
    expect(keysPage).toContain("paymentGraceEndsAt=");
    expect(keysPage).toContain("subscriptionStatus=");

    // The old /developers/keys route now permanently redirects into the shell.
    expect(pageSource).toContain('redirect("/app/keys")');
  });

  it("loads, creates, and revokes keys through the existing UI routes", () => {
    const panelSource = source("components/developers/api-keys-panel.tsx");

    expect(panelSource).toContain('fetch("/api/keys"');
    expect(panelSource).toContain('method: "POST"');
    expect(panelSource).toContain('method: "DELETE"');
    expect(panelSource).toContain("/rotate");
    expect(panelSource).toContain("/webhook");
    expect(panelSource).toContain("encodeURIComponent(key.id)");
    expect(panelSource).toContain("maskedKey");
    expect(panelSource).toContain("lastUsedAt");
    expect(panelSource).toContain("Last used");
    expect(panelSource).toContain('formatDateTime(key.lastUsedAt, "Never")');
    expect(panelSource).toContain("last30dUsage");
    expect(panelSource).toContain("webhookUrl");
    expect(panelSource).toContain("revokedAt");
    expect(panelSource).toContain("isTest");
    expect(panelSource).toContain("apiKey.isTest");
    expect(panelSource).toContain('aria-label="Test key"');
    expect(panelSource).not.toContain("test: isTest");
    expect(panelSource).not.toContain("test: true");
    expect(panelSource).not.toContain("localStorage");
    expect(panelSource).not.toContain("sessionStorage");
  });

  it("keeps the one-time key reveal and revoke confirmation explicit", () => {
    const panelSource = source("components/developers/api-keys-panel.tsx");

    expect(panelSource).toContain("Create key");
    expect(panelSource).not.toContain("Create test key");
    expect(panelSource).not.toContain("submitCreateKey(true)");
    expect(panelSource).toContain("Copy key");
    expect(panelSource).toContain("you won't see this again");
    expect(panelSource).toContain("Rotate");
    expect(panelSource).toContain("Revoke");
    expect(panelSource).toContain("Webhook URL");
    expect(panelSource).toContain("Copy signing secret");
    expect(panelSource).toContain("Clear webhook");
    expect(panelSource).toContain("30-day calls");
    expect(panelSource).toContain('"/api/me/api-usage/summary"');
    expect(panelSource).toContain("Remaining credits");
    expect(panelSource).toContain('href="/pricing"');
    expect(panelSource).toContain("Buy credits");
    expect(panelSource).toContain("Confirm revoke");
    expect(panelSource).toContain("setRevealedKey(null)");
  });

  it("points the key manager API reference card at the API reference", () => {
    const panelSource = source("components/developers/api-keys-panel.tsx");

    expect(panelSource).toContain("API reference");
    expect(panelSource).toContain('href="/developers/api"');
    expect(panelSource).not.toContain('href="/developers"');
  });

  it("keeps gated API key access connected to the API docs", () => {
    const shellPrimitivesSource = source(
      "components/app/shell/shell-primitives.tsx",
    );

    expect(shellPrimitivesSource).toContain(
      "API & MCP access comes with Pro/API",
    );
    expect(shellPrimitivesSource).toContain("NZ$19.90/mo");
    expect(shellPrimitivesSource).toContain('href="/developers/api"');
    expect(shellPrimitivesSource).toContain("Read API docs");
  });

  it("wires signed-in navigation to the key manager", () => {
    expect(headerSource).toContain('href="/developers/keys"');
    expect(headerSource).toContain("API keys");
  });

  it("turns the key manager page into a developer dashboard with tabs", () => {
    const dashboardPath = "components/developers/developer-dashboard.tsx";

    expect(existsSync(join(root, dashboardPath))).toBe(true);

    const dashboardSource = source(dashboardPath);

    expect(dashboardSource).toContain('import { ApiKeysPanel } from "./api-keys-panel"');
    expect(dashboardSource).toContain('import { PastDueBanner } from "../app/past-due-banner"');
    expect(dashboardSource).toContain('import { UsagePanel } from "./usage-panel"');
    expect(dashboardSource).toContain('"keys"');
    expect(dashboardSource).toContain('"usage"');
    expect(dashboardSource).toContain('"billing"');
    expect(dashboardSource).toContain("Keys");
    expect(dashboardSource).toContain("Usage");
    expect(dashboardSource).toContain("Billing");
    expect(dashboardSource).toContain("<ApiKeysPanel");
    expect(dashboardSource).toContain("<UsagePanel");
    expect(dashboardSource).toContain("<PastDueBanner");
    expect(dashboardSource).toContain('subscriptionStatus === "PastDue"');
  });

  it("syncs developer dashboard tabs to stable app routes", () => {
    const dashboardSource = source("components/developers/developer-dashboard.tsx");

    expect(dashboardSource).toContain('import Link from "next/link"');
    expect(dashboardSource).toContain("usePathname");
    expect(dashboardSource).toContain("useSearchParams");
    expect(dashboardSource).toContain('href: "/app/keys"');
    expect(dashboardSource).toContain('href: "/app/usage"');
    expect(dashboardSource).toContain('href: "/app/keys?tab=billing"');
    expect(dashboardSource).not.toContain("useState<DashboardTab>");
    expect(dashboardSource).not.toContain("setActiveTab");
  });

  it("loads usage data from the same-origin account API routes", () => {
    const usagePath = "components/developers/usage-panel.tsx";

    expect(existsSync(join(root, usagePath))).toBe(true);

    const usageSource = source(usagePath);

    expect(usageSource).toContain('"/api/me/api-usage/summary"');
    expect(usageSource).toContain('"/api/me/api-usage/series?days=30"');
    expect(usageSource).toContain('"/api/me/api-usage/recent?limit=50"');
    expect(usageSource).toContain('"/api/me/api-usage/export?limit=1000"');
    expect(usageSource).toContain("downloadCsvFile");
    expect(usageSource).toContain('exportState.status === "loading"');
    expect(usageSource).toContain("Export CSV");
    expect(usageSource).not.toContain("download\n");
    expect(usageSource).toContain('cache: "no-store"');
    expect(usageSource).toContain("Today");
    expect(usageSource).toContain("Yesterday");
    expect(usageSource).toContain("Month-to-date");
    expect(usageSource).toContain("No calls yet");
    expect(usageSource).toContain("remaining");
    expect(usageSource).toContain("periodEnd");
    expect(usageSource).toContain('href="/developers/api"');
    expect(usageSource).toContain("API docs");
    expect(usageSource).toContain('href="/developers/api#errors"');
    expect(usageSource).toContain("What does this mean?");
    expect(usageSource).toContain("web and API rewrites share this balance");
    expect(usageSource).toContain("request id field");
  });

  it("keeps the usage chart dependency-free and accessible", () => {
    const chartPath = "components/developers/usage-bar-chart.tsx";

    expect(existsSync(join(root, chartPath))).toBe(true);

    const chartSource = source(chartPath);

    expect(chartSource).toContain("<svg");
    expect(chartSource).toContain("<title");
    expect(chartSource).toContain("aria-label");
    expect(chartSource).toContain("tabIndex={0}");
    expect(chartSource).toContain("onMouseEnter");
    expect(chartSource).toContain("onFocus");
    expect(chartSource).not.toContain("chart.js");
    expect(chartSource).not.toContain("recharts");
    expect(chartSource).not.toContain("d3");
  });
});
