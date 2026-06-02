import { defineConfig, devices } from "@playwright/test";

const azureMockUrl = "http://127.0.0.1:45934";
const appUrl = "http://127.0.0.1:3001";
const authSessionSecret = "playwright-auth-session-secret";

export default defineConfig({
  testDir: "./tests/e2e",
  timeout: 30_000,
  use: {
    baseURL: appUrl,
    trace: "on-first-retry",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"], channel: "chromium" },
    },
  ],
  webServer: {
    command: "npm run build && npx next start -H 127.0.0.1 -p 3001",
    env: {
      ...process.env,
      AUTH_SESSION_SECRET: process.env.AUTH_SESSION_SECRET ?? authSessionSecret,
      NEXT_DIST_DIR: process.env.NEXT_DIST_DIR ?? ".next-build",
      NEXT_PUBLIC_AZURE_API_BASE_URL:
        process.env.NEXT_PUBLIC_AZURE_API_BASE_URL ?? azureMockUrl,
    },
    url: appUrl,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
