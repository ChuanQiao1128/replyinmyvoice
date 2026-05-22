import Link from "next/link";

export function SiteFooter() {
  return (
    <footer className="border-t border-line bg-ink text-cream">
      <div className="mx-auto grid max-w-7xl gap-8 px-4 py-10 text-sm text-cream/68 sm:px-6 md:grid-cols-[1.3fr_0.7fr_0.7fr]">
        <div>
          <p className="font-semibold text-cream">Reply In My Voice</p>
          <p className="mt-2 leading-6">
            Operated by TimeAwake Ltd. Built for practical replies in teacher,
            sales, workplace, and client communication.
          </p>
          <p className="mt-3 text-xs text-cream/45">
            © 2026 TimeAwake Ltd. All rights reserved.
          </p>
        </div>
        <div>
          <p className="font-semibold text-cream">Product</p>
          <div className="mt-3 grid gap-2">
            <Link href="/" className="hover:text-cream">
              Home
            </Link>
            <Link href="/app" className="hover:text-cream">
              App
            </Link>
            <Link href="/pricing" className="hover:text-cream">
              Pricing
            </Link>
          </div>
        </div>
        <div>
          <p className="font-semibold text-cream">Company</p>
          <div className="mt-3 grid gap-2">
            <Link href="/privacy" className="hover:text-cream">
              Privacy
            </Link>
            <Link href="/terms" className="hover:text-cream">
              Terms
            </Link>
            <a href="mailto:info@timeawake.co.nz" className="hover:text-cream">
              Contact
            </a>
          </div>
        </div>
      </div>
    </footer>
  );
}
