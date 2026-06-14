import { describe, expect, it } from "vitest";

import nextConfig from "../../next.config";

describe("next.config security headers", () => {
  it("applies baseline security headers to every route", async () => {
    const headers = await nextConfig.headers!();
    const allRoutes = headers.find((entry) => entry.source === "/(.*)");

    expect(allRoutes).toBeDefined();
    expect(allRoutes!.headers).toContainEqual({
      key: "X-Content-Type-Options",
      value: "nosniff",
    });
    expect(allRoutes!.headers).toContainEqual({
      key: "Referrer-Policy",
      value: "strict-origin-when-cross-origin",
    });
    expect(allRoutes!.headers).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          key: "Permissions-Policy",
          value: expect.stringContaining("camera=()"),
        }),
        expect.objectContaining({
          key: "Content-Security-Policy-Report-Only",
          value: expect.stringContaining("default-src 'self'"),
        }),
      ]),
    );
  });

  it("never sets an enforcing CSP header", async () => {
    const headers = await nextConfig.headers!();

    expect(headers.flatMap((entry) => entry.headers).map((header) => header.key)).not.toContain(
      "Content-Security-Policy",
    );
  });
});
