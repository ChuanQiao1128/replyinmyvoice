import { SiteHeader } from "../../components/site-header";

function Block({
  className = "",
  rounded = "rounded-full",
}: {
  className?: string;
  rounded?: string;
}) {
  return (
    <span
      aria-hidden="true"
      className={`block animate-pulse bg-paper-deep ${rounded} ${className}`}
    />
  );
}

export default function AdminLoading() {
  return (
    <>
      <SiteHeader />
      <main className="wrap py-10" aria-busy="true">
        <div className="mb-8 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <Block className="h-4 w-32" />
            <Block className="mt-4 h-10 w-36 rounded-md" rounded="" />
          </div>
          <Block className="h-11 w-full rounded-md md:max-w-sm" rounded="" />
        </div>

        <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          {Array.from({ length: 5 }).map((_, index) => (
            <div
              className="rounded-lg border border-line bg-white/70 p-5 shadow-crisp"
              key={index}
            >
              <Block className="h-3 w-24" />
              <Block className="mt-4 h-7 w-20 rounded-md" rounded="" />
            </div>
          ))}
        </section>

        <div className="mt-6 rounded-md border border-line bg-white/50 px-5 py-4">
          <Block className="h-4 w-32" />
          <Block className="mt-3 h-4 w-64 max-w-full" />
        </div>

        <section className="mt-6 overflow-hidden rounded-lg border border-line bg-white/80 shadow-crisp">
          <div className="border-b border-line px-5 py-4">
            <Block className="h-6 w-56 rounded-md" rounded="" />
            <Block className="mt-3 h-4 w-28" />
          </div>
          <div className="divide-y divide-line">
            {Array.from({ length: 4 }).map((_, index) => (
              <div className="grid gap-4 px-5 py-4 md:grid-cols-5" key={index}>
                <Block className="h-4 w-44 max-w-full" />
                <Block className="h-4 w-28" />
                <Block className="h-4 w-36" />
                <Block className="h-4 w-full" />
                <Block className="h-9 w-24 rounded-md" rounded="" />
              </div>
            ))}
          </div>
        </section>

        <section className="mt-6 overflow-hidden rounded-lg border border-line bg-white/80 shadow-crisp">
          <div className="border-b border-line px-5 py-4">
            <Block className="h-6 w-24 rounded-md" rounded="" />
            <Block className="mt-3 h-4 w-44" />
          </div>
          <div className="divide-y divide-line">
            {Array.from({ length: 5 }).map((_, index) => (
              <div className="grid gap-4 px-5 py-4 md:grid-cols-7" key={index}>
                <Block className="h-4 w-48 max-w-full" />
                <Block className="h-6 w-20 rounded-md" rounded="" />
                <Block className="h-4 w-20" />
                <Block className="h-4 w-16" />
                <Block className="h-4 w-16" />
                <Block className="h-4 w-24" />
                <Block className="h-9 w-20 rounded-md" rounded="" />
              </div>
            ))}
          </div>
        </section>
      </main>
    </>
  );
}
