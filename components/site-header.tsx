import { SignedIn, SignedOut, UserButton } from "@clerk/nextjs";
import { PenLine } from "lucide-react";
import Link from "next/link";

import { AdminEntry } from "./app/admin-entry";
import { LinkButton } from "./ui/button";

export function SiteHeader({ showAdmin = false }: { showAdmin?: boolean }) {
  return (
    <header className="sticky top-0 z-30 border-b border-line bg-paper/90 backdrop-blur">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-6">
        <Link href="/" className="flex items-center gap-2 font-semibold text-ink">
          <span className="flex h-9 w-9 items-center justify-center rounded-md bg-ink text-paper">
            <PenLine className="h-4 w-4" aria-hidden="true" />
          </span>
          Reply In My Voice
        </Link>
        <nav className="flex items-center gap-2">
          <Link href="/pricing" className="hidden px-3 py-2 text-sm font-medium text-ink/70 hover:text-ink sm:inline-flex">
            Pricing
          </Link>
          <SignedOut>
            <Link href="/sign-in" className="hidden px-3 py-2 text-sm font-medium text-ink/70 hover:text-ink sm:inline-flex">
              Sign in
            </Link>
            <LinkButton href="/sign-up" variant="primary">
              Start rewriting
            </LinkButton>
          </SignedOut>
          <SignedIn>
            <AdminEntry visible={showAdmin} />
            <LinkButton href="/app" variant="secondary">
              Open app
            </LinkButton>
            <UserButton afterSignOutUrl="/" />
          </SignedIn>
        </nav>
      </div>
    </header>
  );
}
