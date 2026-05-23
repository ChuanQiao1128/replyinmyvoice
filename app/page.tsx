import { ClosingCta } from "../components/landing/closing-cta";
import { FAQ } from "../components/landing/faq";
import { Hero } from "../components/landing/hero";
import { HowItWorks } from "../components/landing/how-it-works";
import { Naturalness } from "../components/landing/naturalness";
import { PricingV2 } from "../components/landing/pricing-v2";
import { TrustPanel } from "../components/landing/trust-panel";
import { UseCases } from "../components/landing/use-cases";
import { SiteHeader } from "../components/site-header";

export default function HomePage() {
  return (
    <main className="rimv">
      <SiteHeader />
      <Hero />
      <UseCases />
      <HowItWorks />
      <Naturalness />
      <TrustPanel />
      <PricingV2 />
      <FAQ />
      <ClosingCta />
    </main>
  );
}
