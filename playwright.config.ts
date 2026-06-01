import { defineConfig, devices } from "@playwright/test";

const paymentMockApiPort = process.env.PAYMENT_E2E_MOCK_API_PORT ?? "43183";
const paymentMockApiUrl =
  process.env.PAYMENT_E2E_MOCK_API_URL ??
  `http://127.0.0.1:${paymentMockApiPort}`;

function envWith(overrides: Record<string, string>) {
  return Object.fromEntries(
    Object.entries({ ...process.env, ...overrides }).filter(
      (entry): entry is [string, string] => typeof entry[1] === "string",
    ),
  );
}

export default defineConfig({
  testDir: "./tests/e2e",
  timeout: 30_000,
  use: {
    baseURL: "http://127.0.0.1:3000",
    trace: "on-first-retry",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: [
    {
      command: "node --import tsx tests/e2e/payment-flow-mock-api.ts",
      env: envWith({
        PAYMENT_E2E_MOCK_API_PORT: paymentMockApiPort,
      }),
      url: `${paymentMockApiUrl}/__health`,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
    {
      command: "npm run dev",
      env: envWith({
        ADMIN_EMAILS: "admin@example.test",
        AUTH_SESSION_SECRET: "playwright-admin-session-secret",
        NEXT_PUBLIC_AZURE_API_BASE_URL: paymentMockApiUrl,
        STRIPE_PRICE_QUICK_PACK_NZD: "price_test_quick_pack",
        WATCHPACK_POLLING: "true",
      }),
      url: "http://127.0.0.1:3000",
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
  ],
});
