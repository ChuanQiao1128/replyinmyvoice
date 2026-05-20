import Link from "next/link";
import type { ReactNode } from "react";

export function AdminShell({ children }: { children: ReactNode }) {
  return (
    <main className="min-h-screen bg-paper text-ink">
      <div className="mx-auto max-w-6xl px-4 py-6 md:px-6 md:py-8">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-clay">
              Internal
            </p>
            <h1 className="mt-2 text-3xl font-semibold">Admin dashboard</h1>
          </div>
          <nav className="flex flex-wrap gap-2 text-sm font-semibold">
            <Link
              className="rounded-md border border-line bg-white px-3 py-2 text-ink/70 hover:text-ink"
              href="/app"
            >
              App
            </Link>
            <Link
              className="rounded-md border border-line bg-white px-3 py-2 text-ink/70 hover:text-ink"
              href="/admin"
            >
              Overview
            </Link>
            <Link
              className="rounded-md border border-line bg-white px-3 py-2 text-ink/70 hover:text-ink"
              href="/admin/rewrites"
            >
              Rewrites
            </Link>
          </nav>
        </div>
        {children}
      </div>
    </main>
  );
}
