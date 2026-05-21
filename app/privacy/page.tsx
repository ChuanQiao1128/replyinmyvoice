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
    text: "When you use the rewrite workspace, the app processes the message context, rough draft, tone preference, and facts you provide so it can produce a revised reply and writing signal.",
  },
  {
    title: "Reply content and quality improvement",
    text: "The app may store submitted message context, rough drafts, rewritten replies, writing-signal results, and rewrite metadata for internal quality improvement, debugging, and strategy evaluation. This helps us improve the rewrite and repair system over time. We do not sell this content or publish it publicly.",
  },
  {
    title: "Not used for ad targeting or model training",
    text: "We do not sell your content, share it for ad targeting, or use it to train third-party AI models. We may use your draft and rewritten content internally to improve our own rewrite quality (e.g., adjusting prompts and strategies). Telemetry is for our own product quality only.",
  },
  {
    title: "Account and billing data",
    text: "The app stores account identifiers, subscription status, usage counts, and Stripe event records needed to run access control and billing. Payment details are handled by Stripe.",
  },
  {
    title: "Vendors and subprocessors",
    text: "We use Cloudflare (hosting and edge runtime), Neon (Postgres database), Microsoft Entra External ID (sign-in), Stripe (payments), and AI providers for rewrite generation (DeepSeek) and writing-signal analysis (Sapling). These subprocessors process the data necessary to deliver the service and are bound by their own data protection terms.",
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
    <main className="min-h-screen bg-paper text-ink">
      <SiteHeader />
      <section className="mx-auto max-w-4xl px-6 py-14">
        <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
          TimeAwake Ltd.
        </p>
        <h1 className="mt-3 text-4xl font-semibold md:text-5xl">Privacy</h1>
        <p className="mt-5 max-w-2xl leading-7 text-ink/65">
          This page summarizes the MVP data boundaries for Reply In My Voice.
          It is written for clarity and will be expanded as the product matures.
        </p>
        <p className="mt-3 text-sm text-ink/55">Effective date: 22 May 2026.</p>
        <div className="mt-10 grid gap-4">
          {sections.map((section) => (
            <article
              className="rounded-lg border border-line bg-white/70 p-5"
              key={section.title}
            >
              <h2 className="text-lg font-semibold">{section.title}</h2>
              <p className="mt-2 leading-7 text-ink/65">{section.text}</p>
            </article>
          ))}
        </div>
      </section>
    </main>
  );
}
