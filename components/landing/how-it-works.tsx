const steps = [
  {
    title: "Paste the thread",
    text: "Add the message you are answering and the rough reply you already have.",
  },
  {
    title: "Lock the facts",
    text: "Tell the workspace what happened, who the audience is, and what must not change.",
  },
  {
    title: "Choose the tone",
    text: "Use a warmer or more direct mode depending on the relationship and urgency.",
  },
  {
    title: "Review the signal",
    text: "Compare the before and after AI-like signal, then copy the reply when it feels right.",
  },
];

export function HowItWorks() {
  return (
    <section className="border-y border-line bg-paper-deep/50">
      <div className="mx-auto max-w-6xl px-6 py-16">
        <h2 className="text-3xl font-semibold md:text-4xl">How it works</h2>
        <div className="mt-8 grid gap-4 md:grid-cols-4">
          {steps.map((step, index) => (
            <div key={step.title} className="rounded-lg border border-line bg-paper p-5">
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
