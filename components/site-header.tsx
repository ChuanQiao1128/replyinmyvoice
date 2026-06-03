import Link from "next/link";

import { isAdminSession } from "../lib/admin-auth";
import { getCurrentSession } from "../lib/entra-auth";

export async function SiteHeader() {
  const session = await getCurrentSession();

  return (
    <nav className="nav">
      <div className="wrap nav-inner">
        <Link href="/" className="brand">
          <span className="brand-mark" aria-hidden="true">
            R
          </span>
          <span>Reply In My Voice</span>
        </Link>
        <div className="nav-links">
          <Link href="/pricing">Pricing</Link>
          <Link href="/developers">Developers</Link>
          {!session ? (
            <>
              <Link href="/sign-in">Sign in</Link>
              <Link href="/sign-up" className="btn btn-primary">
                Start rewriting <span className="btn-arrow">→</span>
              </Link>
            </>
          ) : (
            <>
              {isAdminSession(session) ? <Link href="/admin">Admin</Link> : null}
              <a href="/api/auth/logout">Sign out</a>
              <Link href="/app" className="btn btn-primary">
                Open app <span className="btn-arrow">→</span>
              </Link>
            </>
          )}
        </div>
      </div>
    </nav>
  );
}
