const steps = [
  "Paste your rough draft",
  "Add the context that matters",
  "Get a reply that preserves the facts and sounds like you",
];

export function HowItWorks() {
  return (
    <section className="border-y border-line bg-paper-deep/50">
      <div className="mx-auto max-w-6xl px-6 py-16">
        <h2 className="text-3xl font-semibold md:text-4xl">How it works</h2>
        <div className="mt-8 grid gap-4 md:grid-cols-3">
          {steps.map((step, index) => (
            <div key={step} className="rounded-lg border border-line bg-paper p-5">
              <span className="text-sm font-semibold text-clay">0{index + 1}</span>
              <p className="mt-3 text-lg font-semibold">{step}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
