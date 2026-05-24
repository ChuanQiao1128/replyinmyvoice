import { describe, expect, it, vi } from "vitest";

vi.mock("next/headers", () => ({
  cookies: vi.fn(),
}));

import { summarizeAccessTokenForLog } from "../../lib/azure-api";

function encodeJwtPart(value: unknown) {
  return Buffer.from(JSON.stringify(value)).toString("base64url");
}

function unsignedToken(claims: Record<string, unknown>) {
  return [
    encodeJwtPart({ alg: "none", typ: "JWT" }),
    encodeJwtPart(claims),
    "signature",
  ].join(".");
}

describe("Azure API diagnostics", () => {
  it("summarizes access-token shape without logging private identity claims", () => {
    const token = unsignedToken({
      aud: "api://1ecb5f62-22b8-4e5a-8139-b2c4f15c3f32",
      email: "casey@example.com",
      exp: 1_777_777_777,
      iss: "https://614ea821-6ef3-43e2-8613-d4b13fae115d.ciamlogin.com/614ea821-6ef3-43e2-8613-d4b13fae115d/v2.0",
      name: "Casey Rivera",
      oid: "stable-object-id",
      scp: "access_as_user profile",
      sub: "pairwise-subject",
      ver: "2.0",
    });

    expect(summarizeAccessTokenForLog(token)).toEqual({
      aud: ["api-uri:f15c3f32"],
      hasOid: true,
      hasRoles: false,
      hasScp: true,
      hasSub: true,
      issHost: "614ea821-6ef3-43e2-8613-d4b13fae115d.ciamlogin.com",
      roleCount: 0,
      scopeNames: ["access_as_user", "profile"],
      ver: "2.0",
    });
  });
});
