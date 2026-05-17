"use client";

import { useMemo, useState } from "react";
import { ArrowRight, CheckCircle2 } from "lucide-react";

import { Card } from "../ui/card";

const scenarios = [
  {
    label: "Teacher message",
    context:
      "Maya asks whether she can still submit a missed reflection after a family issue.",
    draft:
      "Dear Maya, I acknowledge receipt of your email regarding the missed reflection. Late submissions are generally not accepted under the course policy. I will review the circumstances you described and determine whether any exception can be considered. Please be advised that approval is not guaranteed and further information may be required before a decision is made.",
    rewrite:
      "Hi Maya, thanks for letting me know what happened. I can look at this with you tomorrow and check it against the late-work policy before deciding the next step. If there is anything else I should understand about the family issue, send it through before then.",
    before: 81,
    after: 39,
  },
  {
    label: "Sales follow-up",
    context:
      "Jordan says the team is still comparing vendors and may need another week.",
    draft:
      "Hello Jordan, I am following up regarding the proposal sent last Tuesday. Please advise whether your team has completed its vendor comparison and whether you would like to proceed with the package as discussed. I would appreciate any update you can provide so that we can determine the appropriate next steps.",
    rewrite:
      "Hi Jordan, just checking back on the proposal from Tuesday. If your team is still comparing vendors, no problem. I can send a shorter version with the two options side by side, or answer anything that would help you decide next week.",
    before: 76,
    after: 41,
  },
  {
    label: "Workplace email",
    context:
      "A teammate needs revised numbers for a partner update, but the source file arrived late.",
    draft:
      "Unfortunately, the requested numbers are not available at this time because the source information was delayed. I understand that the partner update is important, and I will provide the revised figures as soon as the underlying file has been checked and the information is ready for circulation.",
    rewrite:
      "The source file came in late, so I need one more check before I send the revised numbers. I know you need them for the partner update. I will get the final version to you by 4pm Friday.",
    before: 73,
    after: 32,
  },
  {
    label: "Client reply",
    context:
      "Priya asks why this month's report totals look different from last month.",
    draft:
      "Dear Priya, we apologize for any inconvenience caused by the discrepancy in the report totals. Our team is currently looking into the matter and will provide an update as soon as possible. We appreciate your patience while we review the relevant information and determine what may have changed.",
    rewrite:
      "Hi Priya, thanks for flagging this. I am checking the report now because this month includes a category that was hidden last month. I will send you a clear line-by-line note today so you can see exactly what changed.",
    before: 79,
    after: 37,
  },
];

function SignalBar({
  label,
  value,
  variant = "clay",
}: {
  label: string;
  value: number;
  variant?: "clay" | "sage";
}) {
  const fillClass = variant === "sage" ? "bg-sage" : "bg-clay";

  return (
    <div>
      <div className="mb-1 flex items-center justify-between text-xs font-medium text-ink/65">
        <span>{label}</span>
        <span>{value}%</span>
      </div>
      <div className="h-2 rounded-full bg-paper-deep">
        <div
          className={`h-2 rounded-full ${fillClass}`}
          style={{ width: `${value}%` }}
        />
      </div>
    </div>
  );
}

export function InteractiveDemo() {
  const [index, setIndex] = useState(0);
  const scenario = scenarios[index];
  const change = useMemo(() => scenario.after - scenario.before, [scenario]);

  return (
    <Card id="examples" className="p-4 md:p-5">
      <div className="flex flex-wrap gap-2">
        {scenarios.map((item, itemIndex) => (
          <button
            key={item.label}
            className={`rounded-md border px-3 py-2 text-xs font-semibold transition ${
              itemIndex === index
                ? "border-ink bg-ink text-paper"
                : "border-line bg-white text-ink/70 hover:text-ink"
            }`}
            onClick={() => setIndex(itemIndex)}
            type="button"
          >
            {item.label}
          </button>
        ))}
      </div>
      <div className="mt-5 rounded-lg border border-line bg-paper-deep/65 px-4 py-3 text-sm leading-6 text-ink/68">
        {scenario.context}
      </div>
      <div className="mt-4 grid gap-4 md:grid-cols-[1fr_auto_1fr] md:items-stretch">
        <div className="rounded-lg border border-line bg-paper p-4 md:min-h-72">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink/45">
            Rough draft
          </p>
          <p className="mt-3 text-sm leading-6 text-ink/75">{scenario.draft}</p>
        </div>
        <div className="hidden items-center text-clay md:flex">
          <ArrowRight className="h-5 w-5" aria-hidden="true" />
        </div>
        <div className="rounded-lg border border-line bg-white p-4 md:min-h-72">
          <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.16em] text-sage">
            <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
            In your voice
          </p>
          <p className="mt-3 text-sm leading-6 text-ink">{scenario.rewrite}</p>
        </div>
      </div>
      <div className="mt-5 rounded-lg border border-line bg-white p-4">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <p className="font-semibold">Naturalness Check</p>
          <p className="text-sm font-medium text-sage">{change} pts</p>
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <SignalBar label="Draft AI-like signal" value={scenario.before} />
          <SignalBar
            label="Rewrite AI-like signal"
            value={scenario.after}
            variant="sage"
          />
        </div>
        <p className="mt-3 text-xs leading-5 text-ink/55">
          A third-party reference signal that helps compare how natural the draft
          and rewrite feel. It is not a guarantee; review the reply before sending.
        </p>
      </div>
    </Card>
  );
}
