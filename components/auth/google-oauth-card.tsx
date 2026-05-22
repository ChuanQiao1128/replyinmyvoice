import { ArrowRight, MailCheck, ShieldCheck } from "lucide-react";

export function GoogleOAuthCard() {
  return (
    <main className="min-h-screen bg-paper px-4 py-12 text-ink sm:px-6">
      <section className="mx-auto grid max-w-5xl gap-8 md:grid-cols-[1fr_420px] md:items-center">
        <div className="max-w-xl">
          <div className="mb-5 flex h-11 w-11 items-center justify-center rounded-md bg-evergreen text-cream">
            <MailCheck className="h-5 w-5" aria-hidden="true" />
          </div>
          <p className="text-sm font-semibold text-brick">Secure workspace</p>
          <h1 className="mt-3 text-4xl font-semibold leading-tight md:text-5xl">
            Continue to Reply In My Voice
          </h1>
          <p className="mt-4 text-lg leading-8 text-ink/68">
            Sign in to rewrite everyday replies, keep facts visible, and track
            your remaining usage in one focused workspace.
          </p>
          <p className="mt-5 flex items-center gap-2 text-sm text-ink/55">
            <ShieldCheck className="h-4 w-4 text-evergreen" aria-hidden="true" />
            Operated by TimeAwake Ltd.
          </p>
        </div>

        <div className="rounded-lg border border-line bg-cream p-6 shadow-panel">
          <div className="rounded-md bg-mist/70 p-4 text-sm leading-6 text-ink/65">
            Open the app to paste the message, add your rough draft, choose Warm
            or Direct, and copy the rewritten reply after review.
          </div>
          <a
            href="/api/auth/login?redirectTo=/app"
            className="mt-5 flex w-full items-center justify-center gap-3 rounded-md border border-evergreen bg-evergreen px-4 py-3 text-base font-semibold text-cream shadow-sm transition hover:bg-evergreen/90"
          >
            <span aria-hidden="true" className="text-lg font-bold">
              G
            </span>
            Continue with Google
            <ArrowRight className="h-4 w-4" aria-hidden="true" />
          </a>
          <p className="mt-4 text-center text-xs leading-5 text-ink/50">
            You will return to the rewrite workspace after signing in.
          </p>
        </div>
      </section>
    </main>
  );
}
