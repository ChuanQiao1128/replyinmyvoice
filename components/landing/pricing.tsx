import { Check } from "lucide-react";

import { LinkButton } from "../ui/button";
import { Card } from "../ui/card";

const features = [
  "100 rewrites per billing month",
  "Naturalness Check before and after",
  "Warm and Direct reply modes",
  "Cancel anytime",
];

export function PricingSection() {
  return (
    <section className="mx-auto max-w-6xl px-6 py-16">
      <Card className="grid gap-8 p-6 md:grid-cols-[1fr_0.8fr] md:p-8">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
            Simple pricing
          </p>
          <h2 className="mt-3 text-3xl font-semibold md:text-4xl">
            Start with the NZD $9 plan.
          </h2>
          <p className="mt-4 max-w-2xl leading-7 text-ink/65">
            Every signed-in user gets 3 free rewrites first. Upgrade when you
            want a steady monthly workflow for real replies.
          </p>
        </div>
        <div className="rounded-lg border border-line bg-white p-5">
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
        </div>
      </Card>
    </section>
  );
}
