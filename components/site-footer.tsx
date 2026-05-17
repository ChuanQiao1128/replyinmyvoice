import Link from "next/link";

export function SiteFooter() {
  return (
    <footer className="border-t border-line bg-paper-deep/45">
      <div className="mx-auto flex max-w-6xl flex-col gap-4 px-6 py-8 text-sm text-ink/60 sm:flex-row sm:items-center sm:justify-between">
        <p className="font-medium text-ink">Reply In My Voice</p>
        <div className="flex gap-4">
          <Link href="/" className="hover:text-ink">
            Home
          </Link>
          <Link href="/app" className="hover:text-ink">
            App
          </Link>
          <Link href="/pricing" className="hover:text-ink">
            Pricing
          </Link>
        </div>
      </div>
    </footer>
  );
}
