import { ClosingCta } from "../components/landing/closing-cta";
import { FAQ } from "../components/landing/faq";
import { Hero } from "../components/landing/hero";
import { HowItWorks } from "../components/landing/how-it-works";
import { PricingSection } from "../components/landing/pricing";
import { TrustPanel } from "../components/landing/trust-panel";
import { UseCases } from "../components/landing/use-cases";
import { SiteFooter } from "../components/site-footer";
import { SiteHeader } from "../components/site-header";

export default function HomePage() {
  return (
    <main className="min-h-screen bg-paper text-ink">
      <SiteHeader />
      <Hero />
      <TrustPanel />
      <UseCases />
      <HowItWorks />
      <PricingSection />
      <FAQ />
      <ClosingCta />
      <SiteFooter />
    </main>
  );
}
