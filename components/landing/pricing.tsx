import Link from "next/link";
import { Check } from "lucide-react";

import { LinkButton } from "../ui/button";

const features = [
  "40 rewrites per billing month",
  "3 free rewrites after sign-up",
  "Naturalness Check before and after",
  "Warm and Direct tone presets",
  "Cancel anytime",
];

export function PricingSection() {
  return (
    <section className="mx-auto max-w-7xl px-4 py-16 sm:px-6">
      <div className="grid gap-8 md:grid-cols-[1fr_380px] md:items-start">
        <div className="max-w-3xl">
          <p className="text-sm font-semibold text-brick">
            Simple pricing
          </p>
          <h2 className="mt-3 text-3xl font-semibold md:text-4xl">
            Start with the NZD $9 plan.
          </h2>
          <p className="mt-4 max-w-2xl leading-7 text-ink/65">
            Every signed-in user gets 3 free rewrites first. Upgrade when you
            want a steady monthly workflow for real replies. Billing is handled
            through Stripe.
          </p>
          <div className="mt-8 grid gap-3 text-sm text-ink/62 sm:grid-cols-3">
            <div className="rounded-lg border border-line bg-cream p-4">
              <p className="font-semibold text-ink">Free start</p>
              <p className="mt-1">3 successful rewrites</p>
            </div>
            <div className="rounded-lg border border-line bg-cream p-4">
              <p className="font-semibold text-ink">Paid plan</p>
              <p className="mt-1">40 per billing month</p>
            </div>
            <div className="rounded-lg border border-line bg-cream p-4">
              <p className="font-semibold text-ink">Company</p>
              <p className="mt-1">TimeAwake Ltd.</p>
            </div>
          </div>
        </div>
        <div className="rounded-lg border border-evergreen/25 bg-white p-5 shadow-panel">
          <p className="text-sm font-medium text-ink/60">Reply In My Voice</p>
          <div className="mt-3 flex items-end gap-2">
            <span className="text-4xl font-semibold">NZD $9</span>
            <span className="pb-1 text-sm text-ink/55">/month</span>
          </div>
          <ul className="mt-5 space-y-3">
            {features.map((feature) => (
              <li key={feature} className="flex gap-2 text-sm text-ink/70">
                <Check className="mt-0.5 h-4 w-4 shrink-0 text-sage" aria-hidden="true" />
                {feature}
              </li>
            ))}
          </ul>
          <LinkButton href="/sign-up" className="mt-6 w-full" variant="clay">
            Start with the NZD $9 plan
          </LinkButton>
          <p className="mt-4 text-center text-xs text-ink/55">
            By subscribing you agree to our{" "}
            <Link href="/terms" className="underline hover:text-ink">
              Terms
            </Link>{" "}
            and{" "}
            <Link href="/privacy" className="underline hover:text-ink">
              Privacy Policy
            </Link>
            .
          </p>
        </div>
      </div>
    </section>
  );
}
