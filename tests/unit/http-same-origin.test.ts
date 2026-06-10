import { afterEach, describe, expect, it, vi } from "vitest";

import { requireSameOrigin } from "../../lib/http";

function req(method: string, origin?: string) {
  const headers = new Headers();
  if (origin) {
    headers.set("origin", origin);
  }
  return new Request("https://replyinmyvoice.com/api/me", { method, headers });
}

describe("requireSameOrigin", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it("allows a same-origin GET with no Origin header in production", () => {
    vi.stubEnv("NODE_ENV", "production");
    // Browsers omit Origin on same-origin GET — this must not 403.
    expect(requireSameOrigin(req("GET"))).toBeNull();
  });

  it("still rejects a cross-origin GET", () => {
    vi.stubEnv("NODE_ENV", "production");
    const response = requireSameOrigin(req("GET", "https://attacker.example.test"));
    expect(response?.status).toBe(403);
  });

  it("rejects a no-Origin POST in production", () => {
    vi.stubEnv("NODE_ENV", "production");
    const response = requireSameOrigin(req("POST"));
    expect(response?.status).toBe(403);
  });

  it("allows a matching same-origin POST", () => {
    vi.stubEnv("NODE_ENV", "production");
    expect(
      requireSameOrigin(req("POST", "https://replyinmyvoice.com")),
    ).toBeNull();
  });
});
