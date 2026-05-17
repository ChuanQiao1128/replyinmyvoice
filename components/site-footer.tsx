import Link from "next/link";

export function SiteFooter() {
  return (
    <footer className="border-t border-line bg-paper-deep/45">
      <div className="mx-auto grid max-w-6xl gap-8 px-6 py-10 text-sm text-ink/62 md:grid-cols-[1.2fr_0.8fr_0.8fr]">
        <div>
          <p className="font-semibold text-ink">Reply In My Voice</p>
          <p className="mt-2 leading-6">
            Operated by TimeAwake Ltd. Built for practical replies in teacher,
            sales, workplace, and client communication.
          </p>
          <p className="mt-3 text-xs text-ink/45">
            © 2026 TimeAwake Ltd. All rights reserved.
          </p>
        </div>
        <div>
          <p className="font-semibold text-ink">Product</p>
          <div className="mt-3 grid gap-2">
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
        <div>
          <p className="font-semibold text-ink">Company</p>
          <div className="mt-3 grid gap-2">
            <Link href="/privacy" className="hover:text-ink">
              Privacy
            </Link>
            <Link href="/terms" className="hover:text-ink">
              Terms
            </Link>
            <a href="mailto:info@timeawake.co.nz" className="hover:text-ink">
              Contact
            </a>
          </div>
        </div>
      </div>
    </footer>
  );
}
