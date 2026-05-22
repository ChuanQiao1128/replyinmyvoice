import { ArrowRight, Mail, MessageSquareText } from "lucide-react";

import { LinkButton } from "../ui/button";
import { InteractiveDemo } from "./interactive-demo";

export function Hero() {
  return (
    <section className="border-b border-line bg-sky">
      <div className="mx-auto grid w-full max-w-6xl min-w-0 gap-8 px-4 py-10 sm:px-6 md:grid-cols-[0.88fr_1.12fr] md:items-center lg:py-12">
        <div className="min-w-0">
          <div className="mb-5 inline-flex max-w-full min-w-0 items-center gap-2 rounded-md border border-line bg-white/80 px-3 py-2 text-sm font-medium text-ink/68 shadow-crisp">
            <Mail className="h-4 w-4 shrink-0 text-clay" aria-hidden="true" />
            <span className="min-w-0 truncate">
              Teacher messages, sales follow-ups, workplace email
            </span>
          </div>
          <h1 className="max-w-3xl text-4xl font-semibold leading-tight sm:text-5xl md:text-6xl">
            Replies that still sound like you.
          </h1>
          <p className="mt-5 max-w-2xl text-lg leading-8 text-ink/72">
            Turn rough drafts into clear, natural replies for students, customers,
            colleagues, and clients without losing the facts or your voice.
          </p>
          <div className="mt-7 flex flex-wrap gap-3">
            <LinkButton href="/sign-up">
              Start rewriting
              <ArrowRight className="h-4 w-4" aria-hidden="true" />
            </LinkButton>
            <LinkButton href="#examples" variant="secondary">
              See examples
            </LinkButton>
          </div>
          <div className="mt-7 grid gap-3 text-sm text-ink/64 sm:grid-cols-3">
            <div className="border-l-2 border-clay pl-3">
              <span className="block font-semibold text-ink">3 free rewrites</span>
              after sign-up
            </div>
            <div className="border-l-2 border-sage pl-3">
              <span className="block font-semibold text-ink">40 monthly</span>
              on the NZD $9 plan
            </div>
            <div className="border-l-2 border-gold pl-3">
              <span className="block font-semibold text-ink">Warm or Direct</span>
              simple tone presets
            </div>
          </div>
          <div className="mt-7 flex items-center gap-3 text-sm text-ink/62">
            <MessageSquareText className="h-4 w-4 shrink-0 text-sage" aria-hidden="true" />
            Preserves facts, softens tone, and keeps replies send-ready.
          </div>
        </div>
        <InteractiveDemo />
      </div>
    </section>
  );
}
