import Link from "next/link";
import type { Metadata } from "next";

import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "Pricing",
  description:
    "One clear plan for practical replies: 3 free rewrites after sign-up, then NZD $9/month for 40 monthly rewrites.",
};

const pricingHighlights = [
  "3 free lifetime rewrites after sign-up",
  "40 rewrites per billing month on the paid plan",
  "Billing and cancellation handled through Stripe",
];

export default function PricingPage() {
  return (
    <main className="rimv">
      <SiteHeader />
      <section className="page">
        <div className="wrap">
          <div className="page-head">
            <div className="eyebrow">
              <span className="dot" />
              Pricing
            </div>
            <h1>One clear plan for practical replies.</h1>
            <p className="lede">
              Start with three successful free rewrites after sign-up. Upgrade to
              the NZD $9 monthly plan when you want a steady workflow for teacher
              messages, sales follow-ups, workplace email, and client replies.
            </p>
            <ul className="content-list" style={{ maxWidth: "60ch" }}>
              {pricingHighlights.map((highlight) => (
                <li key={highlight}>{highlight}</li>
              ))}
            </ul>
          </div>

          <div className="pricing-wrap" style={{ marginTop: 48 }}>
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
    </main>
  );
}
