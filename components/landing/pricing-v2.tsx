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
            <h2>Start free, then buy rewrites as you need them.</h2>
            <p className="lede">
              Everyone starts with 3 lifetime rewrites and no card. After that,
              buy one-time rewrite packs, or go Pro/API for monthly rewrites and
              developer access.
            </p>
          </div>
        </div>

        <div className="pricing-wrap">
          <div className="plan plan-free">
            <div className="eyebrow">
              <span className="dot" />
              Free tier
            </div>
            <h3>Try it three times.</h3>
            <div className="plan-price">
              3<small>free rewrites</small>
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
              Rewrite packs & Pro/API
            </div>
            <h3>Buy a pack, or go Pro/API for more.</h3>

            <div className="pack-list">
              <div className="pack-row">
                <div>
                  <div className="pack-name">Quick Pack</div>
                  <div className="pack-sub">
                    10 rewrites · one-time · valid 90 days
                  </div>
                </div>
                <div className="pack-price">NZ$2.50</div>
              </div>

              <div className="pack-row pack-pop">
                <div>
                  <div className="pack-name">
                    Value Pack <span className="pack-tag">Most popular</span>
                  </div>
                  <div className="pack-sub">
                    30 rewrites · one-time · best price per rewrite
                  </div>
                </div>
                <div className="pack-price">NZ$6.90</div>
              </div>

              <div className="pack-row">
                <div>
                  <div className="pack-name">Pro/API</div>
                  <div className="pack-sub">
                    90 rewrites / month · includes API access
                  </div>
                </div>
                <div className="pack-price">
                  NZ$19.90<small>/mo</small>
                </div>
              </div>
            </div>

            <div className="plan-cta">
              <Link href="/pricing" className="btn btn-primary btn-lg">
                Compare plans <span className="btn-arrow">-&gt;</span>
              </Link>
            </div>
            <div className="plan-meta">
              Billing is managed through Stripe. Packs expire 90 days after
              purchase.
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
