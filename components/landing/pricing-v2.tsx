import Link from "next/link";

/**
 * Landing-page pricing block in the v2 design (free plan + inverted paid plan).
 */
export function PricingV2() {
  return (
    <section className="block" id="pricing">
      <div className="wrap">
        <div className="sec-head">
          <div>
            <span className="sec-num">06 · Pricing</span>
          </div>
          <div className="sec-head-lead">
            <h2>Start with the NZD $9 plan.</h2>
            <p className="lede">
              Every signed-in user gets 3 free rewrites first. Upgrade when you
              want a steady monthly workflow for real replies. Billing is
              handled through Stripe.
            </p>
          </div>
        </div>

        <div className="pricing-wrap">
          <div className="plan plan-free">
            <div className="eyebrow">Free start</div>
            <h3>Try it three times.</h3>
            <div className="plan-price">
              3<small>rewrites</small>
            </div>
            <p className="uc-body">
              Sign up, paste a draft, see whether it actually sounds like you.
              No card required to start.
            </p>
            <ul className="plan-list">
              <li>3 successful rewrites after sign-up</li>
              <li>Tone check before and after</li>
              <li>Warm and Direct tone presets</li>
              <li>All four templates included</li>
            </ul>
            <div className="plan-cta">
              <Link href="/sign-up" className="btn btn-ghost btn-lg">
                Create a free account <span className="btn-arrow">→</span>
              </Link>
            </div>
            <div className="plan-meta">No credit card required</div>
          </div>

          <div className="plan plan-paid">
            <div className="eyebrow" style={{ color: "var(--bg)" }}>
              <span className="dot" style={{ background: "var(--bg)" }} />
              Reply In My Voice — Monthly
            </div>
            <h3>For people writing replies every day.</h3>
            <div className="plan-price">
              NZD $9<small>/ month</small>
            </div>
            <p style={{ color: "rgba(246,244,238,0.7)", fontSize: 14.5, lineHeight: 1.5 }}>
              40 rewrites per billing month. Everything in the free tier, plus a
              steady working volume for daily use.
            </p>
            <ul className="plan-list">
              <li>40 rewrites per billing month</li>
              <li>3 free rewrites after sign-up</li>
              <li>Tone check before and after</li>
              <li>Teacher, sales, workplace, client templates</li>
              <li>Cancel anytime</li>
            </ul>
            <div className="plan-cta">
              <Link href="/sign-up" className="btn btn-primary btn-lg">
                Start with the NZD $9 plan <span className="btn-arrow">→</span>
              </Link>
            </div>
            <div className="plan-meta">
              By subscribing you agree to our{" "}
              <Link href="/terms" style={{ textDecoration: "underline" }}>
                Terms
              </Link>{" "}
              and{" "}
              <Link href="/privacy" style={{ textDecoration: "underline" }}>
                Privacy Policy
              </Link>
              . Operated by TimeAwake Ltd.
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
