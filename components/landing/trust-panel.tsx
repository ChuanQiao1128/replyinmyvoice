import { ClipboardCheck, DatabaseZap, Gauge, ShieldCheck } from "lucide-react";

const items = [
  {
    title: "Fact-aware rewriting",
    text: "The workspace asks for the facts that must stay intact before it shapes the reply.",
    icon: ClipboardCheck,
  },
  {
    title: "Naturalness Check",
    text: "Before and after percentages give a reference writing signal without promising a perfect score.",
    icon: Gauge,
  },
  {
    title: "Draft storage boundary",
    text: "Reply content is processed for the request and is not saved to our database.",
    icon: DatabaseZap,
  },
  {
    title: "Commercial billing",
    text: "Subscriptions are managed through Stripe for NZD $9/month, operated by TimeAwake Ltd.",
    icon: ShieldCheck,
  },
];

export function TrustPanel() {
  return (
    <section className="border-b border-line bg-white">
      <div className="mx-auto max-w-6xl px-6 py-14">
        <div className="grid gap-8 lg:grid-cols-[0.7fr_1.3fr] lg:items-start">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-clay">
              Built for trust
            </p>
            <h2 className="mt-3 text-3xl font-semibold md:text-4xl">
              Built for real communication workflows.
            </h2>
            <p className="mt-4 leading-7 text-ink/65">
              Reply In My Voice is designed around the moments where people
              already use draft assistance: student replies, sales follow-ups,
              client support, and workplace updates.
            </p>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            {items.map((item) => (
              <div
                className="rounded-lg border border-line bg-paper p-5 shadow-crisp"
                key={item.title}
              >
                <item.icon className="h-5 w-5 text-sage" aria-hidden="true" />
                <h3 className="mt-4 font-semibold">{item.title}</h3>
                <p className="mt-2 text-sm leading-6 text-ink/65">{item.text}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
