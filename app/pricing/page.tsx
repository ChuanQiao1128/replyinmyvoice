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
        className="btn btn-ghost btn-lg"
        disabled
        style={{ cursor: "not-allowed", opacity: 0.62, width: "100%", justifyContent: "center" }}
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

// One-time packs (the everyday-replies path).
const packs: Pack[] = [
  {
    sku: "quick_pack",
    name: "Quick Pack",
    price: "NZ$2.50",
    allowance: "10 rewrites",
    term: "valid 90 days",
    description: "The lowest-cost way in — try it on one real reply.",
    cta: "Get Quick Pack",
  },
  {
    sku: "value_pack",
    name: "Value Pack",
    price: "NZ$6.90",
    allowance: "30 rewrites",
    term: "valid 90 days",
    description: "The best per-rewrite price of the one-time packs.",
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
  description:
    "For heavy use and builders — call the engine from your own code, Claude, or Cursor. Web and API rewrites share one balance.",
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
            <h1>
              A short price list, for replies that still sound like{" "}
              <span className="alt">you</span>.
            </h1>
            <p className="lede">
              One-time packs from NZ$2.50 that never auto-renew. Go Pro/API
              monthly for heavy use, the REST API, and MCP. A trial code
              unlocks 3 trial rewrites — no card.
            </p>
            {sessionEmail ? (
              <p className="pricing-session-hint">
                Signed in as {sessionEmail} — purchases credit this account.
              </p>
            ) : null}
            <PricingCheckoutResume />
          </div>

          {/* ── The price list: one typeset list instead of a card grid ── */}
          <div className="plist-head-strip" aria-hidden="true">
            <span>Price list · NZD</span>
            <span>Secure checkout by Stripe</span>
          </div>
          <div className="plist">
            {/* One-time packs */}
            {visiblePacks.map((pack, index) => (
              <article
                className={"plist-row" + (pack.highlight ? " plist-row-pop" : "")}
                key={pack.sku}
              >
                <div className="plist-group">
                  {index === 0 ? "One-time packs" : ""}
                </div>
                <div className="plist-id">
                  <h3 className="plist-name">
                    {pack.name}
                    {pack.badge ? (
                      <span className="plist-stamp">{pack.badge}</span>
                    ) : null}
                  </h3>
                  <p className="plist-desc">{pack.description}</p>
                </div>
                <div className="plist-ledger">
                  <div className="plist-allow">
                    {pack.allowance} · {pack.term}
                  </div>
                  <div className="plist-unit">{unitPriceBySku[pack.sku]}</div>
                </div>
                <div className="plist-price">{pack.price}</div>
                <div className="plist-cta">
                  <PlanAction
                    configured={isPriceConfigured(pack.sku)}
                    sku={pack.sku}
                    label={pack.cta}
                    buttonClassName={
                      pack.highlight
                        ? "btn btn-accent btn-lg"
                        : "btn btn-ghost btn-lg"
                    }
                  />
                </div>
              </article>
            ))}

            {/* Monthly subscription — Pro/API: the page's one dark anchor */}
            <article className="plist-row plist-row-dark" id="pro">
              <div className="plist-group">Monthly subscription</div>
              <div className="plist-id">
                <h3 className="plist-name">
                  {proApiPack.name}
                  <span className="plist-chip">REST API + MCP · one key</span>
                </h3>
                <p className="plist-desc">
                  {proApiPack.description}{" "}
                  <Link href="/developers/api">Integration docs &rarr;</Link>
                </p>
              </div>
              <div className="plist-ledger">
                <div className="plist-allow">{proApiPack.allowance}</div>
                <div className="plist-unit">{unitPriceBySku["pro_api"]}</div>
              </div>
              <div className="plist-price">{proApiPack.price}</div>
              <div className="plist-cta">
                {isOnDeveloperTier ? (
                  <Link className="btn btn-ghost btn-lg" href="/app/account">
                    Manage billing <span className="btn-arrow">-&gt;</span>
                  </Link>
                ) : (
                  <PlanAction
                    configured={isPriceConfigured("pro_api")}
                    sku="pro_api"
                    label={proApiPack.cta}
                    buttonClassName="btn btn-ghost btn-lg"
                  />
                )}
                <div className="plist-cancel">Cancel anytime</div>
              </div>
            </article>

            {/* Trial code — a margin note, not a tier */}
            <article className="plist-row plist-row-note">
              <div className="plist-group">Trial code access</div>
              <div className="plist-id">
                <p className="plist-desc">
                  Have a trial code? It unlocks <strong>3 trial rewrites</strong>{" "}
                  in the app — no card needed.
                  {!session ? (
                    <>
                      {" "}
                      No code? <a href="mailto:info@timeawake.co.nz">Contact us</a>.
                    </>
                  ) : null}
                </p>
              </div>
              <div className="plist-ledger">
                <div className="plist-allow">3 rewrites · no card</div>
              </div>
              <div className="plist-price plist-price-note">—</div>
              <div className="plist-cta">
                <Link className="btn btn-ghost btn-lg" href={trialCtaHref}>
                  {trialCtaLabel} <span className="btn-arrow">-&gt;</span>
                </Link>
              </div>
            </article>
          </div>

          <p className="plist-foot">
            Every option includes facts-preserved rewriting, the AI Signal
            before/after reference (a guide, not a guarantee), and
            server-backed history. A rewrite that fails our quality bar
            isn&apos;t charged.
          </p>

          <PricingComparison />
          <PricingFaq />

          <div className="plist-closing">
            <p>
              Ready when you are — most replies cost about NZ$0.23.
            </p>
            <div className="plist-closing-cta">
              <PlanAction
                configured={isPriceConfigured("value_pack")}
                sku="value_pack"
                label="Get Value Pack"
                buttonClassName="btn btn-accent btn-lg"
              />
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}
