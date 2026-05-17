import { describe, expect, it } from "vitest";

import { isAllowedOrigin } from "../../lib/security";

describe("isAllowedOrigin", () => {
  it("allows the configured app origin", () => {
    expect(
      isAllowedOrigin("https://replyinmyvoice.com", {
        appUrl: "https://replyinmyvoice.com",
        nodeEnv: "production",
      }),
    ).toBe(true);
  });

  it("allows localhost during development", () => {
    expect(
      isAllowedOrigin("http://localhost:3000", {
        appUrl: "https://replyinmyvoice.com",
        nodeEnv: "development",
      }),
    ).toBe(true);
  });

  it("rejects cross-site production origins", () => {
    expect(
      isAllowedOrigin("https://example.com", {
        appUrl: "https://replyinmyvoice.com",
        nodeEnv: "production",
      }),
    ).toBe(false);
  });

  it("rejects missing production origins", () => {
    expect(
      isAllowedOrigin(null, {
        appUrl: "https://replyinmyvoice.com",
        nodeEnv: "production",
      }),
    ).toBe(false);
  });
});
