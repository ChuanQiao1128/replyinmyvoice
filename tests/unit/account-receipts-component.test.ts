import React, { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  AccountPanel,
  PurchaseHistorySection,
} from "../../components/account/account-panel";
import type { AzureAccountPayment } from "../../lib/azure-api";

describe("PurchaseHistorySection", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-06-11T00:00:00Z"));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders purchase details with a Stripe receipt link", () => {
    const payments: AzureAccountPayment[] = [
      {
        amount: 250,
        currency: "nzd",
        date: "2026-06-01T00:00:00Z",
        expiry: null,
        paymentIntentId: "pi_quick_pack",
        receiptUrl: "https://payments.example.test/receipt/quick-pack",
        remaining: 9,
        sku: "quick_pack",
      },
    ];

    const html = renderToStaticMarkup(
      createElement(PurchaseHistorySection, { payments }),
    );

    expect(html).toContain("Purchase history");
    expect(html).toContain("Quick Pack");
    expect(html).toContain("NZD 2.50");
    expect(html).toContain("9 remaining");
    expect(html).toContain("Active");
    expect(html).toContain("Expires in 80 days");
    expect(html).toContain(
      'href="https://payments.example.test/receipt/quick-pack"',
    );
    expect(html).toContain("View receipt");
  });

  it("badges active, expiring, expired, and Pro renewal rows", () => {
    const payments: AzureAccountPayment[] = [
      {
        amount: 690,
        currency: "nzd",
        date: "2026-03-20T00:00:00Z",
        expiry: null,
        paymentIntentId: "pi_value_pack",
        receiptUrl: null,
        remaining: 4,
        sku: "value_pack",
      },
      {
        amount: 500,
        currency: "nzd",
        date: "2026-01-01T00:00:00Z",
        expiry: null,
        paymentIntentId: "pi_focus_pack",
        receiptUrl: null,
        remaining: 0,
        sku: "focus_pack",
      },
      {
        amount: 1990,
        currency: "nzd",
        date: "2026-06-01T00:00:00Z",
        expiry: null,
        paymentIntentId: "pi_pro_api",
        receiptUrl: null,
        remaining: 72,
        sku: "pro_api",
      },
    ];

    const html = renderToStaticMarkup(
      createElement(PurchaseHistorySection, {
        currentPeriodEnd: "2026-06-30T00:00:00Z",
        payments,
      }),
    );

    expect(html).toContain("Expires in 7 days");
    expect(html).toContain("Expired");
    expect(html).toContain("Next billing date");
    expect(html).toContain("Pro / API");
    expect(html).toContain("Jun 30, 2026");
  });
});

describe("AccountPanel", () => {
  it("renders a PastDue payment action card near the account summary", () => {
    vi.stubGlobal("React", React);

    type AccountPanelProps = NonNullable<Parameters<typeof AccountPanel>[0]>;
    const AccountPanelForTest =
      AccountPanel as React.ComponentType<AccountPanelProps>;
    const props = {
      demoBundle: {
        account: {
          currentPeriodEnd: "2026-06-30T00:00:00Z",
          email: "pastdue@example.test",
          externalAuthUserId: "external-pastdue-1",
          paymentGraceEndsAt: "2026-06-20T00:00:00Z",
          subscriptionStatus: "PastDue",
          usage: {
            exhausted: false,
            periodEnd: "2026-06-30T00:00:00Z",
            periodKey: "paid:2026-06",
            quota: 90,
            remaining: 72,
            reserved: 0,
            scope: "paid",
            used: 18,
          },
          userId: "user_pastdue",
        },
        payments: [],
        supportRequests: [],
      },
    } satisfies AccountPanelProps;

    const html = renderToStaticMarkup(
      createElement(AccountPanelForTest, props),
    );

    expect(html).toContain("Payment issue");
    expect(html).toContain("Payment failed");
    expect(html).toContain("update your payment method by");
    expect(html).toContain("Update payment method");
    expect(html.indexOf("Payment failed")).toBeLessThan(
      html.indexOf("Purchase history"),
    );

    vi.unstubAllGlobals();
  });

  it("shows the related pack name on billing support requests", () => {
    vi.stubGlobal("React", React);

    type AccountPanelProps = NonNullable<Parameters<typeof AccountPanel>[0]>;
    const AccountPanelForTest =
      AccountPanel as React.ComponentType<AccountPanelProps>;
    const props = {
      demoBundle: {
        account: {
          currentPeriodEnd: null,
          email: "buyer@example.test",
          externalAuthUserId: "external-buyer-1",
          paymentGraceEndsAt: null,
          subscriptionStatus: "inactive",
          usage: {
            exhausted: false,
            periodEnd: null,
            periodKey: "free:lifetime",
            quota: 3,
            remaining: 3,
            reserved: 0,
            scope: "free",
            used: 0,
          },
          userId: "user_buyer",
        },
        payments: [
          {
            amount: 250,
            currency: "nzd",
            date: "2026-06-01T00:00:00Z",
            expiry: null,
            paymentIntentId: "pi_quick_pack",
            receiptUrl: null,
            remaining: 9,
            sku: "quick_pack",
          },
        ],
        supportRequests: [
          {
            createdAt: "2026-06-02T00:00:00Z",
            id: "support-quick-pack",
            message: "Please check this purchase.",
            relatedPaymentIntentId: "pi_quick_pack",
            resolvedAt: null,
            status: "open",
            type: "billing-question",
            updatedAt: "2026-06-02T00:00:00Z",
            userId: "user_buyer",
          },
        ],
      },
    } satisfies AccountPanelProps;

    const html = renderToStaticMarkup(
      createElement(AccountPanelForTest, props),
    );
    const recentRequestsHtml = html.slice(html.indexOf("Recent requests"));

    expect(recentRequestsHtml).toContain("Quick Pack");
    expect(recentRequestsHtml).not.toContain("pi_quick_pack");

    vi.unstubAllGlobals();
  });
});
