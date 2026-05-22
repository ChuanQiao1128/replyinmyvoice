import { CheckCircle2, PenLine } from "lucide-react";
import Link from "next/link";

type AuthMode = "sign-in" | "sign-up";

const authCopy = {
  "sign-in": {
    eyebrow: "Welcome back",
    heading: "Sign in to your reply workspace.",
    body: "Continue with Google to open your drafts, remaining usage, and billing state.",
    cardTitle: "Continue securely",
    cardBody: "Google confirms your account before sending you back to the app.",
    alternateText: "Need an account?",
    alternateHref: "/sign-up",
    alternateLabel: "Start here",
  },
  "sign-up": {
    eyebrow: "Create your account",
    heading: "Start with three free reply rewrites.",
    body: "Use Google to create your account, then try the workspace before choosing the paid monthly plan.",
    cardTitle: "Create account with Google",
    cardBody: "You will land in the workspace after Google confirms your account.",
    alternateText: "Already signed up?",
    alternateHref: "/sign-in",
    alternateLabel: "Sign in",
  },
} satisfies Record<
  AuthMode,
  {
    eyebrow: string;
    heading: string;
    body: string;
    cardTitle: string;
    cardBody: string;
    alternateText: string;
    alternateHref: string;
    alternateLabel: string;
  }
>;

const highlights = [
  "Facts stay in the reply",
  "Warm or Direct tone",
  "40 rewrites on the paid plan",
];

export function GoogleOAuthCard({ mode = "sign-in" }: { mode?: AuthMode }) {
  const copy = authCopy[mode];

  return (
    <main className="min-h-screen bg-paper text-ink">
      <section className="border-b border-line bg-sky">
        <div className="mx-auto grid min-h-[calc(100vh-5rem)] max-w-6xl items-center gap-8 px-6 py-12 lg:grid-cols-[0.95fr_1fr]">
          <div className="max-w-xl">
            <Link
              href="/"
              className="inline-flex items-center gap-2 text-sm font-semibold text-ink transition hover:text-clay focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 focus-visible:ring-offset-2 focus-visible:ring-offset-sky"
            >
              <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-ink text-paper shadow-crisp">
                <PenLine className="h-4 w-4" aria-hidden="true" />
              </span>
              Reply In My Voice
            </Link>

            <p className="mt-8 text-sm font-semibold uppercase tracking-[0.18em] text-clay">
              {copy.eyebrow}
            </p>
            <h1 className="mt-3 text-4xl font-semibold leading-tight md:text-5xl">
              {copy.heading}
            </h1>
            <p className="mt-4 max-w-lg leading-7 text-ink/68">{copy.body}</p>

            <div className="mt-7 grid gap-3 text-sm text-ink/68 sm:grid-cols-3">
              {highlights.map((highlight) => (
                <div
                  key={highlight}
                  className="rounded-lg border border-line bg-white/75 p-3 shadow-crisp"
                >
                  <CheckCircle2
                    className="mb-2 h-4 w-4 text-sage"
                    aria-hidden="true"
                  />
                  <p>{highlight}</p>
                </div>
              ))}
            </div>
          </div>

          <section
            aria-labelledby="auth-card-title"
            className="w-full rounded-lg border border-line bg-white p-5 shadow-soft sm:p-6"
          >
            <p className="text-sm font-medium text-ink/58">Google account</p>
            <h2 id="auth-card-title" className="mt-2 text-2xl font-semibold">
              {copy.cardTitle}
            </h2>
            <p className="mt-2 text-sm leading-6 text-ink/62">{copy.cardBody}</p>

            <a
              href="/api/auth/login?redirectTo=/app"
              className="mt-7 flex min-h-12 w-full items-center justify-center gap-3 rounded-md border border-line bg-paper px-4 py-3 text-base font-semibold text-ink shadow-crisp transition hover:bg-paper-deep focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 focus-visible:ring-offset-2 focus-visible:ring-offset-white"
            >
              <span
                aria-hidden="true"
                className="flex h-7 w-7 items-center justify-center rounded-md border border-line bg-white text-sm font-bold"
              >
                G
              </span>
              Continue with Google
            </a>

            <div className="mt-6 border-t border-line pt-5 text-sm text-ink/62">
              <span>{copy.alternateText}</span>{" "}
              <Link
                href={copy.alternateHref}
                className="font-semibold text-ink underline decoration-line underline-offset-4 transition hover:text-clay focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 focus-visible:ring-offset-2 focus-visible:ring-offset-white"
              >
                {copy.alternateLabel}
              </Link>
            </div>
          </section>
        </div>
      </section>
    </main>
  );
}
