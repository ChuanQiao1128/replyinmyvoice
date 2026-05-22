import { PricingSection } from "../../components/landing/pricing";
import { SiteHeader } from "../../components/site-header";
import { LinkButton } from "../../components/ui/button";
import { ArrowRight, CheckCircle2 } from "lucide-react";

import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Pricing",
  description:
    "One clear plan for practical replies: 3 free rewrites after sign-up, then NZD $9/month for 40 monthly rewrites.",
};

const pricingHighlights = [
  "3 free lifetime rewrites after sign-up",
  "40 rewrites per billing month on the paid plan",
  "Billing and cancellation handled through Stripe",
];

export default function PricingPage() {
  return (
    <main className="min-h-screen bg-paper text-ink">
      <SiteHeader />
      <section className="border-b border-line bg-sky">
        <div className="mx-auto grid max-w-6xl gap-8 px-6 py-14 md:grid-cols-[1fr_0.74fr] md:items-end">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
              Pricing
            </p>
            <h1 className="mt-3 max-w-3xl text-4xl font-semibold leading-tight md:text-5xl">
              One clear plan for practical replies.
            </h1>
            <p className="mt-5 max-w-2xl leading-7 text-ink/68">
              Start with three successful free rewrites after sign-up. Upgrade
              to the NZD $9 monthly plan when you want a steady workflow for
              teacher messages, sales follow-ups, workplace email, and client
              replies.
            </p>
            <LinkButton href="/sign-up" variant="clay" className="mt-7">
              Start with free rewrites
              <ArrowRight className="h-4 w-4" aria-hidden="true" />
            </LinkButton>
          </div>

          <div className="rounded-lg border border-line bg-white/80 p-5 shadow-soft">
            <p className="text-sm font-semibold text-ink">What is included</p>
            <ul className="mt-4 space-y-3">
              {pricingHighlights.map((highlight) => (
                <li key={highlight} className="flex gap-2 text-sm text-ink/68">
                  <CheckCircle2
                    className="mt-0.5 h-4 w-4 shrink-0 text-sage"
                    aria-hidden="true"
                  />
                  {highlight}
                </li>
              ))}
            </ul>
          </div>
        </div>
      </section>
      <PricingSection />
    </main>
  );
}
