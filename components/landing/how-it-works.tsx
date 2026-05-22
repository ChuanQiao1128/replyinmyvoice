const steps = [
  {
    title: "Paste the thread",
    text: "Paste the message you are answering and the rough reply you already have.",
  },
  {
    title: "Pick quick context",
    text: "Choose audience, purpose, and anything that must stay unchanged. Most fields are optional.",
  },
  {
    title: "Choose a tone preset",
    text: "Choose Warm or Direct, then let the app shape the reply around your context.",
  },
  {
    title: "Review the signal",
    text: "Compare the before and after AI-like signal, then copy the reply when it feels right.",
  },
];

export function HowItWorks() {
  return (
    <section className="border-y border-line bg-mint">
      <div className="mx-auto max-w-6xl px-6 py-16">
        <div className="max-w-2xl">
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-sage">
            Four quick steps
          </p>
          <h2 className="mt-3 text-3xl font-semibold md:text-4xl">How it works</h2>
        </div>
        <div className="mt-8 grid gap-4 md:grid-cols-4">
          {steps.map((step, index) => (
            <div key={step.title} className="rounded-lg border border-line bg-white p-5 shadow-crisp">
              <span className="text-sm font-semibold text-clay">0{index + 1}</span>
              <p className="mt-3 text-lg font-semibold">{step.title}</p>
              <p className="mt-2 text-sm leading-6 text-ink/62">{step.text}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
