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
  WandSparkles,
} from "lucide-react";
import { FormEvent, useEffect, useMemo, useState } from "react";

import { Button } from "../ui/button";
import { Card } from "../ui/card";
import { Input } from "../ui/input";
import { Textarea } from "../ui/textarea";
import { SubscriptionStatus } from "./subscription-status";

const HISTORY_KEY = "rimv.rewrite.history.v1";

const limits = {
  messageToReplyTo: 5000,
  roughDraftReply: 5000,
  audience: 300,
  purpose: 500,
  whatHappened: 1000,
  factsToPreserve: 1000,
};

type Naturalness = {
  draftAiLikePercent: number | null;
  rewriteAiLikePercent: number | null;
  changePoints: number | null;
  label: "lower" | "still_high" | "unavailable";
};

type RewriteResponse = {
  rewrittenText: string;
  changeSummary: string[];
  riskNotes: string[];
  naturalness: Naturalness;
  optimization: {
    internalStrategiesTried: number;
    userUsageCharged: 1;
  };
};

type HistoryItem = {
  roughDraftReply: string;
  rewrittenText: string;
  tone: "warm" | "direct";
  changeSummary: string[];
  riskNotes: string[];
  naturalness: Naturalness;
  createdAt: string;
};

type FormState = {
  messageToReplyTo: string;
  roughDraftReply: string;
  audience: string;
  purpose: string;
  whatHappened: string;
  factsToPreserve: string;
  tone: "warm" | "direct";
};

type Props = {
  usageLabel: string;
  subscriptionStatus: string;
  paid: boolean;
};

const initialForm: FormState = {
  messageToReplyTo:
    "Hi, I missed the deadline because I had a family issue this week. Is there any way I can still submit the reflection?",
  roughDraftReply:
    "Dear student, I acknowledge your message. Late submissions are generally not accepted according to course policy. Please provide additional details for consideration.",
  audience: "A student who missed a reflection deadline",
  purpose: "Reply clearly while keeping the policy in mind",
  whatHappened: "They had a family issue and asked whether they can still submit.",
  factsToPreserve: "Late work policy matters. I can review the situation tomorrow.",
  tone: "warm" as const,
};

const templates: Array<{
  label: string;
  description: string;
  form: FormState;
}> = [
  {
    label: "Teacher reply",
    description: "A student missed a deadline and needs a firm but kind answer.",
    form: initialForm,
  },
  {
    label: "Sales follow-up",
    description: "A buyer needs a specific, relationship-aware nudge.",
    form: {
      messageToReplyTo:
        "Thanks for the proposal. We are still comparing options and may come back next month.",
      roughDraftReply:
        "Hello, I am following up on the proposal and would like to know if you have any updates about whether you will proceed.",
      audience: "A prospect who reviewed a proposal last week",
      purpose: "Follow up without sounding pushy",
      whatHappened:
        "They are comparing options and said they may revisit the decision next month.",
      factsToPreserve:
        "Proposal was sent last week. They may revisit next month. Offer to simplify the options.",
      tone: "warm",
    },
  },
  {
    label: "Workplace update",
    description: "A teammate needs a clear status reply with timing.",
    form: {
      messageToReplyTo:
        "Can you send the revised numbers today? I need them for the partner update.",
      roughDraftReply:
        "Unfortunately, the requested numbers are not available at this time due to delayed source information.",
      audience: "A teammate preparing a partner update",
      purpose: "Explain a short delay and give a reliable next step",
      whatHappened: "The source file arrived late and still needs one check.",
      factsToPreserve: "Send the revised numbers by 4pm Friday.",
      tone: "direct",
    },
  },
  {
    label: "Client support",
    description: "A client reported a problem and needs a careful reply.",
    form: {
      messageToReplyTo:
        "The report totals look different from last month. Can someone explain what changed?",
      roughDraftReply:
        "Thank you for contacting us. Our team is currently reviewing the matter and will provide an update as soon as possible.",
      audience: "A client asking about a reporting change",
      purpose: "Acknowledge the issue and explain what will happen next",
      whatHappened: "The new report includes a category that was hidden last month.",
      factsToPreserve:
        "Report includes a newly visible category. Send a line-by-line note today.",
      tone: "warm",
    },
  },
];

const progressSteps = [
  "Reading the thread",
  "Rewriting in your voice",
  "Checking the writing signal",
];

function Remaining({
  value,
  max,
}: {
  value: string;
  max: number;
}) {
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
  const [activeTemplate, setActiveTemplate] = useState(templates[0].label);
  const [loadingStepIndex, setLoadingStepIndex] = useState(0);

  const combinedLength = useMemo(
    () =>
      form.messageToReplyTo.length +
      form.roughDraftReply.length +
      form.audience.length +
      form.purpose.length +
      form.whatHappened.length +
      form.factsToPreserve.length,
    [form],
  );

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
      roughDraftReply: form.roughDraftReply,
      rewrittenText: response.rewrittenText,
      tone: form.tone,
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
    setLoading(true);
    setError("");

    try {
      const response = await fetch("/api/rewrite", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(form),
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

  function updateField(name: keyof typeof form, value: string) {
    setForm((current) => ({ ...current, [name]: value }));
  }

  function applyTemplate(template: (typeof templates)[number]) {
    setForm({ ...template.form });
    setActiveTemplate(template.label);
    setResult(null);
    setError("");
    setCopied(false);
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
      <div className="mx-auto max-w-7xl px-4 py-6 md:px-6 md:py-8">
        <div className="mb-5">
          <h1 className="text-3xl font-semibold">Rewrite workspace</h1>
          <p className="mt-2 text-sm text-ink/60">
            Avoid pasting passwords, payment details, or highly sensitive
            personal information.
          </p>
        </div>
        <SubscriptionStatus
          paid={paid}
          status={subscriptionStatus}
          usageLabel={usageLabel}
        />
        <form className="mt-5 grid gap-5 lg:grid-cols-[1fr_0.9fr]" onSubmit={submit}>
          <section className="space-y-4">
            <Card className="p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="flex items-center gap-2 text-sm font-semibold">
                    <WandSparkles className="h-4 w-4 text-clay" aria-hidden="true" />
                    Starter scenarios
                  </p>
                  <p className="mt-1 text-xs leading-5 text-ink/50">
                    Use a realistic email workflow as a starting point, then
                    replace the details with your own.
                  </p>
                </div>
                <span className="rounded-md bg-paper-deep px-2 py-1 text-xs font-semibold text-ink/55">
                  {activeTemplate}
                </span>
              </div>
              <div className="mt-4 grid gap-2 md:grid-cols-2">
                {templates.map((template) => (
                  <button
                    className={`rounded-lg border p-3 text-left transition ${
                      activeTemplate === template.label
                        ? "border-ink bg-ink text-paper"
                        : "border-line bg-white text-ink hover:bg-paper"
                    }`}
                    key={template.label}
                    onClick={() => applyTemplate(template)}
                    type="button"
                  >
                    <span className="text-sm font-semibold">{template.label}</span>
                    <span className="mt-1 block text-xs leading-5 opacity-75">
                      {template.description}
                    </span>
                  </button>
                ))}
              </div>
            </Card>
            <Card className="p-4">
              <div className="mb-2 flex items-center justify-between">
                <label className="text-sm font-semibold" htmlFor="messageToReplyTo">
                  Message to reply to
                </label>
                <Remaining
                  max={limits.messageToReplyTo}
                  value={form.messageToReplyTo}
                />
              </div>
              <Textarea
                id="messageToReplyTo"
                maxLength={limits.messageToReplyTo}
                onChange={(event) =>
                  updateField("messageToReplyTo", event.target.value)
                }
                rows={6}
                value={form.messageToReplyTo}
              />
            </Card>
            <Card className="p-4">
              <div className="mb-2 flex items-center justify-between">
                <label className="text-sm font-semibold" htmlFor="roughDraftReply">
                  Rough draft reply
                </label>
                <Remaining
                  max={limits.roughDraftReply}
                  value={form.roughDraftReply}
                />
              </div>
              <Textarea
                id="roughDraftReply"
                maxLength={limits.roughDraftReply}
                minLength={10}
                onChange={(event) =>
                  updateField("roughDraftReply", event.target.value)
                }
                required
                rows={7}
                value={form.roughDraftReply}
              />
            </Card>
            <div className="grid gap-4 md:grid-cols-2">
              <Card className="p-4">
                <div className="mb-2 flex items-center justify-between">
                  <label className="text-sm font-semibold" htmlFor="audience">
                    Audience
                  </label>
                  <Remaining max={limits.audience} value={form.audience} />
                </div>
                <Input
                  id="audience"
                  maxLength={limits.audience}
                  onChange={(event) => updateField("audience", event.target.value)}
                  value={form.audience}
                />
              </Card>
              <Card className="p-4">
                <div className="mb-2 flex items-center justify-between">
                  <label className="text-sm font-semibold" htmlFor="purpose">
                    Purpose
                  </label>
                  <Remaining max={limits.purpose} value={form.purpose} />
                </div>
                <Input
                  id="purpose"
                  maxLength={limits.purpose}
                  onChange={(event) => updateField("purpose", event.target.value)}
                  value={form.purpose}
                />
              </Card>
            </div>
            <Card className="p-4">
              <div className="mb-2 flex items-center justify-between">
                <label className="text-sm font-semibold" htmlFor="whatHappened">
                  What actually happened
                </label>
                <Remaining
                  max={limits.whatHappened}
                  value={form.whatHappened}
                />
              </div>
              <Textarea
                id="whatHappened"
                maxLength={limits.whatHappened}
                onChange={(event) =>
                  updateField("whatHappened", event.target.value)
                }
                rows={3}
                value={form.whatHappened}
              />
            </Card>
            <Card className="p-4">
              <div className="mb-2 flex items-center justify-between">
                <label className="text-sm font-semibold" htmlFor="factsToPreserve">
                  Facts to preserve
                </label>
                <Remaining
                  max={limits.factsToPreserve}
                  value={form.factsToPreserve}
                />
              </div>
              <Textarea
                id="factsToPreserve"
                maxLength={limits.factsToPreserve}
                onChange={(event) =>
                  updateField("factsToPreserve", event.target.value)
                }
                rows={3}
                value={form.factsToPreserve}
              />
            </Card>
            <Card className="flex flex-wrap items-center justify-between gap-3 p-4">
              <div>
                <p className="text-sm font-semibold">Tone</p>
                <p className="mt-1 text-xs text-ink/50">
                  Combined request: {combinedLength}/10000
                </p>
              </div>
              <div className="flex rounded-md border border-line bg-white p-1">
                {(["warm", "direct"] as const).map((tone) => (
                  <button
                    className={`rounded px-4 py-2 text-sm font-semibold capitalize ${
                      form.tone === tone ? "bg-ink text-paper" : "text-ink/65"
                    }`}
                    key={tone}
                    onClick={() => setForm((current) => ({ ...current, tone }))}
                    type="button"
                  >
                    {tone}
                  </button>
                ))}
              </div>
              <Button disabled={loading || combinedLength > 10000} type="submit">
                {loading ? (
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                ) : (
                  <Send className="h-4 w-4" aria-hidden="true" />
                )}
                Rewrite
              </Button>
              {loading ? (
                <div
                  aria-live="polite"
                  className="w-full rounded-lg border border-line bg-white px-3 py-3"
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
                          <CheckCircle2 className="h-4 w-4 text-sage" aria-hidden="true" />
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
          </section>
          <section className="space-y-4">
            <Card className="p-4">
              <div className="mb-3 flex items-center justify-between">
                <h2 className="font-semibold">Rewritten reply</h2>
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
                    disabled={loading}
                    onClick={() => void submit()}
                    type="button"
                    variant="secondary"
                  >
                    <RefreshCw className="h-4 w-4" aria-hidden="true" />
                    Try again
                  </Button>
                </div>
              </div>
              <div className="min-h-52 rounded-lg border border-line bg-white p-4 text-sm leading-7 text-ink">
                {loading ? (
                  <div className="flex h-44 flex-col items-center justify-center text-center text-ink/55">
                    <Sparkles className="mb-3 h-5 w-5 text-clay" aria-hidden="true" />
                    <p className="font-semibold text-ink">
                      {progressSteps[loadingStepIndex]}
                    </p>
                    <p className="mt-1 text-xs">
                      The workspace is preserving the facts you provided.
                    </p>
                  </div>
                ) : (
                  result?.rewrittenText ??
                  "Your rewritten reply will appear here after you run the workspace."
                )}
              </div>
            </Card>
            <Card className="p-4">
              <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                <h2 className="font-semibold">Naturalness Check</h2>
                <span className="rounded-md bg-paper-deep px-2 py-1 text-xs font-semibold text-sage">
                  {labelForNaturalness(result?.naturalness)}
                </span>
              </div>
              <div className="space-y-3">
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
              <p className="mt-3 text-sm text-ink/60">
                Change:{" "}
                {result?.naturalness.changePoints === null ||
                result?.naturalness.changePoints === undefined
                  ? "Unavailable"
                  : `${result.naturalness.changePoints} pts`}
              </p>
              <p className="mt-2 text-xs leading-5 text-ink/50">
                A third-party reference signal that helps compare how natural the
                draft and rewrite feel. It is not a guarantee; review the reply
                before sending.
              </p>
            </Card>
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-1 xl:grid-cols-2">
              <Card className="p-4">
                <h2 className="font-semibold">Change summary</h2>
                <ul className="mt-3 space-y-2 text-sm text-ink/65">
                  {(result?.changeSummary ?? ["No rewrite yet."]).map((item) => (
                    <li key={item}>- {item}</li>
                  ))}
                </ul>
              </Card>
              <Card className="p-4">
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
            <Card className="p-4">
              <div className="mb-3 flex items-center justify-between">
                <h2 className="font-semibold">History</h2>
                <Button onClick={clearHistory} type="button" variant="ghost">
                  <Trash2 className="h-4 w-4" aria-hidden="true" />
                  Clear history
                </Button>
              </div>
              <div className="space-y-3">
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
                      <p className="font-medium capitalize">{item.tone}</p>
                      <p className="mt-1 line-clamp-2 text-ink/60">
                        {item.rewrittenText}
                      </p>
                    </button>
                  ))
                ) : (
                  <p className="text-sm text-ink/55">No saved rewrites yet.</p>
                )}
              </div>
            </Card>
          </section>
        </form>
      </div>
    </main>
  );
}
