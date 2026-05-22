import {
  ArrowRight,
  CheckCircle2,
  Mail,
  MessageSquareText,
  ShieldCheck,
} from "lucide-react";

import { LinkButton } from "../ui/button";

function WritingDeskScene() {
  return (
    <div
      aria-hidden="true"
      className="pointer-events-none absolute inset-0 overflow-hidden"
    >
      <div className="absolute inset-y-0 right-0 hidden w-[58%] bg-mist/60 lg:block" />
      <div className="absolute right-[5%] top-20 hidden w-[520px] rotate-[-2deg] rounded-lg border border-line bg-cream p-4 shadow-panel lg:block">
        <div className="flex items-center justify-between border-b border-line pb-3">
          <div>
            <p className="text-xs font-semibold text-evergreen">Message</p>
            <p className="mt-1 text-sm font-semibold text-ink">
              Parent question about Friday
            </p>
          </div>
          <span className="rounded-md bg-paper-deep px-2 py-1 text-xs font-semibold text-ink/55">
            2 min ago
          </span>
        </div>
        <div className="mt-4 space-y-2 text-sm leading-6 text-ink/64">
          <p>Thanks for the update. Could you confirm the new pickup time?</p>
          <p>I want to make sure Maya has the right details before Friday.</p>
        </div>
      </div>
      <div className="absolute bottom-12 right-[12%] hidden w-[560px] rounded-lg border border-evergreen/25 bg-white p-5 shadow-panel lg:block">
        <div className="flex items-center gap-2 text-sm font-semibold text-evergreen">
          <CheckCircle2 className="h-4 w-4" />
          Rewritten reply
        </div>
        <p className="mt-4 text-lg leading-8 text-ink">
          Thanks for checking. Friday pickup is still at 3:15, and I will let you
          know before lunch if anything changes.
        </p>
        <div className="mt-5 grid gap-3 text-xs font-semibold text-ink/60 sm:grid-cols-3">
          <span className="rounded-md bg-mist px-3 py-2">Facts preserved</span>
          <span className="rounded-md bg-paper-deep px-3 py-2">Warm tone</span>
          <span className="rounded-md bg-cream px-3 py-2">Ready to send</span>
        </div>
      </div>
      <div className="absolute -bottom-16 left-0 right-0 h-36 bg-gradient-to-t from-paper to-transparent" />
    </div>
  );
}

export function Hero() {
  return (
    <section className="relative min-h-[calc(92vh-4rem)] overflow-hidden border-b border-line bg-paper text-ink">
      <WritingDeskScene />
      <div className="relative mx-auto max-w-7xl px-4 py-16 sm:px-6 md:py-20 lg:py-24">
        <div className="max-w-3xl">
          <div className="mb-5 inline-flex items-center gap-2 rounded-md border border-line bg-cream/82 px-3 py-2 text-sm font-medium text-ink/66 shadow-sm">
            <Mail className="h-4 w-4 text-brick" aria-hidden="true" />
            Teacher messages, sales follow-ups, workplace email
          </div>
          <h1 className="max-w-3xl text-5xl font-semibold leading-tight md:text-7xl">
            Reply In My Voice
          </h1>
          <p className="mt-5 text-2xl font-semibold leading-9 text-evergreen md:text-3xl">
            Replies that still sound like you.
          </p>
          <p className="mt-5 max-w-2xl text-lg leading-8 text-ink/70">
            Turn rough drafts into clear, natural replies for students,
            customers, colleagues, and clients while keeping the facts intact.
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
          <div className="mt-8 grid max-w-2xl gap-3 text-sm text-ink/64 sm:grid-cols-2">
            <div className="flex items-center gap-2">
              <MessageSquareText
                className="h-4 w-4 text-evergreen"
                aria-hidden="true"
              />
              Draft plus context workflow
            </div>
            <div className="flex items-center gap-2">
              <ShieldCheck className="h-4 w-4 text-brick" aria-hidden="true" />
              Facts reviewed before output
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
