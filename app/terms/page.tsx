import type { Metadata } from "next";

import { buildBreadcrumbListJsonLd } from "../../components/seo/json-ld";
import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "Terms",
  description:
    "Basic product terms for using Reply In My Voice by TimeAwake Ltd.",
  openGraph: {
    title: "Reply In My Voice terms",
    description:
      "Product terms for trial-code access, rewrite packs, Pro/API quota, cancellation, and responsible use.",
    url: "https://replyinmyvoice.com/terms",
    siteName: "Reply In My Voice",
    type: "website",
    images: "/og.png",
  },
  twitter: {
    card: "summary_large_image",
    title: "Reply In My Voice terms",
    description:
      "Product terms for trial-code access, rewrite packs, Pro/API quota, cancellation, and responsible use.",
    images: "/og.png",
  },
};

const sections = [
  {
    title: "Use of the product",
    text: "Reply In My Voice helps rewrite practical replies for everyday communication. You are responsible for reviewing any output before sending it.",
  },
  {
    title: "Workspace content and retention",
    text: "By submitting content for a rewrite, you agree that pasted messages, rough drafts, rewritten replies, and related quality metadata are processed to provide the request and retained for up to the configured retention window (default 90 days). After that window, raw content is removed. You can delete items from your history in the workspace.",
  },
  {
    title: "No guaranteed writing score",
    text: "AI Signal percentages are reference signals for comparison. They are not a promise that a message will be judged a certain way by any person or system.",
  },
  {
    title: "Billing and quota",
    text: "Trial access is granted by redeeming a trial code, which provides a small number of successful rewrites (currently 3). After that, you can buy one-time rewrite packs — for example Quick Pack (NZ$2.50 for 10 successful rewrites) and Value Pack (NZ$6.90 for 30 successful rewrites) — which are valid for 90 days from purchase. Pro/API is NZ$19.90 per month for 90 successful rewrites per billing month plus API access; the monthly quota resets at the start of each billing period and unused monthly rewrites do not roll over. Subscriptions, packs, and payment details are managed through Stripe-hosted Checkout and customer portal pages. Reply In My Voice does not collect or store full card numbers or card security codes.",
  },
  {
    title: "Cancellation",
    text: "You can cancel your Pro/API subscription at any time from the customer portal. Your paid quota stays active until the end of the current billing period; no partial-month refunds are issued for cancellations. One-time rewrite packs are not subscriptions and are not cancellable; unused pack rewrites simply expire 90 days after purchase.",
  },
  {
    title: "Refunds",
    text: "If a charge is in error or the service was materially unavailable, contact info@timeawake.co.nz within 14 days and we will work in good faith to resolve it. Nothing in these terms removes rights you may have under the New Zealand Consumer Guarantees Act 1993.",
  },
  {
    title: "Disputes and chargebacks",
    text: "Please contact us at info@timeawake.co.nz before opening a chargeback so we can investigate. We will respond within 5 business days. If a chargeback is opened, access to paid features may be paused until the matter is resolved.",
  },
  {
    title: "Governing law",
    text: "These terms are governed by the laws of New Zealand. Any dispute that cannot be resolved by good-faith communication will be handled in the courts of New Zealand. TimeAwake Ltd. is the operator of Reply In My Voice.",
  },
  {
    title: "Content responsibility",
    text: "You should only submit content that you have the right to use and should avoid highly sensitive personal information.",
  },
  {
    title: "Contact",
    text: "For product or billing questions, contact TimeAwake Ltd. at info@timeawake.co.nz.",
  },
];

const termsBreadcrumbJsonLd = buildBreadcrumbListJsonLd([
  { name: "Home", item: "https://replyinmyvoice.com/" },
  { name: "Terms", item: "https://replyinmyvoice.com/terms" },
]);

export default function TermsPage() {
  return (
    <main className="rimv">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: JSON.stringify(termsBreadcrumbJsonLd),
        }}
      />
      <SiteHeader />
      <section className="page">
        <div className="wrap" style={{ maxWidth: 920 }}>
          <div className="page-head">
            <div className="eyebrow">
              <span className="dot" />
              TimeAwake Ltd.
            </div>
            <h1>Terms</h1>
            <p className="lede">
              These MVP terms describe the practical boundaries of using Reply In
              My Voice. They may be refined before a wider public launch.
            </p>
            <div className="page-meta">Effective date · 22 May 2026</div>
          </div>
          <div className="card-stack">
            {sections.map((section) => (
              <article className="v2card" key={section.title}>
                <h2 style={{ fontSize: 18 }}>{section.title}</h2>
                <p>{section.text}</p>
              </article>
            ))}
          </div>
        </div>
      </section>
    </main>
  );
}
