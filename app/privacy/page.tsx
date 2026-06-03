import type { Metadata } from "next";

import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "Privacy",
  description:
    "How Reply In My Voice handles account, billing, and reply workspace data.",
};

const sections = [
  {
    title: "What the workspace processes",
    text: "When you use the rewrite workspace, the app processes the pasted messages, rough drafts, tone preference, and facts you provide so it can produce a revised reply and writing signal.",
  },
  {
    title: "Reply content retention and deletion",
    text: "Submitted message content, rough drafts, rewritten replies, writing-signal results, and rewrite metadata are retained for up to the configured retention window (default 90 days). After that window, raw content is removed. You can delete items from your history in the app workspace.",
  },
  {
    title: "Quality improvement",
    text: "During the retention window, stored content may be used for internal quality improvement, debugging, and strategy evaluation. This helps us improve the rewrite and repair system over time. We do not sell this content or publish it publicly.",
  },
  {
    title: "Not used for ad targeting or model training",
    text: "We do not sell your content, share it for ad targeting, or use it to train third-party AI models. We may use your draft and rewritten content internally to improve our own rewrite quality (e.g., adjusting prompts and strategies). Telemetry is for our own product quality only.",
  },
  {
    title: "Account and billing data",
    text: "The app stores account identifiers, subscription status, usage counts, and Stripe event records needed to run access control and billing. Payment details are entered on Stripe-hosted Checkout and customer portal pages. Reply In My Voice does not collect or store full card numbers or card security codes.",
  },
  {
    title: "Vendors and subprocessors",
    text: "We use Cloudflare (hosting and edge runtime), Microsoft Azure (SQL database and backend services), Microsoft Entra External ID (sign-in), Stripe (payments), and AI providers for rewrite generation (DeepSeek) and writing-signal analysis (Sapling). These subprocessors process the data necessary to deliver the service and are bound by their own data protection terms.",
  },
  {
    title: "Local history",
    text: "Your recent workspace history may also be stored in your browser local storage so you can revisit recent outputs on the same device. You can clear this local history from the app workspace.",
  },
  {
    title: "Safety reminder",
    text: "Do not paste passwords, payment details, government identifiers, or highly sensitive personal information into the workspace.",
  },
  {
    title: "Contact",
    text: "For privacy questions, contact TimeAwake Ltd. at info@timeawake.co.nz.",
  },
];

export default function PrivacyPage() {
  return (
    <main className="rimv">
      <SiteHeader />
      <section className="page">
        <div className="wrap" style={{ maxWidth: 920 }}>
          <div className="page-head">
            <div className="eyebrow">
              <span className="dot" />
              TimeAwake Ltd.
            </div>
            <h1>Privacy</h1>
            <p className="lede">
              This page summarizes the MVP data boundaries for Reply In My Voice.
              It is written for clarity and will be expanded as the product matures.
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
