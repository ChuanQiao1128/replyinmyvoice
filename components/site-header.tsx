import { PenLine } from "lucide-react";
import Link from "next/link";

import { AdminEntry } from "./app/admin-entry";
import { LinkButton } from "./ui/button";
import { getCurrentSession } from "../lib/entra-auth";

export async function SiteHeader({ showAdmin = false }: { showAdmin?: boolean }) {
  const session = await getCurrentSession();

  return (
    <header className="sticky top-0 z-30 border-b border-line bg-cream/88 backdrop-blur">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6">
        <Link
          href="/"
          className="flex min-w-0 items-center gap-2.5 font-semibold text-ink"
        >
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-evergreen text-cream shadow-sm">
            <PenLine className="h-4 w-4" aria-hidden="true" />
          </span>
          <span className="truncate">Reply In My Voice</span>
        </Link>
        <nav className="flex items-center gap-1 sm:gap-2">
          <Link
            href="/pricing"
            className="hidden rounded-md px-3 py-2 text-sm font-medium text-ink/66 transition hover:bg-mist/45 hover:text-ink sm:inline-flex"
          >
            Pricing
          </Link>
          <Link
            href="/developers"
            className="hidden rounded-md px-3 py-2 text-sm font-medium text-ink/66 transition hover:bg-mist/45 hover:text-ink md:inline-flex"
          >
            Developers
          </Link>
          {!session ? (
            <>
              <Link
                href="/sign-in"
                className="hidden rounded-md px-3 py-2 text-sm font-medium text-ink/66 transition hover:bg-mist/45 hover:text-ink sm:inline-flex"
              >
                Sign in
              </Link>
              <LinkButton href="/sign-up" variant="primary">
                Start rewriting
              </LinkButton>
            </>
          ) : (
            <>
              <AdminEntry visible={showAdmin} />
              <LinkButton href="/app" variant="secondary">
                Open app
              </LinkButton>
              <a
                href="/api/auth/logout"
                className="rounded-md px-3 py-2 text-sm font-medium text-ink/66 transition hover:bg-mist/45 hover:text-ink"
              >
                Sign out
              </a>
            </>
          )}
        </nav>
      </div>
    </header>
  );
}
