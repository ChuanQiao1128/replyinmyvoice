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

  if (!isProduction() && !request.headers.get("origin")) {
    console.warn("Allowing development POST request with no Origin header.");
    return null;
  }

  return jsonError("Cross-origin request rejected.", 403);
}
