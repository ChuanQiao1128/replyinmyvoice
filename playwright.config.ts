import { defineConfig, devices } from "@playwright/test";

const azureMockUrl = "http://127.0.0.1:45934";
const promoAppUrl = "http://127.0.0.1:3001";
const paymentAppUrl = "http://127.0.0.1:3000";
const promoAuthSessionSecret = "playwright-auth-session-secret";
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
  workers: 1,
  use: {
    trace: "on-first-retry",
  },
  projects: [
    {
      name: "chromium",
      testIgnore: [
        /.*admin-promo-codes\.spec\.ts/,
        /.*promo-full-loop\.spec\.ts/,
        /.*promo-redeem-ui\.spec\.ts/,
      ],
      use: {
        ...devices["Desktop Chrome"],
        baseURL: paymentAppUrl,
      },
    },
    {
      name: "promo-chromium",
      testMatch: [
        /.*admin-promo-codes\.spec\.ts/,
        /.*promo-full-loop\.spec\.ts/,
        /.*promo-redeem-ui\.spec\.ts/,
      ],
      use: {
        ...devices["Desktop Chrome"],
        baseURL: promoAppUrl,
      },
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
      url: paymentAppUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
    {
      command: "npm run build && npx next start -H 127.0.0.1 -p 3001",
      env: envWith({
        AUTH_SESSION_SECRET: promoAuthSessionSecret,
        NEXT_DIST_DIR: ".next-promo-build",
        NEXT_PUBLIC_AZURE_API_BASE_URL: azureMockUrl,
      }),
      url: promoAppUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
  ],
});
