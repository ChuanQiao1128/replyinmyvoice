import { defineConfig } from "vitest/config";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

// Repo root (same anchor as tsconfig's "@/*" -> "./*"), so unit tests can import
// via "@/..." instead of brittle "../../" relatives. Vite/Rollup string-alias
// matching only fires on "@/..." (not "@scope/pkg"), so npm scoped packages are safe.
const root = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  cacheDir: ".vite-cache",
  resolve: {
    alias: {
      "@": resolve(root),
    },
  },
  test: {
    include: ["tests/unit/**/*.test.ts"],
    exclude: ["tests/e2e/**", "node_modules/**"],
  },
});
