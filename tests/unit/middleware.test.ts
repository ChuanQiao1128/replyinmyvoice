import { NextRequest } from "next/server";
import { describe, expect, it } from "vitest";

import { middleware } from "../../middleware";
import { sessionCookieName } from "../../lib/entra-auth";

const req = (path: string, withSession = false) => {
  const r = new NextRequest(new URL(path, "https://example.com"));
  if (withSession) r.cookies.set(sessionCookieName, "x");
  return r;
};

describe("middleware", () => {
  it("redirects unauthed /app", () => {
    const res = middleware(req("/app"));
    expect(res.status).toBe(307);
    expect(res.headers.get("location")).toContain("/sign-in");
    expect(res.headers.get("location")).toContain("redirectTo=%2Fapp");
    expect(res.headers.get("location")).not.toContain("redirect_to=");
  });
  it("redirects unauthed /app/foo", () => {
    const res = middleware(req("/app/foo"));
    expect(res.headers.get("location")).toContain("redirectTo=%2Fapp%2Ffoo");
    expect(res.headers.get("location")).not.toContain("redirect_to=");
  });
  it("allows authed /app", () => {
    const res = middleware(req("/app", true));
    expect(res.status).toBe(200);
  });
  it("401s unauthed /api/rewrite", async () => {
    const res = middleware(req("/api/rewrite"));
    expect(res.status).toBe(401);
    expect(await res.json()).toEqual({ error: "unauthorized" });
  });
  it("allows authed /api/rewrite", () => {
    const res = middleware(req("/api/rewrite", true));
    expect(res.status).toBe(200);
  });
});
