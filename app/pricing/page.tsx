import Link from "next/link";

export default function PricingPage() {
  return (
    <main className="min-h-screen bg-paper px-6 py-16 text-ink">
      <section className="mx-auto max-w-3xl">
        <Link href="/" className="text-sm font-medium text-clay">
          Reply In My Voice
        </Link>
        <h1 className="mt-6 text-4xl font-semibold">Simple pricing</h1>
        <div className="mt-8 rounded-lg border border-line bg-white/70 p-8 shadow-soft">
          <p className="text-sm font-semibold uppercase tracking-[0.14em] text-sage">
            Monthly
          </p>
          <p className="mt-3 text-5xl font-semibold">NZD $9</p>
          <p className="mt-2 text-ink/65">100 successful rewrites per billing period.</p>
          <a
            href="/app"
            className="mt-8 inline-flex rounded-md bg-ink px-5 py-3 text-sm font-semibold text-paper"
          >
            Start with the NZD $9 plan
          </a>
        </div>
      </section>
    </main>
  );
}
