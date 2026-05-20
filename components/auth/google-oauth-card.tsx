export function GoogleOAuthCard() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-paper px-6">
      <section className="w-full max-w-md rounded-2xl border border-line bg-white p-8 shadow-sm">
        <div className="space-y-2 text-center">
          <h1 className="text-3xl font-semibold text-ink">Continue to Reply In My Voice</h1>
          <p className="text-sm text-muted">Use your Google account to open your workspace.</p>
        </div>

        <a
          href="/api/auth/login?redirectTo=/app"
          className="mt-8 flex w-full items-center justify-center gap-3 rounded-xl border border-line bg-white px-4 py-3 text-base font-semibold text-ink shadow-sm transition hover:bg-paper disabled:cursor-not-allowed disabled:opacity-60"
        >
          <span aria-hidden="true" className="text-xl">G</span>
          Continue with Google
        </a>
      </section>
    </main>
  );
}
