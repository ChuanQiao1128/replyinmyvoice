import { ArrowRight, Mail, MessageSquareText } from "lucide-react";

import { LinkButton } from "../ui/button";
import { InteractiveDemo } from "./interactive-demo";

export function Hero() {
  return (
    <section className="mx-auto grid min-h-[calc(100vh-4rem)] max-w-6xl gap-10 px-6 py-14 md:grid-cols-[0.92fr_1.08fr] md:items-center">
      <div>
        <div className="mb-5 inline-flex items-center gap-2 rounded-md border border-line bg-white/70 px-3 py-2 text-sm font-medium text-ink/65">
          <Mail className="h-4 w-4 text-clay" aria-hidden="true" />
          Teacher messages, sales follow-ups, workplace email
        </div>
        <h1 className="max-w-3xl text-5xl font-semibold leading-tight md:text-7xl">
          Replies that still sound like you.
        </h1>
        <p className="mt-6 max-w-2xl text-lg leading-8 text-ink/70">
          Turn rough drafts into clear, natural replies for students, customers,
          colleagues, and clients without losing your voice.
        </p>
        <div className="mt-8 flex flex-wrap gap-3">
          <LinkButton href="/sign-up">
            Start rewriting
            <ArrowRight className="h-4 w-4" aria-hidden="true" />
          </LinkButton>
          <LinkButton href="#examples" variant="secondary">
            See examples
          </LinkButton>
        </div>
        <div className="mt-8 flex items-center gap-3 text-sm text-ink/60">
          <MessageSquareText className="h-4 w-4 text-sage" aria-hidden="true" />
          Preserves facts, softens tone, and keeps replies send-ready.
        </div>
      </div>
      <InteractiveDemo />
    </section>
  );
}
