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

  it("adds a signed-in portal page using the shared header", () => {
    expect(pageSource).toContain(
      'from "../../../components/developers/developer-dashboard"',
    );
    expect(pageSource).toContain('from "../../../components/site-header"');
    expect(pageSource).toContain('export const dynamic = "force-dynamic"');
    expect(pageSource).toContain("<SiteHeader");
    expect(pageSource).toContain("<DeveloperDashboard");
    expect(pageSource).not.toContain("getAzureApiBaseUrl");
    expect(pageSource).not.toContain("getCurrentAccessToken");
  });

  it("loads, creates, and revokes keys through the existing UI routes", () => {
    const panelSource = source("components/developers/api-keys-panel.tsx");

    expect(panelSource).toContain('fetch("/api/keys"');
    expect(panelSource).toContain('method: "POST"');
    expect(panelSource).toContain('method: "DELETE"');
    expect(panelSource).toContain("/rotate");
    expect(panelSource).toContain("encodeURIComponent(key.id)");
    expect(panelSource).toContain("maskedKey");
    expect(panelSource).toContain("lastUsedAt");
    expect(panelSource).toContain("last30dUsage");
    expect(panelSource).toContain("revokedAt");
    expect(panelSource).not.toContain("localStorage");
    expect(panelSource).not.toContain("sessionStorage");
  });

  it("keeps the one-time key reveal and revoke confirmation explicit", () => {
    const panelSource = source("components/developers/api-keys-panel.tsx");

    expect(panelSource).toContain("Create key");
    expect(panelSource).toContain("Copy key");
    expect(panelSource).toContain("you won't see this again");
    expect(panelSource).toContain("Rotate");
    expect(panelSource).toContain("Revoke");
    expect(panelSource).toContain("30-day calls");
    expect(panelSource).toContain("Confirm revoke");
    expect(panelSource).toContain("setRevealedKey(null)");
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
    expect(dashboardSource).toContain('import { UsagePanel } from "./usage-panel"');
    expect(dashboardSource).toContain('"keys"');
    expect(dashboardSource).toContain('"usage"');
    expect(dashboardSource).toContain('"billing"');
    expect(dashboardSource).toContain("Keys");
    expect(dashboardSource).toContain("Usage");
    expect(dashboardSource).toContain("Billing");
    expect(dashboardSource).toContain("<ApiKeysPanel");
    expect(dashboardSource).toContain("<UsagePanel");
  });

  it("loads usage data from the same-origin account API routes", () => {
    const usagePath = "components/developers/usage-panel.tsx";

    expect(existsSync(join(root, usagePath))).toBe(true);

    const usageSource = source(usagePath);

    expect(usageSource).toContain('"/api/me/api-usage/summary"');
    expect(usageSource).toContain('"/api/me/api-usage/series?days=30"');
    expect(usageSource).toContain('"/api/me/api-usage/recent?limit=50"');
    expect(usageSource).toContain('cache: "no-store"');
    expect(usageSource).toContain("Today");
    expect(usageSource).toContain("Yesterday");
    expect(usageSource).toContain("Month-to-date");
    expect(usageSource).toContain("No calls yet");
    expect(usageSource).toContain("remaining");
    expect(usageSource).toContain("periodEnd");
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
