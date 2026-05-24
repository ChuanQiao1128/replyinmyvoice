import Link from "next/link";
import type { Metadata } from "next";

import { BuyButton } from "../../components/landing/buy-button";
import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "Pricing",
  description:
    "Start free with 3 lifetime rewrites, then buy one-time rewrite packs or go Pro/API for monthly rewrites and developer API access.",
};

type PaidSku = "quick_pack" | "value_pack" | "pro_api" | "focus_pack";

const priceEnvBySku = {
  quick_pack: "STRIPE_PRICE_QUICK_PACK_NZD",
  value_pack: "STRIPE_PRICE_VALUE_PACK_NZD",
  pro_api: "STRIPE_PRICE_PRO_API_MONTHLY_NZD",
  focus_pack: "STRIPE_PRICE_FOCUS_PACK_NZD",
} satisfies Record<PaidSku, string>;

function isPriceConfigured(sku: PaidSku) {
  return Boolean(process.env[priceEnvBySku[sku]]);
}

// Focus Pack stays hidden unless its price is configured AND the flag is on.
function isFocusPackEnabled() {
  return isPriceConfigured("focus_pack") && process.env.SHOW_FOCUS_PACK === "true";
}

function PlanAction({
  configured,
  sku,
  label,
}: {
  configured: boolean;
  sku: PaidSku;
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

  return <BuyButton sku={sku} label={label} />;
}

type Pack = {
  sku: PaidSku;
  name: string;
  price: string;
  allowance: string;
  term: string;
  description: string;
  cta: string;
  highlight?: boolean;
  badge?: string;
  apiNote?: boolean;
};

const packs: Pack[] = [
  {
    sku: "quick_pack",
    name: "Quick Pack",
    price: "NZ$2.50",
    allowance: "10 rewrites",
    term: "Valid 90 days",
    description: "The lowest-cost way to start. No subscription, no auto-renew.",
    cta: "Get Quick Pack",
  },
  {
    sku: "value_pack",
    name: "Value Pack",
    price: "NZ$6.90",
    allowance: "30 rewrites",
    term: "Valid 90 days",
    description: "Best price per rewrite — most people start here.",
    cta: "Get Value Pack",
    highlight: true,
    badge: "Most popular",
  },
  {
    sku: "pro_api",
    name: "Pro/API",
    price: "NZ$19.90/mo",
    allowance: "90 rewrites/mo",
    term: "Monthly subscription",
    description: "For heavy use and developers. Includes API access.",
    cta: "Go Pro/API",
    apiNote: true,
  },
];

const focusPack: Pack = {
  sku: "focus_pack",
  name: "Focus Pack",
  price: "NZ$4.90",
  allowance: "20 rewrites",
  term: "Valid 90 days",
  description: "A mid-size pack for one concentrated stretch.",
  cta: "Get Focus Pack",
};

const includes = [
  {
    k: "Tone check",
    p: "A before/after reference signal on every rewrite — a guide, never a guarantee.",
  },
  {
    k: "Facts preserved",
    p: "Dates, names, and amounts you mark stay intact through the rewrite.",
  },
  {
    k: "Warm · Direct",
    p: "Two simple tone presets that shape the reply around your real context.",
  },
  {
    k: "Private history",
    p: "Recent rewrites stay in your browser only — not saved to our database.",
  },
];

export default function PricingPage() {
  const visiblePacks = isFocusPackEnabled() ? [...packs, focusPack] : packs;

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
            <h1>Start free. Buy rewrites when you need them.</h1>
            <p className="lede">
              Every signed-in user gets 3 lifetime rewrites with no card. After
              that, buy one-time rewrite packs, or go Pro/API for monthly
              rewrites and developer API access.
            </p>
          </div>

          <div className="pricing-wrap" style={{ marginTop: 44 }}>
            <div className="plan plan-free">
              <div className="eyebrow">
                <span className="dot" />
                Free tier
              </div>
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

            <div className="plan plan-paid">
              <div className="eyebrow" style={{ color: "var(--bg)" }}>
                <span className="dot" style={{ background: "var(--bg)" }} />
                Rewrite packs & Pro/API
              </div>
              <h3>Pay only for what you need.</h3>

              <div className="pp-packs">
                {visiblePacks.map((pack) => (
                  <article
                    key={pack.sku}
                    className={"pp-pack" + (pack.highlight ? " pp-pop" : "")}
                  >
                    <div className="pp-pack-head">
                      <div>
                        <div className="pp-pack-name">
                          {pack.name}
                          {pack.badge ? (
                            <span className="pack-tag">{pack.badge}</span>
                          ) : null}
                        </div>
                        <div className="pp-pack-sub">
                          {pack.allowance} · {pack.term}
                        </div>
                      </div>
                      <div className="pp-pack-price">{pack.price}</div>
                    </div>
                    <p className="pp-pack-desc">{pack.description}</p>
                    <PlanAction
                      configured={isPriceConfigured(pack.sku)}
                      sku={pack.sku}
                      label={pack.cta}
                    />
                  </article>
                ))}
              </div>

              <div className="plan-meta">
                Packs are one-time and valid 90 days. Pro/API is billed monthly
                through Stripe; monthly rewrites reset each period and do not roll
                over.
              </div>
            </div>
          </div>

          <div className="pp-includes-head">Every plan includes</div>
          <div className="pp-includes">
            {includes.map((item) => (
              <div className="pp-include" key={item.k}>
                <div className="k">{item.k}</div>
                <p>{item.p}</p>
              </div>
            ))}
          </div>
        </div>
      </section>
    </main>
  );
}
