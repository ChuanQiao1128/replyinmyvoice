import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();
const source = (path: string) => readFileSync(join(root, path), "utf8");

describe("MCP package version", () => {
  it("bumps the npm package metadata to the next patch version", () => {
    const packageJson = JSON.parse(
      source("packages/mcp-server/package.json"),
    ) as { version: string };

    expect(packageJson.version).toBe("0.1.3");
  });

  it("uses the shared MCP server version in local and remote servers", () => {
    expect(source("packages/mcp-server/src/index.ts")).toContain("MCP_SERVER_VERSION");
    expect(source("app/api/mcp/route.ts")).toContain("MCP_SERVER_VERSION");
  });
});
