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
    title: "Billing",
    text: "Paid access is NZD $9/month for 40 successful rewrites per billing month. Subscriptions and payment details are managed through Stripe.",
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
