"use client";

import { useMemo, useState } from "react";
import { ArrowRight, CheckCircle2 } from "lucide-react";

import { homepageSampleCases } from "./sample-cases";

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
  const scenario = homepageSampleCases[index];
  const change = useMemo(() => scenario.after - scenario.before, [scenario]);

  return (
    <div
      id="examples"
      className="rounded-lg border border-line bg-paper p-4 shadow-panel md:p-5"
    >
      <div className="flex flex-wrap gap-2 border-b border-line pb-4">
        {homepageSampleCases.map((item, itemIndex) => (
          <button
            key={item.label}
            className={`rounded-md border px-3 py-2 text-xs font-semibold transition ${
              itemIndex === index
                ? "border-evergreen bg-evergreen text-cream"
                : "border-line bg-cream text-ink/70 hover:border-evergreen/35 hover:text-ink"
            }`}
            onClick={() => setIndex(itemIndex)}
            type="button"
          >
            {item.label}
          </button>
        ))}
      </div>
      <div className="mt-5 rounded-md bg-mist/70 px-4 py-3 text-sm leading-6 text-ink/68">
        {scenario.context}
      </div>
      <div className="mt-4 grid gap-4 md:grid-cols-[1fr_auto_1fr] md:items-stretch">
        <div className="rounded-md bg-cream p-4 ring-1 ring-line md:min-h-72">
          <p className="text-xs font-semibold uppercase text-ink/45">
            Rough draft
          </p>
          <p className="mt-3 text-sm leading-6 text-ink/75">{scenario.draft}</p>
        </div>
        <div className="hidden items-center text-brick md:flex">
          <ArrowRight className="h-5 w-5" aria-hidden="true" />
        </div>
        <div className="rounded-md bg-white p-4 ring-1 ring-evergreen/20 md:min-h-72">
          <p className="flex items-center gap-2 text-xs font-semibold uppercase text-evergreen">
            <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
            In your voice
          </p>
          <p className="mt-3 text-sm leading-6 text-ink">{scenario.rewrite}</p>
        </div>
      </div>
      <div className="mt-5 rounded-md bg-cream p-4 ring-1 ring-line">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <p className="font-semibold">Naturalness Check</p>
          <p className="text-sm font-medium text-evergreen">{change} pts</p>
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
    </div>
  );
}
