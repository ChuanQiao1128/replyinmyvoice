export default function HomePage() {
  return (
    <main className="min-h-screen bg-paper text-ink">
      <section className="mx-auto flex min-h-screen max-w-6xl flex-col justify-center px-6 py-16">
        <p className="mb-4 text-sm font-semibold uppercase tracking-[0.18em] text-clay">
          Reply In My Voice
        </p>
        <h1 className="max-w-3xl text-5xl font-semibold leading-tight md:text-7xl">
          Replies that still sound like you.
        </h1>
        <p className="mt-6 max-w-2xl text-lg leading-8 text-ink/70">
          Turn rough drafts into clear, natural replies for students, customers,
          colleagues, and clients without losing your voice.
        </p>
        <div className="mt-8 flex flex-wrap gap-3">
          <a
            href="/app"
            className="rounded-md bg-ink px-5 py-3 text-sm font-semibold text-paper"
          >
            Start rewriting
          </a>
          <a
            href="/pricing"
            className="rounded-md border border-line px-5 py-3 text-sm font-semibold text-ink"
          >
            View pricing
          </a>
        </div>
      </section>
    </main>
  );
}
