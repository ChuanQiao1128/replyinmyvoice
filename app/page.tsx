import { ClosingCta } from "../components/landing/closing-cta";
import { FAQ } from "../components/landing/faq";
import { Hero } from "../components/landing/hero";
import { HowItWorks } from "../components/landing/how-it-works";
import { InteractiveDemo } from "../components/landing/interactive-demo";
import { PricingSection } from "../components/landing/pricing";
import { TrustPanel } from "../components/landing/trust-panel";
import { UseCases } from "../components/landing/use-cases";
import { SiteHeader } from "../components/site-header";

export default function HomePage() {
  return (
    <main className="min-h-screen bg-paper text-ink">
      <SiteHeader />
      <Hero />
      <TrustPanel />
      <section className="border-b border-line bg-cream">
        <div className="mx-auto max-w-7xl px-4 py-16 sm:px-6">
          <div className="mb-8 max-w-2xl">
            <p className="text-sm font-semibold text-brick">Example workflow</p>
            <h2 className="mt-3 text-3xl font-semibold md:text-4xl">
              From stiff draft to send-ready reply.
            </h2>
          </div>
          <InteractiveDemo />
        </div>
      </section>
      <UseCases />
      <HowItWorks />
      <PricingSection />
      <FAQ />
      <ClosingCta />
    </main>
  );
}
