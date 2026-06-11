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
  buttonClassName,
}: {
  configured: boolean;
  sku: PaidSku;
  label: string;
  buttonClassName: string;
}) {
  if (!configured) {
    const unavailableTitle =
      sku === "focus_pack"
        ? "Focus Pack — available soon"
        : `${label.replace(/^(Get|Go) /, "")} — available soon`;

    return (
      <button
        aria-disabled="true"
        className="btn btn-ghost btn-lg pcard-btn"
        disabled
        style={{ cursor: "not-allowed", opacity: 0.62 }}
        title={unavailableTitle}
        type="button"
      >
        Available soon
      </button>
    );
  }

  return <BuyButton sku={sku} label={label} className={buttonClassName} />;
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

// One-time packs (kept as data for JSON-LD + Focus Pack gating).
const packs: Pack[] = [
  {
    sku: "quick_pack",
    name: "Quick Pack",
    price: "NZ$2.50",
    allowance: "10 rewrites",
    term: "valid 90 days",
    description: "Try it on real replies, no commitment.",
    cta: "Get Quick Pack",
  },
  {
    sku: "value_pack",
    name: "Value Pack",
    price: "NZ$6.90",
    allowance: "30 rewrites",
    term: "valid 90 days",
    description: "The best one-time unit price.",
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
  term: "valid 90 days",
  description: "A mid-size pack for one concentrated stretch.",
  cta: "Get Focus Pack",
};

const proApiPack: Pack = {
  sku: "pro_api",
  name: "Pro/API",
  price: "NZ$19.90/mo",
  allowance: "90 rewrites / month",
  term: "Monthly subscription",
  description: "For heavy use and developers.",
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

function Check() {
  return (
    <span aria-hidden="true" className="pcard-check">
      ✓
    </span>
  );
}

export default async function PricingPage() {
  const visiblePacks = isFocusPackEnabled() ? [...packs, focusPack] : packs;
  const productOfferJsonLd = buildProductOfferJsonLd(
    [...visiblePacks, proApiPack].map(productOfferForPack),
  );
  const focusEnabled = isFocusPackEnabled();

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
          <div className="pcards-head">
            <h1>Pricing</h1>
            <p className="lede">
              Pay per rewrite with one-time packs, or go monthly for heavy use
              and the API. A trial code unlocks 3 trial rewrites — no card.
            </p>
            {sessionEmail ? (
              <p className="pricing-session-hint">
                Signed in as {sessionEmail} — purchases credit this account.
              </p>
            ) : null}
            <PricingCheckoutResume />
          </div>

          {/* Canonical plan cards (modeled on familiar plan-page layouts) */}
          <div className={"pcards" + (focusEnabled ? " pcards-5" : "")}>
            {/* Trial — code-gated */}
            <article className="pcard">
              <h3 className="pcard-name">Trial</h3>
              <p className="pcard-tag">Try it with a team-issued code</p>
              <div className="pcard-price">
                NZ$0 <small>/ with a trial code</small>
              </div>
              <Link className="btn btn-primary btn-lg pcard-btn" href={trialCtaHref}>
                {trialCtaLabel}
              </Link>
              <ul className="pcard-list">
                <li>
                  <Check />
                  3 trial rewrites
                </li>
                <li>
                  <Check />
                  No card needed
                </li>
                <li>
                  <Check />
                  Full engine — facts preserved
                </li>
                <li>
                  <Check />
                  AI Signal before/after on every rewrite
                </li>
              </ul>
              <p className="pcard-foot">
                Trial code access · No code?{" "}
                <a href="mailto:info@timeawake.co.nz">Contact us</a>
              </p>
            </article>

            {/* One-time packs */}
            {visiblePacks.map((pack) => (
              <article
                className={"pcard" + (pack.highlight ? " pcard-pop" : "")}
                key={pack.sku}
              >
                {pack.badge ? (
                  <span className="pcard-badge">{pack.badge}</span>
                ) : null}
                <h3 className="pcard-name">{pack.name}</h3>
                <p className="pcard-tag">{pack.description}</p>
                <div className="pcard-price">
                  {pack.price} <small>/ one-time</small>
                </div>
                <PlanAction
                  configured={isPriceConfigured(pack.sku)}
                  sku={pack.sku}
                  label={pack.cta}
                  buttonClassName={
                    pack.highlight
                      ? "btn btn-accent btn-lg pcard-btn"
                      : "btn btn-primary btn-lg pcard-btn"
                  }
                />
                <ul className="pcard-list">
                  <li>
                    <Check />
                    {pack.allowance}, {pack.term}
                  </li>
                  <li>
                    <Check />
                    {unitPriceBySku[pack.sku]}
                  </li>
                  <li>
                    <Check />
                    One-time — never auto-renews
                  </li>
                  <li>
                    <Check />
                    AI Signal + server-backed history
                  </li>
                </ul>
                <p className="pcard-foot">One-time packs · Checkout by Stripe</p>
              </article>
            ))}

            {/* Pro/API — monthly */}
            <article className="pcard" id="pro">
              <h3 className="pcard-name">Pro/API</h3>
              <p className="pcard-tag">{proApiPack.description}</p>
              <div className="pcard-price">
                NZ$19.90 <small>/ month</small>
              </div>
              {isOnDeveloperTier ? (
                <Link className="btn btn-primary btn-lg pcard-btn" href="/app/account">
                  Manage billing
                </Link>
              ) : (
                <PlanAction
                  configured={isPriceConfigured("pro_api")}
                  sku="pro_api"
                  label={proApiPack.cta}
                  buttonClassName="btn btn-primary btn-lg pcard-btn"
                />
              )}
              <ul className="pcard-list">
                <li>
                  <Check />
                  90 rewrites every month
                </li>
                <li>
                  <Check />
                  REST API + MCP server access
                </li>
                <li>
                  <Check />
                  One key — web + API share one balance
                </li>
                <li>
                  <Check />
                  {unitPriceBySku["pro_api"]}
                </li>
                <li>
                  <Check />
                  Cancel anytime
                </li>
              </ul>
              <p className="pcard-foot">
                Monthly subscription ·{" "}
                <Link href="/developers/api">Integration docs</Link>
              </p>
            </article>
          </div>

          <p className="pcards-note">
            Facts preserved on every rewrite · AI Signal is a before/after
            reference, not a guarantee · A rewrite that fails our quality bar
            isn&apos;t charged
          </p>

          <PricingComparison />
          <PricingFaq />
        </div>
      </section>
    </main>
  );
}
