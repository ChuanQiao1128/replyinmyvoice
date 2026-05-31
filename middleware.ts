import { NextResponse, type NextRequest } from "next/server";
import { sessionCookieName } from "./lib/entra-auth";

const PROTECTED_PAGES = ["/app"];
const PROTECTED_APIS = ["/api/rewrite"];

const hasCookie = (req: NextRequest) => Boolean(req.cookies.get(sessionCookieName)?.value);

const matchesPrefix = (pathname: string, prefixes: string[]) =>
  prefixes.some((p) => pathname === p || pathname.startsWith(`${p}/`));

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  if (matchesPrefix(pathname, PROTECTED_APIS)) {
    if (hasCookie(request)) return NextResponse.next();
    return NextResponse.json({ error: "unauthorized" }, { status: 401 });
  }
  if (matchesPrefix(pathname, PROTECTED_PAGES)) {
    if (hasCookie(request)) return NextResponse.next();
    const url = new URL("/sign-in", request.url);
    url.searchParams.set("redirectTo", pathname);
    return NextResponse.redirect(url, 307);
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/app/:path*", "/api/rewrite/:path*"],
};
