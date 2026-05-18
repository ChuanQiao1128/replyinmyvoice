"use client";

import {
  CheckCircle2,
  Clipboard,
  CopyCheck,
  Loader2,
  RefreshCw,
  Send,
  Sparkles,
  Trash2,
} from "lucide-react";
import { FormEvent, useEffect, useMemo, useState } from "react";

import {
  scenarioOptions,
  tonePresetOptions,
  tonePresetToTone,
  type ScenarioOption,
  type TonePreset,
} from "../../lib/rewrite-presets";
import { Button } from "../ui/button";
import { Card } from "../ui/card";
import { Textarea } from "../ui/textarea";
import { SubscriptionStatus } from "./subscription-status";

const HISTORY_KEY = "rimv.rewrite.history.v2";

const limits = {
  messageToReplyTo: 5000,
  roughDraftReply: 5000,
};

type Naturalness = {
  draftAiLikePercent: number | null;
  rewriteAiLikePercent: number | null;
  changePoints: number | null;
  label: "lower" | "low_signal" | "still_high" | "unavailable";
};

type RewriteResponse = {
  rewrittenText: string;
  changeSummary: string[];
  riskNotes: string[];
  naturalness: Naturalness;
  optimization: {
    internalStrategiesTried: number;
    userUsageCharged: 1;
    diagnosisTags?: string[];
    rewritePlanSummary?: string;
  };
};

type HistoryItem = {
  scenario: ScenarioOption;
  roughDraftReply: string;
  rewrittenText: string;
  tone: "warm" | "direct";
  tonePreset: TonePreset;
  changeSummary: string[];
  riskNotes: string[];
  naturalness: Naturalness;
  createdAt: string;
};

type FormState = {
  scenario: ScenarioOption;
  messageToReplyTo: string;
  roughDraftReply: string;
  tone: "warm" | "direct";
  tonePreset: TonePreset;
};

type StringFormField = "messageToReplyTo" | "roughDraftReply";

type Props = {
  usageLabel: string;
  subscriptionStatus: string;
  paid: boolean;
};

const initialForm: FormState = {
  scenario: "Blank / custom",
  messageToReplyTo: "",
  roughDraftReply: "",
  tone: "warm",
  tonePreset: "Warm",
};

const scenarioHelp: Record<ScenarioOption, string> = {
  "Blank / custom": "Use this when you only have a draft or the text is not a reply.",
  "Email or message reply": "For everyday replies where the thread matters.",
  "Customer support": "For billing, account, product, or service replies.",
  "Cover letter": "For job applications that need to sound specific and real.",
  "Work update": "For internal status notes, blockers, and next-step messages.",
};

const progressSteps = [
  "Reading the draft",
  "Diagnosing the writing pattern",
  "Rewriting and checking the signal",
];

function Remaining({ value, max }: { value: string; max: number }) {
  return (
    <span className="text-xs text-ink/45">
      {Math.max(max - value.length, 0)} left
    </span>
  );
}

function SignalBar({
  label,
  value,
  variant = "clay",
}: {
  label: string;
  value: number | null;
  variant?: "clay" | "sage";
}) {
  const width = value ?? 0;
  const fillClass = variant === "sage" ? "bg-sage" : "bg-clay";

  return (
    <div>
      <div className="mb-1 flex justify-between text-xs font-medium text-ink/60">
        <span>{label}</span>
        <span>{value === null ? "Unavailable" : `${value}%`}</span>
      </div>
      <div className="h-2 rounded-full bg-paper-deep">
        <div
          className={`h-2 rounded-full ${fillClass}`}
          style={{ width: `${width}%` }}
        />
      </div>
    </div>
  );
}

function labelForNaturalness(naturalness?: Naturalness) {
  if (!naturalness || naturalness.label === "unavailable") {
    return "Signal unavailable";
  }
  if (naturalness.label === "lower") {
    return "Lower AI-like signal";
  }
  if (naturalness.label === "low_signal") {
    return "Low AI-like signal";
  }
  return "High AI-like signal";
}

export function RewriteWorkspace({
  usageLabel,
  subscriptionStatus,
  paid,
}: Props) {
  const [form, setForm] = useState(initialForm);
  const [result, setResult] = useState<RewriteResponse | null>(null);
  const [history, setHistory] = useState<HistoryItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [copied, setCopied] = useState(false);
  const [loadingStepIndex, setLoadingStepIndex] = useState(0);

  const combinedLength = useMemo(
    () => form.messageToReplyTo.length + form.roughDraftReply.length,
    [form.messageToReplyTo, form.roughDraftReply],
  );
  const canSubmit =
    !loading && form.roughDraftReply.trim().length >= 10 && combinedLength <= 10000;

  useEffect(() => {
    try {
      const saved = localStorage.getItem(HISTORY_KEY);
      setHistory(saved ? (JSON.parse(saved) as HistoryItem[]) : []);
    } catch {
      setHistory([]);
    }
  }, []);

  useEffect(() => {
    if (!loading) {
      setLoadingStepIndex(0);
      return;
    }

    const timer = window.setInterval(() => {
      setLoadingStepIndex((current) => (current + 1) % progressSteps.length);
    }, 1400);

    return () => window.clearInterval(timer);
  }, [loading]);

  useEffect(() => {
    if (!copied) {
      return;
    }

    const timer = window.setTimeout(() => setCopied(false), 1800);
    return () => window.clearTimeout(timer);
  }, [copied]);

  function saveHistory(response: RewriteResponse) {
    const nextItem: HistoryItem = {
      scenario: form.scenario,
      roughDraftReply: form.roughDraftReply,
      rewrittenText: response.rewrittenText,
      tone: form.tone,
      tonePreset: form.tonePreset,
      changeSummary: response.changeSummary,
      riskNotes: response.riskNotes,
      naturalness: response.naturalness,
      createdAt: new Date().toISOString(),
    };
    const next = [nextItem, ...history].slice(0, 5);
    setHistory(next);
    localStorage.setItem(HISTORY_KEY, JSON.stringify(next));
  }

  async function submit(event?: FormEvent) {
    event?.preventDefault();
    if (!canSubmit) {
      return;
    }

    setLoading(true);
    setError("");

    try {
      const response = await fetch("/api/rewrite", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          scenario: form.scenario,
          messageToReplyTo: form.messageToReplyTo,
          roughDraftReply: form.roughDraftReply,
          tone: tonePresetToTone(form.tonePreset),
          tonePreset: form.tonePreset,
        }),
      });
      const payload = (await response.json()) as RewriteResponse & {
        error?: string;
      };

      if (!response.ok) {
        throw new Error(payload.error ?? "Could not rewrite this draft.");
      }

      setResult(payload);
      saveHistory(payload);
    } catch (submitError) {
      setError(
        submitError instanceof Error
          ? submitError.message
          : "Could not rewrite this draft.",
      );
    } finally {
      setLoading(false);
    }
  }

  function updateField(name: StringFormField, value: string) {
    setForm((current) => ({ ...current, [name]: value }));
  }

  function updateScenario(value: ScenarioOption) {
    setForm((current) => ({ ...current, scenario: value }));
    setResult(null);
    setError("");
  }

  function updateTonePreset(value: TonePreset) {
    setForm((current) => ({
      ...current,
      tonePreset: value,
      tone: tonePresetToTone(value),
    }));
  }

  async function copyReply() {
    if (!result?.rewrittenText) {
      return;
    }

    await navigator.clipboard.writeText(result.rewrittenText);
    setCopied(true);
  }

  function clearHistory() {
    setHistory([]);
    localStorage.removeItem(HISTORY_KEY);
  }

  return (
    <main className="min-h-screen bg-paper text-ink">
      <div className="mx-auto max-w-5xl px-4 py-6 md:px-6 md:py-8">
        <div className="mb-5">
          <h1 className="text-3xl font-semibold">Rewrite workspace</h1>
          <p className="mt-2 text-sm text-ink/60">
            Paste a draft, choose the writing job, and keep the facts intact.
            Context is optional.
          </p>
        </div>
        <SubscriptionStatus
          paid={paid}
          status={subscriptionStatus}
          usageLabel={usageLabel}
        />
        <form className="mt-5 space-y-5" onSubmit={submit}>
          <Card className="p-4 md:p-5">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h2 className="text-lg font-semibold">Scenario</h2>
                <p className="mt-1 text-sm text-ink/55">
                  Pick the closest writing job. The rewrite rules change behind
                  the scenes.
                </p>
              </div>
              <span className="rounded-md bg-paper-deep px-3 py-1 text-xs font-semibold text-ink/55">
                {combinedLength}/10000
              </span>
            </div>
            <div className="mt-4 grid gap-3 md:grid-cols-5">
              {scenarioOptions.map((scenario) => (
                <button
                  aria-pressed={form.scenario === scenario}
                  className={`rounded-lg border p-3 text-left transition ${
                    form.scenario === scenario
                      ? "border-ink bg-ink text-paper"
                      : "border-line bg-white text-ink hover:bg-paper"
                  }`}
                  key={scenario}
                  onClick={() => updateScenario(scenario)}
                  type="button"
                >
                  <span className="block text-sm font-semibold">{scenario}</span>
                  <span className="mt-2 block text-xs leading-5 opacity-70">
                    {scenarioHelp[scenario]}
                  </span>
                </button>
              ))}
            </div>
          </Card>

          <Card className="p-4 md:p-5">
            <div className="mb-3 flex items-center justify-between gap-3">
              <label
                className="text-base font-semibold"
                htmlFor="messageToReplyTo"
              >
                Context or message
              </label>
              <div className="flex items-center gap-2">
                <span className="rounded-md bg-paper-deep px-2 py-1 text-xs font-semibold text-ink/45">
                  Optional
                </span>
                <Remaining
                  max={limits.messageToReplyTo}
                  value={form.messageToReplyTo}
                />
              </div>
            </div>
            <Textarea
              id="messageToReplyTo"
              maxLength={limits.messageToReplyTo}
              onChange={(event) =>
                updateField("messageToReplyTo", event.target.value)
              }
              placeholder="Optional. Paste the email, note, job post, or situation you are responding to."
              rows={6}
              value={form.messageToReplyTo}
            />
          </Card>

          <Card className="p-4 md:p-5">
            <div className="mb-3 flex items-center justify-between">
              <label className="text-base font-semibold" htmlFor="roughDraftReply">
                Draft to rewrite
              </label>
              <Remaining max={limits.roughDraftReply} value={form.roughDraftReply} />
            </div>
            <Textarea
              id="roughDraftReply"
              maxLength={limits.roughDraftReply}
              minLength={10}
              onChange={(event) =>
                updateField("roughDraftReply", event.target.value)
              }
              placeholder="Required. Paste the draft that sounds too stiff, generic, or over-polished."
              required
              rows={9}
              value={form.roughDraftReply}
            />
          </Card>

          <Card className="p-4 md:p-5">
            <div className="flex flex-wrap items-center justify-between gap-4">
              <div>
                <h2 className="text-base font-semibold">Tone</h2>
                <p className="mt-1 text-sm text-ink/55">
                  Keep this simple. The scenario handles the detailed rules.
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                {tonePresetOptions.map((tonePreset) => (
                  <button
                    className={`rounded-md border px-4 py-2 text-sm font-semibold transition ${
                      form.tonePreset === tonePreset
                        ? "border-ink bg-ink text-paper"
                        : "border-line bg-white text-ink/65 hover:text-ink"
                    }`}
                    key={tonePreset}
                    onClick={() => updateTonePreset(tonePreset)}
                    type="button"
                  >
                    {tonePreset}
                  </button>
                ))}
              </div>
              <Button disabled={!canSubmit} type="submit">
                {loading ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <Send className="h-4 w-4" aria-hidden="true" />
                )}
                Begin rewrite
              </Button>
            </div>
            {loading ? (
              <div
                aria-live="polite"
                className="mt-4 rounded-lg border border-line bg-white px-3 py-3"
              >
                <div className="grid gap-2 md:grid-cols-3">
                  {progressSteps.map((step, index) => (
                    <div
                      className={`flex items-center gap-2 text-xs font-semibold ${
                        index === loadingStepIndex ? "text-ink" : "text-ink/40"
                      }`}
                      key={step}
                    >
                      {index < loadingStepIndex ? (
                        <CheckCircle2
                          className="h-4 w-4 text-sage"
                          aria-hidden="true"
                        />
                      ) : index === loadingStepIndex ? (
                        <Loader2
                          className="h-4 w-4 animate-spin text-clay"
                          aria-hidden="true"
                        />
                      ) : (
                        <span className="h-4 w-4 rounded-full border border-line" />
                      )}
                      {step}
                    </div>
                  ))}
                </div>
              </div>
            ) : null}
          </Card>

          {error ? (
            <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {error}
            </p>
          ) : null}
        </form>

        <section className="mt-5 space-y-5">
          <Card className="p-4 md:p-5">
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <h2 className="text-xl font-semibold">Rewritten text</h2>
              <div className="flex gap-2">
                <Button
                  disabled={!result?.rewrittenText}
                  onClick={() => void copyReply()}
                  type="button"
                  variant="secondary"
                >
                  {copied ? (
                    <CopyCheck className="h-4 w-4" aria-hidden="true" />
                  ) : (
                    <Clipboard className="h-4 w-4" aria-hidden="true" />
                  )}
                  {copied ? "Copied" : "Copy"}
                </Button>
                <Button
                  disabled={!canSubmit}
                  onClick={() => void submit()}
                  type="button"
                  variant="secondary"
                >
                  <RefreshCw className="h-4 w-4" aria-hidden="true" />
                  Retry
                </Button>
              </div>
            </div>
            <div className="min-h-60 whitespace-pre-wrap rounded-lg border border-line bg-white p-4 text-sm leading-7 text-ink md:text-base md:leading-8">
              {loading ? (
                <div className="flex h-48 flex-col items-center justify-center text-center text-ink/55">
                  <Sparkles className="mb-3 h-5 w-5 text-clay" aria-hidden="true" />
                  <p className="font-semibold text-ink">
                    {progressSteps[loadingStepIndex]}
                  </p>
                  <p className="mt-1 text-xs">
                    The rewrite is preserving your facts and checking the signal.
                  </p>
                </div>
              ) : (
                result?.rewrittenText ??
                "Your rewritten text will appear here after you run the workspace."
              )}
            </div>
          </Card>

          <Card className="p-4 md:p-5">
            <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
              <h2 className="text-xl font-semibold">Naturalness Check</h2>
              <span className="rounded-md bg-paper-deep px-3 py-1 text-xs font-semibold text-sage">
                {labelForNaturalness(result?.naturalness)}
              </span>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <SignalBar
                label="Draft AI-like signal"
                value={result?.naturalness.draftAiLikePercent ?? null}
              />
              <SignalBar
                label="Rewrite AI-like signal"
                value={result?.naturalness.rewriteAiLikePercent ?? null}
                variant="sage"
              />
            </div>
            <p className="mt-4 text-sm text-ink/60">
              Change:{" "}
              {result?.naturalness.changePoints === null ||
              result?.naturalness.changePoints === undefined
                ? "Unavailable"
                : `${result.naturalness.changePoints} pts`}
            </p>
            <p className="mt-2 text-xs leading-5 text-ink/50">
              A third-party reference signal that helps compare how natural the
              draft and rewrite feel. It is not a guarantee; review before sending.
            </p>
          </Card>

          <div className="grid gap-5 md:grid-cols-2">
            <Card className="p-4 md:p-5">
              <h2 className="font-semibold">Change summary</h2>
              <ul className="mt-3 space-y-2 text-sm text-ink/65">
                {(result?.changeSummary ?? ["No rewrite yet."]).map((item) => (
                  <li key={item}>- {item}</li>
                ))}
              </ul>
            </Card>
            <Card className="p-4 md:p-5">
              <h2 className="font-semibold">Risk notes</h2>
              <ul className="mt-3 space-y-2 text-sm text-ink/65">
                {(result?.riskNotes ?? ["Review facts before sending."]).map(
                  (item) => (
                    <li key={item}>- {item}</li>
                  ),
                )}
              </ul>
            </Card>
          </div>

          <details className="rounded-lg border border-line bg-white/70 p-4 shadow-soft">
            <summary className="flex cursor-pointer list-none items-center justify-between gap-3 font-semibold">
              <span>Recent rewrites</span>
              <span className="text-xs font-medium text-ink/45">
                {history.length ? `${history.length} saved locally` : "Empty"}
              </span>
            </summary>
            <div className="mt-4 flex justify-end">
              <Button onClick={clearHistory} type="button" variant="ghost">
                <Trash2 className="h-4 w-4" aria-hidden="true" />
                Clear
              </Button>
            </div>
            <div className="mt-3 space-y-3">
              {history.length ? (
                history.map((item) => (
                  <button
                    className="w-full rounded-lg border border-line bg-white p-3 text-left text-sm hover:bg-paper"
                    key={item.createdAt}
                    onClick={() =>
                      setResult({
                        rewrittenText: item.rewrittenText,
                        changeSummary: item.changeSummary,
                        riskNotes: item.riskNotes,
                        naturalness: item.naturalness,
                        optimization: {
                          internalStrategiesTried: 1,
                          userUsageCharged: 1,
                        },
                      })
                    }
                    type="button"
                  >
                    <p className="font-medium">
                      {item.scenario} - {item.tonePreset}
                    </p>
                    <p className="mt-1 line-clamp-2 text-ink/60">
                      {item.rewrittenText}
                    </p>
                  </button>
                ))
              ) : (
                <p className="text-sm text-ink/55">
                  Rewrites stay in this browser only and are not saved to the
                  database.
                </p>
              )}
            </div>
          </details>
        </section>
      </div>
    </main>
  );
}
