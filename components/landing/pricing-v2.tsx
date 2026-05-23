import Link from "next/link";

/**
 * Landing-page pricing block in the v2 design.
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
            <h2>Start free, then choose the right pace.</h2>
            <p className="lede">
              Everyone starts with 3 lifetime rewrites and no card. Use Starter
              for regular replies, Pro/API for developer workflows, or a one-time
              option when you only need a short burst.
            </p>
          </div>
        </div>

        <div className="pricing-wrap">
          <div className="plan plan-free">
            <div className="eyebrow">Free tier</div>
            <h3>Try it three times.</h3>
            <div className="plan-price">
              3<small>rewrites</small>
            </div>
            <p className="uc-body">
              Sign up, paste a draft, and see whether it sounds closer to you.
              No card required to start.
            </p>
            <ul className="plan-list">
              <li>3 successful lifetime rewrites</li>
              <li>Tone check before and after</li>
              <li>Warm and Direct tone presets</li>
              <li>Facts-preserved view</li>
            </ul>
            <div className="plan-cta">
              <Link href="/sign-up" className="btn btn-ghost btn-lg">
                Create a free account <span className="btn-arrow">-&gt;</span>
              </Link>
            </div>
            <div className="plan-meta">No credit card required</div>
          </div>

          <div className="plan plan-paid">
            <div className="eyebrow" style={{ color: "var(--bg)" }}>
              <span className="dot" style={{ background: "var(--bg)" }} />
              Monthly plans
            </div>
            <h3>Starter for routine replies. Pro/API for heavier workflows.</h3>
            <div
              style={{
                display: "grid",
                gap: 16,
                gridTemplateColumns: "repeat(auto-fit, minmax(210px, 1fr))",
              }}
            >
              <div>
                <div className="plan-price" style={{ fontSize: 38 }}>
                  NZ$9.90<small>/mo</small>
                </div>
                <p style={{ color: "rgba(246,244,238,0.75)", lineHeight: 1.5 }}>
                  Starter · 55 rewrites/mo · +5 first-month bonus.
                </p>
              </div>
              <div>
                <div className="plan-price" style={{ fontSize: 38 }}>
                  NZ$19.90<small>/mo</small>
                </div>
                <p style={{ color: "rgba(246,244,238,0.75)", lineHeight: 1.5 }}>
                  Pro/API · 110 rewrites/mo · API access · +15 first-month bonus.
                </p>
              </div>
            </div>
            <ul className="plan-list">
              <li>Most students start with Exam Week Pass or Starter</li>
              <li>Exam Week Pass: NZ$4.90 for 25 rewrites over 7 days</li>
              <li>Top-up: NZ$2.50 for +10 rewrites when quota runs low</li>
              <li>No rollover for monthly quota</li>
            </ul>
            <div className="plan-cta">
              <Link href="/pricing" className="btn btn-primary btn-lg">
                Compare plans <span className="btn-arrow">-&gt;</span>
              </Link>
            </div>
            <div className="plan-meta">
              Billing is managed through Stripe. One-time passes expire as shown.
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
