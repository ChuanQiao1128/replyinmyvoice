import type { Metadata } from "next";

import { ClosingCta } from "../components/landing/closing-cta";
import { FAQ } from "../components/landing/faq";
import { Hero } from "../components/landing/hero";
import { HowItWorks } from "../components/landing/how-it-works";
import { Naturalness } from "../components/landing/naturalness";
import { PricingV2 } from "../components/landing/pricing-v2";
import { StickyCta } from "../components/landing/sticky-cta";
import { TrustPanel } from "../components/landing/trust-panel";
import { UseCases } from "../components/landing/use-cases";
import { SiteHeader } from "../components/site-header";
import { getCurrentSession } from "../lib/entra-auth";

export const metadata: Metadata = {
  title: { absolute: "Reply In My Voice: replies that still sound like you" },
  description:
    "Paste a stiff, generic, or over-polished draft and get a clear, natural reply that keeps your facts and your voice. For teacher, sales, workplace, and client replies.",
  openGraph: {
    title: "Reply In My Voice: replies that still sound like you",
    description:
      "Paste a draft and get a clear, natural reply that keeps your facts and your voice.",
    url: "https://replyinmyvoice.com/",
    siteName: "Reply In My Voice",
    type: "website",
  },
};

export default async function HomePage() {
  const session = await getCurrentSession();
  const signedIn = Boolean(session);

  return (
    <main className="rimv">
      <SiteHeader />
      <Hero signedIn={signedIn} />
      <StickyCta signedIn={signedIn} />
      <UseCases />
      <HowItWorks />
      <Naturalness />
      <TrustPanel />
      <PricingV2 />
      <FAQ />
      <ClosingCta signedIn={signedIn} />
    </main>
  );
}
