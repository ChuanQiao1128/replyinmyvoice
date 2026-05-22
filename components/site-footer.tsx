import Link from "next/link";

export function SiteFooter() {
  return (
    <footer className="border-t border-ink bg-ink text-paper">
      <div className="mx-auto grid max-w-6xl gap-8 px-6 py-12 text-sm text-paper/68 md:grid-cols-[1.35fr_0.7fr_0.7fr]">
        <div>
          <p className="text-base font-semibold text-paper">Reply In My Voice</p>
          <p className="mt-2 max-w-md leading-6">
            Operated by TimeAwake Ltd. Built for practical replies in teacher,
            sales, workplace, and client communication.
          </p>
          <p className="mt-4 text-xs text-paper/45">
            © 2026 TimeAwake Ltd. All rights reserved.
          </p>
        </div>
        <div>
          <p className="font-semibold text-paper">Product</p>
          <div className="mt-3 grid gap-2">
            <Link href="/" className="transition hover:text-paper">
              Home
            </Link>
            <Link href="/app" className="transition hover:text-paper">
              App
            </Link>
            <Link href="/pricing" className="transition hover:text-paper">
              Pricing
            </Link>
          </div>
        </div>
        <div>
          <p className="font-semibold text-paper">Company</p>
          <div className="mt-3 grid gap-2">
            <Link href="/privacy" className="transition hover:text-paper">
              Privacy
            </Link>
            <Link href="/terms" className="transition hover:text-paper">
              Terms
            </Link>
            <a href="mailto:info@timeawake.co.nz" className="transition hover:text-paper">
              Contact
            </a>
          </div>
        </div>
      </div>
    </footer>
  );
}
