import { PricingSection } from "../../components/landing/pricing";
import { SiteHeader } from "../../components/site-header";

import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Pricing",
  description:
    "One clear plan for practical replies: NZD $9/month for 40 rewrites per billing month.",
};


export default function PricingPage() {
  return (
    <main className="min-h-screen bg-paper text-ink">
      <SiteHeader />
      <div className="mx-auto max-w-7xl px-4 pt-14 sm:px-6">
        <p className="text-sm font-semibold text-brick">
          Pricing
        </p>
        <h1 className="mt-3 text-4xl font-semibold md:text-5xl">
          One clear plan for practical replies.
        </h1>
      </div>
      <PricingSection />
    </main>
  );
}
