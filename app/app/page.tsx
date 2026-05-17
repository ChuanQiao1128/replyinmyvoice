import Link from "next/link";

export const dynamic = "force-dynamic";

export default function AppPage() {
  return (
    <main className="min-h-screen bg-paper px-6 py-10 text-ink">
      <section className="mx-auto max-w-5xl">
        <Link href="/" className="text-sm font-medium text-clay">
          Reply In My Voice
        </Link>
        <h1 className="mt-6 text-4xl font-semibold">Rewrite workspace</h1>
        <p className="mt-3 max-w-2xl text-ink/65">
          The workspace is being assembled. Authentication, quota, billing, and
          writing signal checks will be wired in the next phase.
        </p>
      </section>
    </main>
  );
}
