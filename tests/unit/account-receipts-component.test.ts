import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { PurchaseHistorySection } from "../../components/account/account-panel";
import type { AzureAccountPayment } from "../../lib/azure-api";

describe("PurchaseHistorySection", () => {
  it("renders purchase details with a Stripe receipt link", () => {
    const payments: AzureAccountPayment[] = [
      {
        amount: 250,
        currency: "nzd",
        date: "2026-05-30T10:00:00Z",
        expiry: "2026-08-30T10:00:00Z",
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
    expect(html).toContain(
      'href="https://payments.example.test/receipt/quick-pack"',
    );
    expect(html).toContain("View receipt");
  });
});
