import Link from "next/link";

import { isAdminSession } from "../lib/admin-auth";
import { getCurrentSession } from "../lib/entra-auth";
import { SignOutLink } from "./sign-out-link";

type Props = {
  rewriteHistoryUserKey?: string;
};

export async function SiteHeader({ rewriteHistoryUserKey }: Props = {}) {
  const session = await getCurrentSession();
  const isAdmin = session ? isAdminSession(session) : false;
  const signOutUserKey = session
    ? rewriteHistoryUserKey ?? session.sub
    : undefined;
  const primaryCta = session
    ? { href: "/app", label: "Open app" }
    : { href: "/sign-up", label: "Start rewriting" };

  return (
    <nav className="nav">
      <div className="wrap nav-inner">
        <Link href="/" className="brand" aria-label="Reply In My Voice home">
          <span className="brand-mark" aria-hidden="true">
            R
          </span>
          <span>Reply In My Voice</span>
        </Link>
        <div className="nav-links">
          <div className="nav-inline-links">
            <Link href="/pricing">Pricing</Link>
            <Link href="/developers">Developers</Link>
            {!session ? (
              <Link href="/sign-in">Sign in</Link>
            ) : (
              <>
                {isAdmin ? <Link href="/admin">Admin</Link> : null}
                <SignOutLink rewriteHistoryUserKey={signOutUserKey} />
              </>
            )}
          </div>
          <details className="mobile-nav-menu">
            <summary className="mobile-nav-trigger" aria-label="Menu">
              <span>Menu</span>
              <span className="mobile-nav-trigger-mark" aria-hidden="true">
                +
              </span>
            </summary>
            <div className="mobile-nav-panel">
              <Link href="/pricing">Pricing</Link>
              <Link href="/developers">Developers</Link>
              {!session ? (
                <>
                  <Link href="/sign-in">Sign in</Link>
                  <Link href="/sign-up" className="mobile-nav-cta">
                    Start rewriting
                  </Link>
                </>
              ) : (
                <>
                  {isAdmin ? <Link href="/admin">Admin</Link> : null}
                  <SignOutLink rewriteHistoryUserKey={signOutUserKey} />
                  <Link href="/app" className="mobile-nav-cta">
                    Open app
                  </Link>
                </>
              )}
            </div>
          </details>
          <Link href={primaryCta.href} className="btn btn-primary nav-primary-cta">
            {primaryCta.label} <span className="btn-arrow">→</span>
          </Link>
        </div>
      </div>
    </nav>
  );
}
