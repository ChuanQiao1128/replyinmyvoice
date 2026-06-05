import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { BillingHistoryTable } from "../../components/developers/billing-panel";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

type BillingHistoryItem = {
  amount: number | null;
  currency: string | null;
  date: string;
  description: string | null;
  hostedInvoiceUrl?: string | null;
  receiptUrl?: string | null;
  status: string | null;
  type: string;
};

describe("developer billing panel", () => {
  it("is wired into the developer dashboard billing tab", () => {
    const panelPath = "components/developers/billing-panel.tsx";
    const routePath = "app/api/me/billing/history/route.ts";
    const dashboardSource = source("components/developers/developer-dashboard.tsx");

    expect(existsSync(join(root, panelPath))).toBe(true);
    expect(existsSync(join(root, routePath))).toBe(true);
    expect(dashboardSource).toContain('import { BillingPanel } from "./billing-panel"');
    expect(dashboardSource).toContain("<BillingPanel");
    expect(dashboardSource).not.toContain("will appear");
  });

  it("proxies billing history through the same-origin account endpoint", () => {
    const routePath = "app/api/me/billing/history/route.ts";

    expect(existsSync(join(root, routePath))).toBe(true);

    const routeSource = source(routePath);

    expect(routeSource).toContain("requireSameOrigin(request)");
    expect(routeSource).toContain("getCurrentAccessToken()");
    expect(routeSource).toContain('"/api/me/billing/history"');
    expect(routeSource).toContain("Authorization");
    expect(routeSource).toContain('cache: "no-store"');
  });

  it("loads billing data through same-origin account routes", () => {
    const panelSource = source("components/developers/billing-panel.tsx");

    expect(panelSource).toContain('"/api/me"');
    expect(panelSource).toContain('"/api/me/billing/history"');
    expect(panelSource).toContain('"/api/stripe/portal"');
    expect(panelSource).toContain('cache: "no-store"');
    expect(panelSource).not.toContain("getAzureApiBaseUrl");
    expect(panelSource).not.toContain("azureApiFetch");
    expect(panelSource).not.toContain('"/api/me/payments"');
    expect(panelSource).not.toContain('"/api/billing-support-requests"');
  });

  it("renders mocked pack, subscription, and refund billing history with receipts", () => {
    const history: BillingHistoryItem[] = [
      {
        amount: 250,
        currency: "nzd",
        date: "2026-06-01T09:00:00Z",
        description: "Quick Pack",
        receiptUrl: "https://payments.example.test/receipt/quick-pack",
        status: "paid",
        type: "pack",
      },
      {
        amount: 1990,
        currency: "nzd",
        date: "2026-06-02T09:00:00Z",
        description: "Pro/API invoice for June",
        hostedInvoiceUrl: "https://payments.example.test/invoice/pro-api",
        status: "open",
        type: "subscription",
      },
      {
        amount: -250,
        currency: "nzd",
        date: "2026-06-03T09:00:00Z",
        description: "Quick Pack refund",
        status: "succeeded",
        type: "refund",
      },
    ];

    const html = renderToStaticMarkup(
      createElement(BillingHistoryTable, { history }),
    );

    expect(html).toContain("Unified billing history");
    expect(html).toContain("Quick Pack");
    expect(html).toContain("Pro/API invoice for June");
    expect(html).toContain("Quick Pack refund");
    expect(html).toContain("NZD 2.50");
    expect(html).toContain("-NZD 2.50");
    expect(html).toContain("Pack");
    expect(html).toContain("Subscription");
    expect(html).toContain("Refund");
    expect(html).toContain("View receipt");
    expect(html).toContain("View invoice");
    expect(html).toContain(
      'href="https://payments.example.test/receipt/quick-pack"',
    );
    expect(html).toContain(
      'href="https://payments.example.test/invoice/pro-api"',
    );
  });

  it("shows an empty state when no billing records are returned", () => {
    const html = renderToStaticMarkup(
      createElement(BillingHistoryTable, { history: [] }),
    );

    expect(html).toContain("No billing records yet.");
  });
});
