import Link from "next/link";

export function SiteFooter() {
  return (
    <footer className="site-footer">
      <div className="wrap">
        <div className="footer-grid">
          <div>
            <Link href="/" className="brand" style={{ fontSize: 17 }}>
              <span className="brand-mark" aria-hidden="true">
                R
              </span>
              <span>Reply In My Voice</span>
            </Link>
            <p className="footer-blurb">
              Operated by TimeAwake Ltd. Built for practical replies in student,
              teacher, sales, workplace, and client communication.
            </p>
          </div>
          <div>
            <h5>Product</h5>
            <ul>
              <li>
                <Link href="/">Home</Link>
              </li>
              <li>
                <Link href="/app">App</Link>
              </li>
              <li>
                <Link href="/students">For students</Link>
              </li>
              <li>
                <Link href="/pricing">Pricing</Link>
              </li>
              <li>
                <Link href="/developers">Developers</Link>
              </li>
            </ul>
          </div>
          <div>
            <h5>Company</h5>
            <ul>
              <li>
                <Link href="/privacy">Privacy</Link>
              </li>
              <li>
                <Link href="/terms">Terms</Link>
              </li>
              <li>
                <a href="mailto:info@timeawake.co.nz">Contact</a>
              </li>
            </ul>
          </div>
          <div>
            <h5>Account</h5>
            <ul>
              <li>
                <Link href="/sign-in">Sign in</Link>
              </li>
              <li>
                <Link href="/sign-up">Sign up</Link>
              </li>
              <li>
                <Link href="/app">Billing</Link>
              </li>
            </ul>
          </div>
        </div>
        <div className="footer-bottom">
          <span>© 2026 TimeAwake Ltd. All rights reserved.</span>
          <span>Stripe-managed billing · NZD $9/mo</span>
        </div>
      </div>
    </footer>
  );
}
