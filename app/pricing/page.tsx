import Link from "next/link";
import type { Metadata } from "next";

import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "Pricing",
  description:
    "Start free with 3 lifetime rewrites, then choose Starter, Pro/API, Exam Week Pass, or Top-up options for practical replies.",
};

type PaidSku = "starter" | "pro" | "exam_pass" | "top_up";

const priceEnvBySku = {
  starter: "STRIPE_PRICE_STARTER",
  pro: "STRIPE_PRICE_PRO",
  exam_pass: "STRIPE_PRICE_EXAM_PASS",
  top_up: "STRIPE_PRICE_TOPUP",
} satisfies Record<PaidSku, string>;

function isPriceConfigured(sku: PaidSku) {
  return Boolean(process.env[priceEnvBySku[sku]]);
}

function PlanAction({
  configured,
  href,
  label,
}: {
  configured: boolean;
  href: string;
  label: string;
}) {
  if (!configured) {
    return (
      <button
        className="btn btn-ghost btn-lg"
        disabled
        style={{
          cursor: "not-allowed",
          opacity: 0.62,
          width: "100%",
          justifyContent: "center",
        }}
        type="button"
      >
        Available soon
      </button>
    );
  }

  return (
    <Link
      className="btn btn-primary btn-lg"
      href={href}
      style={{ width: "100%", justifyContent: "center" }}
    >
      {label} <span className="btn-arrow">-&gt;</span>
    </Link>
  );
}

const monthlyPlans = [
  {
    sku: "starter" as const,
    name: "Starter",
    price: "NZ$9.90/mo",
    allowance: "55 rewrites/mo",
    description: "For students and everyday replies that are becoming routine.",
    hint: "Most students start with Exam Week Pass or Starter.",
    bonus: "+5 first-month bonus rewrites",
    cta: "Choose Starter",
  },
  {
    sku: "pro" as const,
    name: "Pro/API",
    price: "NZ$19.90/mo",
    allowance: "110 rewrites/mo",
    description: "For heavier workflows and API access across web and developer use.",
    hint: "Developers choose Pro/API.",
    bonus: "+15 first-month bonus rewrites",
    cta: "Choose Pro/API",
  },
];

const oneTimeOptions = [
  {
    sku: "exam_pass" as const,
    name: "Exam Week Pass",
    price: "NZ$4.90",
    allowance: "25 rewrites",
    term: "7 days",
    description: "For deadline weeks, applications, and one concentrated burst.",
    cta: "Get Exam Week Pass",
  },
  {
    sku: "top_up" as const,
    name: "Top-up",
    price: "NZ$2.50",
    allowance: "+10 rewrites",
    term: "no period reset",
    description: "Appears when quota runs low, so there is no surprise metered billing.",
    cta: "Add Top-up",
  },
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
            <h1>Start free. Upgrade when replies become part of your routine.</h1>
            <p className="lede">
              Every signed-in user gets 3 lifetime rewrites with no card. Choose
              a monthly plan for regular use, or a one-time option when you only
              need help for a short stretch.
            </p>
          </div>

          <div className="pricing-wrap" style={{ marginTop: 44 }}>
            <div className="plan plan-free">
              <div className="eyebrow">Free tier</div>
              <h3>Try 3 rewrites first.</h3>
              <div className="plan-price">
                3<small>lifetime rewrites</small>
              </div>
              <p className="uc-body">
                No card required. Paste one real message, rewrite the reply, and
                decide whether the workflow fits.
              </p>
              <ul className="plan-list">
                <li>3 successful rewrites total</li>
                <li>Warm and Direct tone presets</li>
                <li>Tone check and facts-preserved view</li>
                <li>Upgrade only when you need more</li>
              </ul>
              <div className="plan-cta">
                <Link className="btn btn-ghost btn-lg" href="/sign-up">
                  Create a free account <span className="btn-arrow">-&gt;</span>
                </Link>
              </div>
              <div className="plan-meta">No card required</div>
            </div>

            <div
              className="plan plan-paid"
              style={{ background: "var(--ink)", color: "var(--bg)" }}
            >
              <div className="eyebrow" style={{ color: "var(--bg)" }}>
                <span className="dot" style={{ background: "var(--bg)" }} />
                Monthly plans
              </div>
              <h3>For a steady reply workflow.</h3>
              <div
                style={{
                  display: "grid",
                  gap: 16,
                  gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
                }}
              >
                {monthlyPlans.map((plan) => (
                  <article
                    key={plan.sku}
                    style={{
                      border: "1px solid rgba(246,244,238,0.18)",
                      borderRadius: 8,
                      padding: 18,
                      background: "rgba(246,244,238,0.06)",
                    }}
                  >
                    <div className="eyebrow" style={{ color: "var(--bg)" }}>
                      {plan.name}
                    </div>
                    <div className="plan-price" style={{ fontSize: 38 }}>
                      {plan.price}
                    </div>
                    <p style={{ color: "rgba(246,244,238,0.78)", lineHeight: 1.5 }}>
                      {plan.allowance} · {plan.description}
                    </p>
                    <ul className="plan-list" style={{ marginTop: 14 }}>
                      <li>{plan.hint}</li>
                      <li>{plan.bonus}</li>
                      {plan.sku === "pro" ? <li>API access included</li> : null}
                    </ul>
                    <div className="plan-cta">
                      <PlanAction
                        configured={isPriceConfigured(plan.sku)}
                        href={`/sign-up?plan=${plan.sku}`}
                        label={plan.cta}
                      />
                    </div>
                  </article>
                ))}
              </div>
              <div className="plan-meta">
                Monthly quotas reset each billing period. Unused monthly rewrites
                do not roll over.
              </div>
            </div>
          </div>

          <section style={{ marginTop: 48 }}>
            <div className="sec-head" style={{ marginBottom: 22 }}>
              <div>
                <span className="sec-num">One-time options</span>
              </div>
              <div className="sec-head-lead">
                <h2>For short bursts and quota gaps.</h2>
                <p className="lede">
                  Exam Week Pass is built for deadline weeks. Top-up appears
                  when quota runs low, so extra usage stays intentional.
                </p>
              </div>
            </div>
            <div
              className="pricing-wrap"
              style={{
                gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
                gap: 20,
              }}
            >
              {oneTimeOptions.map((option) => (
                <article className="plan plan-free" key={option.sku}>
                  <div className="eyebrow">{option.name}</div>
                  <h3>{option.term}</h3>
                  <div className="plan-price">
                    {option.price}<small>{option.allowance}</small>
                  </div>
                  <p className="uc-body">{option.description}</p>
                  <ul className="plan-list">
                    <li>{option.allowance}</li>
                    <li>
                      {option.sku === "exam_pass"
                        ? "Expires after 7 days"
                        : "No monthly reset"}
                    </li>
                    <li>No metered surprise billing</li>
                  </ul>
                  <div className="plan-cta">
                    <PlanAction
                      configured={isPriceConfigured(option.sku)}
                      href={`/sign-up?plan=${option.sku}`}
                      label={option.cta}
                    />
                  </div>
                  <div className="plan-meta">
                    {option.sku === "top_up"
                      ? "Shown when quota runs low"
                      : "Best for exam and application weeks"}
                  </div>
                </article>
              ))}
            </div>
          </section>
        </div>
      </section>
    </main>
  );
}
