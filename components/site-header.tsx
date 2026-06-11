import Link from "next/link";

import { isAdminSession } from "../lib/admin-auth";
import { getCurrentSession } from "../lib/entra-auth";
import { SiteHeaderMobileMenu } from "./site-header-mobile-menu";
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
                <Link href="/developers/keys">API keys</Link>
                {isAdmin ? <Link href="/admin">Admin</Link> : null}
                <SignOutLink rewriteHistoryUserKey={signOutUserKey} />
              </>
            )}
          </div>
          <SiteHeaderMobileMenu
            isAdmin={isAdmin}
            primaryCta={primaryCta}
            signedIn={Boolean(session)}
            signOutUserKey={signOutUserKey}
          />
          <Link href={primaryCta.href} className="btn btn-primary nav-primary-cta">
            {primaryCta.label} <span className="btn-arrow">→</span>
          </Link>
        </div>
      </div>
    </nav>
  );
}
