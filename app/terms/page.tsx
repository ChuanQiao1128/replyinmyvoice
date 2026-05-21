import type { Metadata } from "next";

import { SiteHeader } from "../../components/site-header";

export const metadata: Metadata = {
  title: "Terms",
  description:
    "Basic product terms for using Reply In My Voice by TimeAwake Ltd.",
};

const sections = [
  {
    title: "Use of the product",
    text: "Reply In My Voice helps rewrite practical replies for everyday communication. You are responsible for reviewing any output before sending it.",
  },
  {
    title: "No guaranteed writing score",
    text: "Naturalness Check percentages are reference signals for comparison. They are not a promise that a message will be judged a certain way by any person or system.",
  },
  {
    title: "Billing and quota",
    text: "Paid access is NZD $9 per month for 40 successful rewrites per billing month. Quota resets at the start of each billing period; unused rewrites do not roll over. Subscriptions and payment details are managed through Stripe.",
  },
  {
    title: "Cancellation",
    text: "You can cancel your subscription at any time from the customer portal. Your paid quota stays active until the end of the current billing period; no partial-month refunds are issued for cancellations.",
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

export default function TermsPage() {
  return (
    <main className="min-h-screen bg-paper text-ink">
      <SiteHeader />
      <section className="mx-auto max-w-4xl px-6 py-14">
        <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
          TimeAwake Ltd.
        </p>
        <h1 className="mt-3 text-4xl font-semibold md:text-5xl">Terms</h1>
        <p className="mt-5 max-w-2xl leading-7 text-ink/65">
          These MVP terms describe the practical boundaries of using Reply In My
          Voice. They may be refined before a wider public launch.
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
