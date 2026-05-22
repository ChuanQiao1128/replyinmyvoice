import { ArrowRight, ShieldCheck } from "lucide-react";
import Link from "next/link";

export function ClosingCta() {
  return (
    <section className="bg-paper px-6 py-8">
      <div className="mx-auto max-w-6xl rounded-lg bg-ink p-6 text-paper shadow-soft md:p-8">
        <div className="grid gap-6 md:grid-cols-[1fr_auto] md:items-center">
          <div>
            <p className="flex items-center gap-2 text-sm font-semibold text-paper/65">
              <ShieldCheck className="h-4 w-4" aria-hidden="true" />
              Operated by TimeAwake Ltd.
            </p>
            <h2 className="mt-3 text-3xl font-semibold md:text-4xl">
              Prepare replies that feel ready to send.
            </h2>
            <p className="mt-3 max-w-2xl leading-7 text-paper/68">
              Keep the facts intact, choose the tone, compare the writing signal,
              and copy a cleaner reply into your existing email workflow.
            </p>
          </div>
          <Link
            className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-paper px-4 py-2 text-sm font-semibold text-ink transition hover:bg-paper-deep"
            href="/sign-up"
          >
            Start rewriting
            <ArrowRight className="h-4 w-4" aria-hidden="true" />
          </Link>
        </div>
      </div>
    </section>
  );
}
