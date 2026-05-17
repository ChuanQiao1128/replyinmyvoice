import { PricingSection } from "../../components/landing/pricing";
import { SiteFooter } from "../../components/site-footer";
import { SiteHeader } from "../../components/site-header";

export default function PricingPage() {
  return (
    <main className="min-h-screen bg-paper text-ink">
      <SiteHeader />
      <div className="mx-auto max-w-6xl px-6 pt-14">
        <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
          Pricing
        </p>
        <h1 className="mt-3 text-4xl font-semibold md:text-5xl">
          One clear plan for practical replies.
        </h1>
      </div>
      <PricingSection />
      <SiteFooter />
    </main>
  );
}
