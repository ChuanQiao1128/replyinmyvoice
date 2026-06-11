import Link from "next/link";
import type { Metadata } from "next";

import { isDeveloperTierStatus } from "../../components/app/shell/shell-types";
import { BuyButton } from "../../components/landing/buy-button";
import { PricingCheckoutResume } from "../../components/landing/pricing-checkout-resume";
import { PricingComparison } from "../../components/landing/pricing-comparison";
import { PricingFaq } from "../../components/landing/pricing-faq";
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
        className="btn btn-ghost btn-lg ptier-btn"
        disabled
        style={{ cursor: "not-allowed", opacity: 0.62 }}
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
};

// One-time packs (the everyday-replies path).
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
    "REST API + MCP server, one key, web and API rewrites share one balance. For heavy use and developers.",
  cta: "Go Pro/API",
};

const unitPriceBySku = {
  quick_pack: "≈ NZ$0.25 / rewrite",
  value_pack: "≈ NZ$0.23 / rewrite",
  pro_api: "≈ NZ$0.22 / rewrite",
  focus_pack: "≈ NZ$0.25 / rewrite",
} satisfies Record<PaidSku, string>;

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
            <PricingCheckoutResume />
          </div>

          {/* ── Unified tier cards: trial · one-time packs · Pro/API ── */}
          <div className="ptier-grid">
            {/* Trial code */}
            <article className="ptier">
              <div className="ptier-kicker">Trial code access</div>
              <h3 className="ptier-name">Free</h3>
              <div className="ptier-price">
                Free<small>3 trial rewrites</small>
              </div>
              <div className="ptier-allow">Redeem a code · no card</div>
              <p className="ptier-desc">
                Paste one real message, rewrite the reply, and decide whether the
                workflow fits before you buy.
              </p>
              <div className="ptier-cta">
                <Link className="btn btn-ghost btn-lg ptier-btn" href={trialCtaHref}>
                  {trialCtaLabel} <span className="btn-arrow">-&gt;</span>
                </Link>
              </div>
              <div className="ptier-note">
                No card required.{" "}
                {!session ? (
                  <>
                    No code?{" "}
                    <a href="mailto:info@timeawake.co.nz">Contact us</a>.
                  </>
                ) : null}
              </div>
            </article>

            {/* One-time packs: Quick, Value (+ Focus when enabled) */}
            {visiblePacks.map((pack) => (
              <article
                className={"ptier" + (pack.highlight ? " ptier-pop" : "")}
                key={pack.sku}
              >
                {pack.badge ? (
                  <span className="ptier-badge">{pack.badge}</span>
                ) : null}
                <div className="ptier-kicker">One-time packs</div>
                <h3 className="ptier-name">{pack.name}</h3>
                <div className="ptier-price">{pack.price}</div>
                <div className="ptier-allow">
                  {pack.allowance} · {pack.term}
                </div>
                <div className="ptier-unit">{unitPriceBySku[pack.sku]}</div>
                <p className="ptier-desc">{pack.description}</p>
                <div className="ptier-cta">
                  <PlanAction
                    configured={isPriceConfigured(pack.sku)}
                    sku={pack.sku}
                    label={pack.cta}
                  />
                </div>
              </article>
            ))}

            {/* Monthly subscription: Pro/API (co-equal developer tier) */}
            <article className="ptier ptier-dev" id="pro">
              <div className="ptier-kicker">Monthly subscription</div>
              <h3 className="ptier-name">
                {proApiPack.name}
                <span className="ptier-tag">For developers</span>
              </h3>
              <div className="ptier-price">{proApiPack.price}</div>
              <div className="ptier-allow">90 rewrites/mo · web + API</div>
              <div className="ptier-unit">{unitPriceBySku["pro_api"]}</div>
              <p className="ptier-desc">
                {proApiPack.description}{" "}
                <Link href="/developers/api">Integration docs &rarr;</Link>
              </p>
              <div className="ptier-cta">
                {isOnDeveloperTier ? (
                  <Link className="btn btn-ghost btn-lg ptier-btn" href="/app/account">
                    Manage billing <span className="btn-arrow">-&gt;</span>
                  </Link>
                ) : (
                  <PlanAction
                    configured={isPriceConfigured("pro_api")}
                    sku="pro_api"
                    label={proApiPack.cta}
                  />
                )}
              </div>
              <div className="ptier-note">
                Billed monthly through Stripe · cancel anytime
              </div>
            </article>
          </div>

          <p className="ptier-foot">
            One-time packs are valid 90 days and never auto-renew. Pro/API
            rewrites reset each month and don&apos;t roll over.
          </p>

          <PricingComparison />
          <PricingFaq />
        </div>
      </section>
    </main>
  );
}
