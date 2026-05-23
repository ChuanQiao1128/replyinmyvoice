import { defineConfig } from "vitest/config";

export default defineConfig({
  cacheDir: ".vite-cache",
  test: {
    include: ["tests/unit/**/*.test.ts"],
    exclude: ["tests/e2e/**", "node_modules/**"],
  },
});
