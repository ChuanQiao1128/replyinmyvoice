import { FAQ } from "../components/landing/faq";
import { Hero } from "../components/landing/hero";
import { HowItWorks } from "../components/landing/how-it-works";
import { PricingSection } from "../components/landing/pricing";
import { UseCases } from "../components/landing/use-cases";
import { SiteFooter } from "../components/site-footer";
import { SiteHeader } from "../components/site-header";

export default function HomePage() {
  return (
    <main className="min-h-screen bg-paper text-ink">
      <SiteHeader />
      <Hero />
      <UseCases />
      <HowItWorks />
      <PricingSection />
      <FAQ />
      <SiteFooter />
    </main>
  );
}
