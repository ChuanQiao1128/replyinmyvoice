"use client";

import { useMemo, useState } from "react";
import { ArrowRight, CheckCircle2 } from "lucide-react";

import { Card } from "../ui/card";

const scenarios = [
  {
    label: "Teacher message",
    draft:
      "Dear Student, I acknowledge receipt of your email. I will review the situation and respond accordingly. Please be advised that late submissions may be subject to policy.",
    rewrite:
      "Hi Maya, thanks for letting me know what happened. I can review this with you tomorrow, and we will look at the late-work policy together before deciding the next step.",
    before: 78,
    after: 34,
  },
  {
    label: "Sales follow-up",
    draft:
      "Hello, following up on our previous communication. Please advise whether you would like to proceed with the proposal as discussed.",
    rewrite:
      "Hi Jordan, I wanted to check back on the proposal from Tuesday. If the timing still works, I can send a tighter version with the two options we discussed.",
    before: 72,
    after: 38,
  },
  {
    label: "Workplace email",
    draft:
      "I am writing to inform you that the document has been completed and is available for your review at your earliest convenience.",
    rewrite:
      "The document is ready for review. I left a few notes in the margin where I think your input would help most.",
    before: 69,
    after: 29,
  },
  {
    label: "Client reply",
    draft:
      "We apologize for any inconvenience caused. Our team is currently looking into the matter and will provide an update soon.",
    rewrite:
      "Thanks for flagging this. I am checking it with the team now and will send you a clear update once we know what changed.",
    before: 74,
    after: 36,
  },
];

function SignalBar({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <div className="mb-1 flex items-center justify-between text-xs font-medium text-ink/65">
        <span>{label}</span>
        <span>{value}%</span>
      </div>
      <div className="h-2 rounded-full bg-paper-deep">
        <div
          className="h-2 rounded-full bg-clay"
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
      <div className="mt-5 grid gap-4 md:grid-cols-[1fr_auto_1fr] md:items-stretch">
        <div className="rounded-lg border border-line bg-paper p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink/45">
            Rough draft
          </p>
          <p className="mt-3 text-sm leading-6 text-ink/75">{scenario.draft}</p>
        </div>
        <div className="hidden items-center text-clay md:flex">
          <ArrowRight className="h-5 w-5" aria-hidden="true" />
        </div>
        <div className="rounded-lg border border-line bg-white p-4">
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
          <SignalBar label="Rewrite AI-like signal" value={scenario.after} />
        </div>
        <p className="mt-3 text-xs leading-5 text-ink/55">
          A third-party reference signal that helps compare how natural the draft
          and rewrite feel. It is not a guarantee; review the reply before sending.
        </p>
      </div>
    </Card>
  );
}
