import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

import { GET, revalidate } from "../../app/api/v1/openapi.json/route";

const root = process.cwd();

describe("/api/v1/openapi.json route", () => {
  it("serves the public OpenAPI document as cacheable JSON", async () => {
    const expected = JSON.parse(
      readFileSync(join(root, "public/openapi.json"), "utf8"),
    );

    const response = await GET();

    await expect(response.json()).resolves.toEqual(expected);
    expect(response.status).toBe(200);
    expect(response.headers.get("Content-Type")).toContain("application/json");
    expect(response.headers.get("Cache-Control")).toContain("public");
    expect(revalidate).toBe(3600);
  });
});
