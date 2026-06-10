import { NextResponse } from "next/server";

import { isProduction } from "./env";
import { hasAllowedOrigin } from "./security";

export function jsonError(message: string, status: number) {
  return NextResponse.json({ error: message }, { status });
}

export function requireSameOrigin(request: Request) {
  if (hasAllowedOrigin(request)) {
    return null;
  }

  const hasOrigin = Boolean(request.headers.get("origin"));

  // Browsers omit the Origin header on same-origin GET/HEAD requests, so an
  // absent Origin on a safe method is same-origin (a genuine cross-origin
  // request always carries an Origin header, which is checked above). Without
  // this, every same-origin read fetch — e.g. /api/me, /api/me/rewrites — 403s
  // in production. State-changing methods stay strict.
  const method = request.method.toUpperCase();
  if (!hasOrigin && (method === "GET" || method === "HEAD")) {
    return null;
  }

  if (!isProduction() && !hasOrigin) {
    console.warn("Allowing development request with no Origin header.");
    return null;
  }

  return jsonError("Cross-origin request rejected.", 403);
}
