import { BriefcaseBusiness, GraduationCap, Handshake, Users } from "lucide-react";

const useCases = [
  {
    title: "Teacher messages",
    text: "Reply to students with warmth and clarity while keeping the class policy and facts intact.",
    icon: GraduationCap,
  },
  {
    title: "Sales follow-ups",
    text: "Turn stiff follow-ups into relationship-aware notes without inventing promises.",
    icon: Handshake,
  },
  {
    title: "Workplace email",
    text: "Make internal updates easier to read and less formulaic.",
    icon: BriefcaseBusiness,
  },
  {
    title: "Client replies",
    text: "Respond to customers and clients with the right balance of care and precision.",
    icon: Users,
  },
];

export function UseCases() {
  return (
    <section className="mx-auto max-w-6xl px-6 py-16">
      <div className="max-w-2xl">
        <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
          Built for real replies
        </p>
        <h2 className="mt-3 text-3xl font-semibold md:text-4xl">
          Better everyday messages, not generic rewrites.
        </h2>
      </div>
      <div className="mt-8 grid gap-4 md:grid-cols-4">
        {useCases.map((item) => (
          <div key={item.title} className="rounded-lg border border-line bg-white/65 p-5">
            <item.icon className="h-5 w-5 text-clay" aria-hidden="true" />
            <h3 className="mt-4 font-semibold">{item.title}</h3>
            <p className="mt-2 text-sm leading-6 text-ink/65">{item.text}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
