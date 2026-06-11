import Link from "next/link";
import type { Metadata } from "next";

import { isDeveloperTierStatus } from "../../components/app/shell/shell-types";
import { BuyButton } from "../../components/landing/buy-button";
import { PricingComparison } from "../../components/landing/pricing-comparison";
import { PricingFaq } from "../../components/landing/pricing-faq";
import { PricingTrust } from "../../components/landing/pricing-trust";
import { buildProductOfferJsonLd } from "../../components/seo/json-ld";
import { SiteHeader } from "../../components/site-header";
import { fetchAzureAccountSummary } from "../../lib/azure-api";
import { getCurrentSession } from "../../lib/entra-auth";

export const metadata: Metadata = {
  title: "Pricing",
  description:
    "Redeem a trial code for 3 rewrites, then buy one-time rewrite packs or go Pro/API for monthly rewrites, REST API, and MCP server access.",
  openGraph: {
    title: "Reply In My Voice pricing",
    description:
      "Compare trial-code access, Quick Pack, Value Pack, and Pro/API options for replies that still sound like you.",
    url: "https://replyinmyvoice.com/pricing",
    siteName: "Reply In My Voice",
    type: "website",
    images: "/og.png",
  },
  twitter: {
    card: "summary_large_image",
    title: "Reply In My Voice pricing",
    description:
      "Compare trial-code access, Quick Pack, Value Pack, and Pro/API options for replies that still sound like you.",
    images: "/og.png",
  },
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
    const unavailableTitle =
      sku === "focus_pack"
        ? "Focus Pack — available soon"
        : `${label.replace(/^(Get|Go) /, "")} — available soon`;

    return (
      <button
        aria-disabled="true"
        className="btn btn-ghost btn-lg"
        disabled
        style={{
          cursor: "not-allowed",
          opacity: 0.62,
          width: "100%",
          justifyContent: "center",
        }}
        title={unavailableTitle}
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

const proApiPack: Pack = {
  sku: "pro_api",
  name: "Pro/API",
  price: "NZ$19.90/mo",
  allowance: "90 rewrites/mo",
  term: "Monthly subscription",
  description:
    "For heavy use and developers. REST API + MCP server, one key, web and API rewrites share one balance.",
  cta: "Go Pro/API",
};

const unitPriceBySku = {
  quick_pack: "≈ NZ$0.25 / rewrite",
  value_pack: "≈ NZ$0.23 / rewrite",
  pro_api: "≈ NZ$0.22 / rewrite",
  focus_pack: "≈ NZ$0.25 / rewrite",
} satisfies Record<PaidSku, string>;

function packGroupLabel(pack: Pack) {
  return pack.term === "Monthly subscription"
    ? "Monthly subscription"
    : "One-time packs";
}

function nzdOfferPrice(displayPrice: string) {
  const match = displayPrice.match(/NZ\$(\d+(?:\.\d{2})?)/);
  return match?.[1] ?? displayPrice;
}

function productOfferForPack(pack: Pack) {
  return {
    name: pack.name,
    price: nzdOfferPrice(pack.price),
    priceCurrency: "NZD",
  };
}

export default async function PricingPage() {
  const visiblePacks = isFocusPackEnabled() ? [...packs, focusPack] : packs;
  const productOfferJsonLd = buildProductOfferJsonLd(
    [...visiblePacks, proApiPack].map(productOfferForPack),
  );

  // Signed-in awareness (PV-3 safe subset): read session + subscription status.
  // fetchAzureAccountSummary returns null when unauthenticated — handled gracefully.
  const session = await getCurrentSession();
  const accountSummary = session ? await fetchAzureAccountSummary().catch(() => null) : null;
  const subscriptionStatus = accountSummary?.subscriptionStatus ?? "";
  const isOnDeveloperTier = subscriptionStatus
    ? isDeveloperTierStatus(subscriptionStatus)
    : false;
  const sessionEmail = session?.email ?? null;

  // Trial CTA: signed-out → /sign-up; signed-in → /app (already have account).
  const trialCtaHref = session ? "/app" : "/sign-up";
  const trialCtaLabel = session ? "Open app" : "Redeem a trial code";

  return (
    <main className="rimv">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(productOfferJsonLd) }}
      />
      <SiteHeader />
      <section className="page">
        <div className="wrap pricing-page">
          <div className="page-head">
            <div className="eyebrow">
              <span className="dot" />
              Pricing
            </div>
            <h1>Redeem a trial code. Buy rewrites when you need them.</h1>
            <p className="lede">
              Trial codes unlock 3 rewrites with no card. After
              that, buy one-time rewrite packs, or go Pro/API for monthly
              rewrites, REST API, and MCP server access.
            </p>
            {sessionEmail ? (
              <p className="pricing-session-hint">
                Signed in as {sessionEmail} — purchases credit this account.
              </p>
            ) : null}
          </div>

          <div className="pricing-wrap">
            {/* ── For everyday replies ─────────────────────────────── */}
            <div className="plan plan-free">
              <div className="eyebrow">
                <span className="dot" />
                Trial code access
              </div>
              <h3>Redeem a code to try it.</h3>
              <div className="plan-price">
                Free<small>trial (3 rewrites)</small>
              </div>
              <p className="uc-body">
                Enter a trial code, paste one real message, rewrite the reply,
                and decide whether the workflow fits.
              </p>
              <ul className="plan-list">
                <li>Trial code unlocks 3 successful rewrites</li>
                <li>AI Signal before/after reference on every rewrite</li>
                <li>Facts-preserved view</li>
                <li>Server-backed history at /app/history</li>
                <li>Upgrade only when you need more</li>
              </ul>
              <div className="plan-cta">
                <Link className="btn btn-ghost btn-lg" href={trialCtaHref}>
                  {trialCtaLabel} <span className="btn-arrow">-&gt;</span>
                </Link>
              </div>
              <div className="plan-meta">
                No card required for trial-code access.{" "}
                {!session ? (
                  <>
                    Don&apos;t have a code? Start with a Quick Pack, or{" "}
                    <a href="mailto:info@timeawake.co.nz">contact us</a>.
                  </>
                ) : null}
              </div>
            </div>

            {/* ── One-time packs ───────────────────────────────────── */}
            <div className="plan plan-paid">
              <div className="eyebrow" style={{ color: "var(--bg)" }}>
                <span className="dot" style={{ background: "var(--bg)" }} />
                Rewrite packs
              </div>
              <h3>Pay only for what you need.</h3>

              <div className="pp-packs">
                {visiblePacks.map((pack, index) => {
                  const groupLabel = packGroupLabel(pack);
                  const previousPack = visiblePacks[index - 1];
                  const startsGroup =
                    index === 0 || packGroupLabel(previousPack) !== groupLabel;

                  return (
                    <div
                      className={
                        "pp-pack-slot" +
                        (startsGroup && index > 0 ? " pp-pack-slot-break" : "")
                      }
                      key={pack.sku}
                    >
                      {startsGroup ? (
                        <div className="pp-pack-group">{groupLabel}</div>
                      ) : null}

                      <article
                        className={
                          "pp-pack" + (pack.highlight ? " pp-pop" : "")
                        }
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
                            <div className="pp-unit-price">
                              {unitPriceBySku[pack.sku]}
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
                    </div>
                  );
                })}
              </div>

              <div className="plan-meta">
                Packs are one-time and valid 90 days. Monthly rewrites reset
                each period and do not roll over.
              </div>
            </div>

            {/* ── For developers — Pro/API ─────────────────────────── */}
            <article className="plan plan-pro" id="pro">
              <div className="eyebrow">
                <span className="dot" />
                For developers
              </div>
              <div className="pp-pack-head">
                <div>
                  <div className="pp-pack-name">{proApiPack.name}</div>
                  <div className="pp-pack-sub">
                    {proApiPack.allowance} · {proApiPack.term}
                  </div>
                  <div className="pp-unit-price">
                    {unitPriceBySku["pro_api"]}
                  </div>
                </div>
                <div className="pp-pack-price">{proApiPack.price}</div>
              </div>
              <p className="pp-pack-desc">
                {proApiPack.description}{" "}
                <Link href="/developers">Read the integration docs &rarr;</Link>
              </p>
              <ul className="plan-list">
                <li>90 rewrites per month (web + API share one balance)</li>
                <li>REST API access — call the engine from any code</li>
                <li>MCP server — use inside Claude Code, Claude Desktop, Cursor</li>
                <li>One API key for both REST and MCP</li>
                <li>AI Signal on every rewrite</li>
                <li>Server-backed history at /app/history</li>
              </ul>
              <div className="plan-cta">
                {isOnDeveloperTier ? (
                  <Link className="btn btn-ghost btn-lg" href="/app/account">
                    You&apos;re on Pro/API — manage billing
                    <span className="btn-arrow"> -&gt;</span>
                  </Link>
                ) : (
                  <PlanAction
                    configured={isPriceConfigured("pro_api")}
                    sku="pro_api"
                    label={proApiPack.cta}
                  />
                )}
              </div>
              <div className="plan-meta">
                Billed monthly through Stripe · cancel anytime
              </div>
            </article>
          </div>

          <PricingComparison />
          <PricingTrust />
          <PricingFaq />
        </div>
      </section>
    </main>
  );
}
