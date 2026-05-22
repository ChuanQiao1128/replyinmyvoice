import { PenLine } from "lucide-react";
import Link from "next/link";

import { AdminEntry } from "./app/admin-entry";
import { LinkButton } from "./ui/button";
import { getCurrentSession } from "../lib/entra-auth";

export async function SiteHeader({ showAdmin = false }: { showAdmin?: boolean }) {
  const session = await getCurrentSession();

  return (
    <header className="sticky top-0 z-30 border-b border-line/80 bg-white/90 backdrop-blur-xl">
      <div className="mx-auto flex min-h-16 max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
        <Link
          href="/"
          className="group flex min-w-0 items-center gap-2 font-semibold text-ink"
        >
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-ink text-paper shadow-crisp transition group-hover:bg-clay">
            <PenLine className="h-4 w-4" aria-hidden="true" />
          </span>
          <span className="truncate">Reply In My Voice</span>
        </Link>
        <nav className="flex shrink-0 items-center gap-1 sm:gap-2">
          <Link
            href="/pricing"
            className="hidden rounded-md px-3 py-2 text-sm font-medium text-ink/70 transition hover:bg-paper-deep/70 hover:text-ink sm:inline-flex"
          >
            Pricing
          </Link>
          <Link
            href="/developers"
            className="hidden rounded-md px-3 py-2 text-sm font-medium text-ink/70 transition hover:bg-paper-deep/70 hover:text-ink md:inline-flex"
          >
            Developers
          </Link>
          {!session ? (
            <>
              <Link
                href="/sign-in"
                className="hidden rounded-md px-3 py-2 text-sm font-medium text-ink/70 transition hover:bg-paper-deep/70 hover:text-ink sm:inline-flex"
              >
                Sign in
              </Link>
              <LinkButton href="/sign-up" variant="primary" className="px-3 sm:px-4">
                <span className="sm:hidden">Start</span>
                <span className="hidden sm:inline">Start rewriting</span>
              </LinkButton>
            </>
          ) : (
            <>
              <AdminEntry visible={showAdmin} />
              <LinkButton href="/app" variant="secondary" className="px-3 sm:px-4">
                Open app
              </LinkButton>
              <a
                href="/api/auth/logout"
                className="hidden rounded-md px-3 py-2 text-sm font-medium text-ink/70 transition hover:bg-paper-deep/70 hover:text-ink sm:inline-flex"
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
