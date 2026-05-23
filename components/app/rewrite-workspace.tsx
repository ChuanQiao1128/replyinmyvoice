"use client";

import {
  CheckCircle2,
  Clipboard,
  CopyCheck,
  FilePlus2,
  Loader2,
  RefreshCw,
  Send,
  Sparkles,
  Trash2,
} from "lucide-react";
import Link from "next/link";
import { FormEvent, useEffect, useMemo, useState } from "react";

import {
  scenarioOptions,
  tonePresetOptions,
  tonePresetToTone,
  type ScenarioOption,
  type TonePreset,
} from "../../lib/rewrite-presets";
import { rewriteInputLimits } from "../../lib/rewrite-limits";
import { Button } from "../ui/button";
import { Card } from "../ui/card";
import { Textarea } from "../ui/textarea";
import { SubscriptionStatus } from "./subscription-status";

const HISTORY_KEY = "rimv.rewrite.history.v1";

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
    selectionStatus?: "passed" | "best_available";
    diagnosisTags?: string[];
    rewritePlanSummary?: string;
    candidateSignals?: Array<{
      stage: "initial" | "targeted_repair" | "repair" | "fallback";
      aiLikePercent: number | null;
      status: string;
      rejected: boolean;
      reason: string;
    }>;
  };
};

type QualityFailure = {
  error: string;
  naturalness?: Naturalness;
  reason?: string;
  charged?: false;
};

type HistoryItem = {
  mode: ScenarioOption;
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
  messageToReplyTo: string;
  roughDraftReply: string;
  audience: string;
  purpose: string;
  whatHappened: string;
  factsToPreserve: string;
  tone: "warm" | "direct";
  tonePreset: TonePreset;
};

type StringFormField =
  | "messageToReplyTo"
  | "roughDraftReply"
  | "audience"
  | "purpose"
  | "whatHappened"
  | "factsToPreserve";

type Props = {
  usageLabel: string;
  subscriptionStatus: string;
  paid: boolean;
  remaining: number;
  quota: number;
};

const initialForm: FormState = {
  messageToReplyTo: "",
  roughDraftReply: "",
  audience: "",
  purpose: "",
  whatHappened: "",
  factsToPreserve: "",
  tone: "warm",
  tonePreset: "Warm",
};

const progressSteps = [
  "Extracting the facts",
  "Building candidate replies",
  "Reviewing quality gates",
];

type WorkspaceScenario = {
  id: string;
  label: string;
  description: string;
  rewriteScenario: ScenarioOption;
  audience: string;
  purpose: string;
};

const workspaceScenarioOptions = [
  {
    id: "extension-request",
    label: "Extension request",
    description: "Ask for more time without overexplaining.",
    rewriteScenario: scenarioOptions[1],
    audience: "Lecturer, tutor, manager, or reviewer",
    purpose: "Ask for more time while keeping the request specific and respectful.",
  },
  {
    id: "lecturer-email",
    label: "Lecturer email",
    description: "Write a clear academic message you still own.",
    rewriteScenario: scenarioOptions[1],
    audience: "Lecturer, tutor, advisor, or course coordinator",
    purpose: "Send a clear academic reply while preserving the exact facts.",
  },
  {
    id: "internship-follow-up",
    label: "Internship follow-up",
    description: "Follow up after an application, interview, or intro.",
    rewriteScenario: scenarioOptions[1],
    audience: "Recruiter, hiring manager, or professional contact",
    purpose: "Follow up politely without sounding stiff or pushy.",
  },
  {
    id: "group-project",
    label: "Group project",
    description: "Align teammates on work, deadlines, or blockers.",
    rewriteScenario: scenarioOptions[5],
    audience: "Classmate, teammate, or project group",
    purpose: "Clarify the project update, next step, or blocker.",
  },
  {
    id: "client-delay",
    label: "Client delay",
    description: "Explain a delay and protect trust.",
    rewriteScenario: scenarioOptions[2],
    audience: "Client, customer, or account contact",
    purpose: "Explain a delay clearly while keeping commitments accurate.",
  },
  {
    id: "less-rude",
    label: "Make this less rude",
    description: "Keep the point, reduce the friction.",
    rewriteScenario: scenarioOptions[0],
    audience: "Person receiving this reply",
    purpose: "Make the reply clearer and calmer without changing the ask.",
  },
  {
    id: "something-else",
    label: "Something else",
    description: "Use the same workspace for another real reply.",
    rewriteScenario: scenarioOptions[0],
    audience: "",
    purpose: "",
  },
] satisfies WorkspaceScenario[];

type WorkspaceScenarioId = (typeof workspaceScenarioOptions)[number]["id"];

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
    return "Writing signal improved";
  }
  if (naturalness.label === "low_signal") {
    return "Writing signal already low";
  }
  return "Writing signal still high";
}

function titleForQualityFailure(reason?: string) {
  if (reason === "signal_unavailable") {
    return "Signal unavailable";
  }
  if (reason === "fact_check_failed") {
    return "Facts need another pass";
  }
  return "Quality bar not met";
}

function splitFactsToPreserve(value: string) {
  return value
    .split(/\n|;|•|·/)
    .map((item) => item.trim().replace(/^[-*]\s*/, ""))
    .filter(Boolean);
}

function fallbackWhyThisWorks(scenario: WorkspaceScenario | null) {
  if (scenario?.id === "extension-request") {
    return [
      "Keeps the request specific without overexplaining.",
      "Leaves room for your lecturer or reviewer to respond.",
    ];
  }

  if (scenario?.id === "client-delay") {
    return [
      "Keeps the delay explanation clear without adding new commitments.",
      "Makes the next step easier for the other person to see.",
    ];
  }

  if (scenario?.id === "less-rude") {
    return [
      "Keeps your main point visible.",
      "Reduces friction without changing the ask.",
    ];
  }

  if (scenario?.id === "internship-follow-up") {
    return [
      "Keeps the follow-up polite and specific.",
      "Avoids making the message sound more certain than your facts allow.",
    ];
  }

  return [
    "Keeps the rewrite grounded in the message and draft you supplied.",
    "Uses the selected tone without adding new promises.",
  ];
}

export function RewriteWorkspace({
  usageLabel,
  subscriptionStatus,
  paid,
  remaining,
  quota,
}: Props) {
  const [form, setForm] = useState(initialForm);
  const [result, setResult] = useState<RewriteResponse | null>(null);
  const [qualityFailure, setQualityFailure] = useState<QualityFailure | null>(
    null,
  );
  const [history, setHistory] = useState<HistoryItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [copied, setCopied] = useState(false);
  const [loadingStepIndex, setLoadingStepIndex] = useState(0);
  const [selectedScenarioId, setSelectedScenarioId] =
    useState<WorkspaceScenarioId | null>(null);
  const [factsExpanded, setFactsExpanded] = useState(false);
  const [freeRewritesRemaining, setFreeRewritesRemaining] = useState(() =>
    Math.max(Math.min(remaining, quota), 0),
  );
  const [showFreeRewriteNudge, setShowFreeRewriteNudge] = useState(false);

  const combinedLength = useMemo(
    () =>
      form.messageToReplyTo.length +
      form.roughDraftReply.length +
      form.audience.length +
      form.purpose.length +
      form.whatHappened.length +
      form.factsToPreserve.length,
    [
      form.audience,
      form.factsToPreserve,
      form.messageToReplyTo,
      form.purpose,
      form.roughDraftReply,
      form.whatHappened,
    ],
  );
  const selectedScenario = useMemo(
    () =>
      workspaceScenarioOptions.find(
        (option) => option.id === selectedScenarioId,
      ) ?? null,
    [selectedScenarioId],
  );
  const canSubmit =
    !loading &&
    Boolean(selectedScenario) &&
    form.roughDraftReply.trim().length >= 10 &&
    combinedLength <= rewriteInputLimits.combined;

  useEffect(() => {
    try {
      const saved = localStorage.getItem(HISTORY_KEY);
      setHistory(saved ? (JSON.parse(saved) as HistoryItem[]) : []);
    } catch {
      setHistory([]);
    }
  }, []);

  useEffect(() => {
    setFreeRewritesRemaining(Math.max(Math.min(remaining, quota), 0));
  }, [quota, remaining]);

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
      mode: selectedScenario?.rewriteScenario ?? scenarioOptions[0],
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
    setQualityFailure(null);

    try {
      const response = await fetch("/api/rewrite", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          scenario: selectedScenario?.rewriteScenario ?? scenarioOptions[0],
          messageToReplyTo: form.messageToReplyTo,
          roughDraftReply: form.roughDraftReply,
          audience: form.audience,
          purpose: form.purpose,
          whatHappened: form.whatHappened,
          factsToPreserve: form.factsToPreserve,
          tone: tonePresetToTone(form.tonePreset),
          tonePreset: form.tonePreset,
        }),
      });
      const payload = (await response.json()) as RewriteResponse & {
        code?: string;
        error?: string;
        naturalness?: Naturalness;
        reason?: string;
        charged?: false;
      };

      if (!response.ok) {
        if (response.status === 422 && payload.code === "quality_gate_failed") {
          setResult(null);
          setQualityFailure({
            error:
              payload.error ??
              "We could not produce a better version yet. Try again or adjust the draft.",
            naturalness: payload.naturalness,
            reason: payload.reason,
            charged: payload.charged,
          });
          setError("");
          return;
        }
        throw new Error(payload.error ?? "Could not rewrite this draft.");
      }

      setQualityFailure(null);
      setResult(payload);
      if (!paid) {
        setFreeRewritesRemaining((current) => Math.max(current - 1, 0));
        setShowFreeRewriteNudge(true);
      }
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

  function updateTonePreset(value: TonePreset) {
    setForm((current) => ({
      ...current,
      tonePreset: value,
      tone: tonePresetToTone(value),
    }));
  }

  function selectScenario(option: WorkspaceScenario) {
    setSelectedScenarioId(option.id as WorkspaceScenarioId);
    setFactsExpanded(false);
    setError("");
    setForm((current) => ({
      ...current,
      audience: option.audience,
      purpose: option.purpose,
      whatHappened: "",
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

  function resetWorkspace() {
    setForm(initialForm);
    setSelectedScenarioId(null);
    setFactsExpanded(false);
    setResult(null);
    setQualityFailure(null);
    setError("");
    setCopied(false);
    setShowFreeRewriteNudge(false);
  }

  const visibleNaturalness = result?.naturalness ?? qualityFailure?.naturalness;
  const showFreeNudge = !paid && showFreeRewriteNudge && result !== null;
  const suppliedFacts = splitFactsToPreserve(form.factsToPreserve);
  const whyThisWorks = result?.changeSummary.length
    ? result.changeSummary.slice(0, 4)
    : fallbackWhyThisWorks(selectedScenario);
  const beforeSendChecks = [
    "check the deadline/date is correct",
    "make sure the reason is true",
    "edit anything that feels too formal",
    ...(result?.riskNotes ?? []),
  ];

  return (
    <main className="min-h-screen bg-paper text-ink">
      <div className="mx-auto max-w-6xl px-4 py-5 md:px-6 md:py-7">
        <div className="mb-5 rounded-lg border border-line bg-white/80 p-4 shadow-crisp md:p-5">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="max-w-2xl">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-clay">
                Workspace
              </p>
              <h1 className="mt-2 text-2xl font-semibold md:text-3xl">
                Rewrite workspace
              </h1>
              <p className="mt-2 text-sm leading-6 text-ink/60">
                Paste the message if you have it, then paste the draft. The
                rewrite keeps the facts intact.
              </p>
            </div>
            <Button onClick={resetWorkspace} type="button" variant="secondary">
              <FilePlus2 className="h-4 w-4" aria-hidden="true" />
              New draft
            </Button>
          </div>
        </div>

        <SubscriptionStatus
          paid={paid}
          status={subscriptionStatus}
          usageLabel={usageLabel}
        />
        <div className="mt-5 grid gap-5 lg:grid-cols-[minmax(0,1.04fr)_minmax(360px,0.96fr)] lg:items-start">
          <form className="space-y-5" onSubmit={submit}>
            <Card className="p-4 md:p-5">
              <div className="mb-4">
                <p className="text-xs font-semibold uppercase tracking-[0.14em] text-clay">
                  Step 1
                </p>
                <h2 className="mt-1 text-base font-semibold">
                  Pick your reply situation
                </h2>
                {selectedScenario ? (
                  <p className="mt-2 text-sm leading-6 text-ink/60">
                    {selectedScenario.description}
                  </p>
                ) : (
                  <p className="mt-2 text-sm leading-6 text-ink/60">
                    {
                      "Pick a real message you need to send. We'll help you make it clearer while keeping your facts unchanged."
                    }
                  </p>
                )}
              </div>
              <div className="grid gap-2 sm:grid-cols-2">
                {workspaceScenarioOptions.map((option) => {
                  const active = selectedScenarioId === option.id;
                  return (
                    <button
                      aria-pressed={active}
                      className={`min-h-[5.25rem] rounded-lg border p-3 text-left transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 focus-visible:ring-offset-2 focus-visible:ring-offset-paper ${
                        active
                          ? "border-ink bg-ink text-paper"
                          : "border-line bg-white text-ink hover:bg-paper"
                      }`}
                      key={option.id}
                      onClick={() => selectScenario(option)}
                      type="button"
                    >
                      <span className="block text-sm font-semibold">
                        {option.label}
                      </span>
                      <span
                        className={`mt-1 block text-xs leading-5 ${
                          active ? "text-paper/70" : "text-ink/55"
                        }`}
                      >
                        {option.description}
                      </span>
                    </button>
                  );
                })}
              </div>
            </Card>

            {selectedScenario ? (
              <>
                <Card className="p-4 md:p-5">
                  <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <p className="text-xs font-semibold uppercase tracking-[0.14em] text-sage">
                        Step 2
                      </p>
                      <label
                        className="mt-1 block text-base font-semibold"
                        htmlFor="messageToReplyTo"
                      >
                        What message are you replying to?
                      </label>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="rounded-md bg-paper-deep px-3 py-1 text-xs font-semibold text-ink/55">
                        {combinedLength}/{rewriteInputLimits.combined}
                      </span>
                      <Remaining
                        max={rewriteInputLimits.messageToReplyTo}
                        value={form.messageToReplyTo}
                      />
                    </div>
                  </div>
                  <Textarea
                    className="min-h-36"
                    id="messageToReplyTo"
                    maxLength={rewriteInputLimits.messageToReplyTo}
                    onChange={(event) =>
                      updateField("messageToReplyTo", event.target.value)
                    }
                    placeholder="Paste the email, note, message, or situation you need to answer."
                    rows={5}
                    value={form.messageToReplyTo}
                  />
                </Card>

                <Card className="p-4 md:p-5">
                  <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                    <label
                      className="text-base font-semibold"
                      htmlFor="roughDraftReply"
                    >
                      What do you want to say?
                    </label>
                    <Remaining
                      max={rewriteInputLimits.roughDraftReply}
                      value={form.roughDraftReply}
                    />
                  </div>
                  <Textarea
                    className="min-h-[14rem]"
                    id="roughDraftReply"
                    maxLength={rewriteInputLimits.roughDraftReply}
                    minLength={10}
                    onChange={(event) =>
                      updateField("roughDraftReply", event.target.value)
                    }
                    placeholder="Write or paste the rough reply. Short notes are fine as long as the facts are real."
                    required
                    rows={9}
                    value={form.roughDraftReply}
                  />
                </Card>

                <Card className="p-4 md:p-5">
                  <button
                    aria-controls="factsToPreservePanel"
                    aria-expanded={factsExpanded}
                    className="flex w-full items-center justify-between gap-3 text-left font-semibold focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 focus-visible:ring-offset-2 focus-visible:ring-offset-paper"
                    onClick={() => setFactsExpanded((current) => !current)}
                    type="button"
                  >
                    <span>Add facts that must stay true</span>
                    <span className="rounded-md bg-paper-deep px-2 py-1 text-xs font-semibold text-ink/45">
                      {factsExpanded ? "Hide" : "Optional"}
                    </span>
                  </button>
                  {factsExpanded ? (
                    <div className="mt-4" id="factsToPreservePanel">
                      <div className="mb-2 flex flex-wrap items-center justify-between gap-3">
                        <label
                          className="text-sm font-semibold"
                          htmlFor="factsToPreserve"
                        >
                          Facts that must stay true
                        </label>
                        <Remaining
                          max={rewriteInputLimits.factsToPreserve}
                          value={form.factsToPreserve}
                        />
                      </div>
                      <Textarea
                        className="min-h-28"
                        id="factsToPreserve"
                        maxLength={rewriteInputLimits.factsToPreserve}
                        onChange={(event) =>
                          updateField("factsToPreserve", event.target.value)
                        }
                        placeholder="Optional: dates, names, deadlines, promises the AI must not change."
                        rows={4}
                        value={form.factsToPreserve}
                      />
                    </div>
                  ) : (
                    <p className="mt-2 text-sm leading-6 text-ink/55">
                      Optional: dates, names, deadlines, or promises that must
                      not change.
                    </p>
                  )}
                </Card>

                <Card className="p-4 md:p-5">
                  <div className="grid gap-4 md:grid-cols-[minmax(0,1fr)_auto] md:items-center">
                    <div>
                      <h2 className="text-base font-semibold">Tone</h2>
                      <p className="mt-1 text-sm leading-6 text-ink/55">
                        Warm adds a little relationship tone. Direct removes
                        padding without removing facts.
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {tonePresetOptions.map((tonePreset) => (
                        <button
                          aria-pressed={form.tonePreset === tonePreset}
                          className={`min-h-10 rounded-md border px-4 py-2 text-sm font-semibold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35 focus-visible:ring-offset-2 focus-visible:ring-offset-paper ${
                            form.tonePreset === tonePreset
                              ? "border-ink bg-ink text-paper"
                              : "border-line bg-white text-ink/65 hover:bg-paper hover:text-ink"
                          }`}
                          key={tonePreset}
                          onClick={() => updateTonePreset(tonePreset)}
                          type="button"
                        >
                          {tonePreset}
                        </button>
                      ))}
                    </div>
                  </div>
                  <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-line pt-4">
                    <p className="text-xs text-ink/45">
                      Successful rewrites count after quality checks pass.
                    </p>
                    <Button disabled={!canSubmit} type="submit">
                      {loading ? (
                        <Loader2
                          className="h-4 w-4 animate-spin"
                          aria-hidden="true"
                        />
                      ) : (
                        <Send className="h-4 w-4" aria-hidden="true" />
                      )}
                      Begin rewrite
                    </Button>
                  </div>
                  {loading ? (
                    <div
                      aria-live="polite"
                      className="mt-4 rounded-lg border border-line bg-mint/65 px-3 py-3"
                    >
                      <div className="grid gap-2 md:grid-cols-3">
                        {progressSteps.map((step, index) => (
                          <div
                            className={`flex items-center gap-2 text-xs font-semibold ${
                              index === loadingStepIndex
                                ? "text-ink"
                                : "text-ink/40"
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
                              <span className="h-4 w-4 rounded-full border border-line bg-white/70" />
                            )}
                            {step}
                          </div>
                        ))}
                      </div>
                    </div>
                  ) : null}
                </Card>
              </>
            ) : null}

            {error ? (
              <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {error}
              </p>
            ) : null}
          </form>

          <section className="space-y-5 lg:sticky lg:top-20">
            <Card className="p-4 md:p-5">
              <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.14em] text-sage">
                    Output
                  </p>
                  <h2 className="mt-1 text-lg font-semibold">Ready to send</h2>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button
                    disabled={!result?.rewrittenText}
                    onClick={() => void copyReply()}
                    type="button"
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
              <div className="min-h-[24rem] whitespace-pre-wrap rounded-lg border border-line bg-white p-4 text-sm leading-7 text-ink md:text-base md:leading-8">
                {loading ? (
                  <div className="flex min-h-[21rem] flex-col items-center justify-center text-center text-ink/55">
                    <Sparkles
                      className="mb-3 h-5 w-5 text-clay"
                      aria-hidden="true"
                    />
                    <p className="font-semibold text-ink">
                      {progressSteps[loadingStepIndex]}
                    </p>
                    <p className="mt-1 text-xs">
                      The rewrite is preserving your facts and checking quality.
                    </p>
                  </div>
                ) : (
                  result?.rewrittenText ??
                  (qualityFailure ? (
                    <div className="flex min-h-[21rem] flex-col items-center justify-center text-center text-ink/55">
                      <Sparkles
                        className="mb-3 h-5 w-5 text-clay"
                        aria-hidden="true"
                      />
                      <p className="font-semibold text-ink">
                        {titleForQualityFailure(qualityFailure.reason)}
                      </p>
                      <p className="mt-1 max-w-md text-xs">
                        {qualityFailure.error}
                      </p>
                    </div>
                  ) : null) ??
                  "Your rewritten text will appear here after you run the workspace."
                )}
              </div>
              {showFreeNudge ? (
                <div className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-lg border border-line bg-paper px-3 py-2 text-xs text-ink/55">
                  <span>
                    {freeRewritesRemaining > 0
                      ? `You have ${freeRewritesRemaining} free rewrite(s) left`
                      : "That was your last free rewrite — see plans to keep going"}
                  </span>
                  <Link
                    className="font-semibold text-sage underline-offset-4 hover:underline"
                    href="/pricing"
                  >
                    See plans
                  </Link>
                </div>
              ) : null}
            </Card>

            <Card className="p-4 md:p-5">
              <div className="mb-3">
                <p className="text-xs font-semibold uppercase tracking-[0.14em] text-sage">
                  Primary check
                </p>
                <h2 className="mt-1 text-lg font-semibold">Facts preserved</h2>
              </div>
              <p className="text-sm leading-6 text-ink/60">
                Facts you asked us to keep:
              </p>
              {suppliedFacts.length ? (
                <div className="mt-3 space-y-2">
                  {suppliedFacts.map((fact) => (
                    <label
                      className="flex items-start gap-3 rounded-lg border border-line bg-white px-3 py-2 text-sm leading-6 text-ink/70"
                      key={fact}
                    >
                      <input
                        className="mt-1 h-4 w-4 accent-sage"
                        defaultChecked
                        type="checkbox"
                      />
                      <span>{fact}</span>
                    </label>
                  ))}
                </div>
              ) : (
                <p className="mt-2 rounded-lg border border-line bg-white px-3 py-2 text-sm leading-6 text-ink/55">
                  No facts added yet. Add dates, names, deadlines, or promises
                  before rewriting when they must stay exact.
                </p>
              )}
              <p className="mt-3 text-xs leading-5 text-ink/45">
                This checklist reflects the facts you supplied. Review the final
                reply before sending.
              </p>
            </Card>

            <div className="space-y-3">
              <details className="rounded-lg border border-line bg-white/70 p-4 shadow-soft">
                <summary className="flex cursor-pointer list-none items-center justify-between gap-3 font-semibold">
                  <span>Why this works</span>
                  <span className="text-xs font-medium text-ink/45">
                    {result ? "From this rewrite" : "Before rewrite"}
                  </span>
                </summary>
                <ul className="mt-3 space-y-2 text-sm leading-6 text-ink/65">
                  {whyThisWorks.map((item) => (
                    <li className="flex gap-2" key={item}>
                      <CheckCircle2
                        className="mt-0.5 h-4 w-4 flex-none text-sage"
                        aria-hidden="true"
                      />
                      <span>{item}</span>
                    </li>
                  ))}
                </ul>
              </details>

              <details className="rounded-lg border border-line bg-white/70 p-4 shadow-soft">
                <summary className="flex cursor-pointer list-none items-center justify-between gap-3 font-semibold">
                  <span>Tone check</span>
                  <span className="rounded-md bg-mint px-3 py-1 text-xs font-semibold text-sage">
                    {labelForNaturalness(visibleNaturalness)}
                  </span>
                </summary>
                <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-1 xl:grid-cols-2">
                  <SignalBar
                    label="Draft writing signal"
                    value={visibleNaturalness?.draftAiLikePercent ?? null}
                  />
                  <SignalBar
                    label="Rewrite writing signal"
                    value={visibleNaturalness?.rewriteAiLikePercent ?? null}
                    variant="sage"
                  />
                </div>
                <p className="mt-4 text-sm text-ink/60">
                  Change:{" "}
                  {visibleNaturalness?.changePoints === null ||
                  visibleNaturalness?.changePoints === undefined
                    ? "Unavailable"
                    : `${visibleNaturalness.changePoints} pts`}
                </p>
                <p className="mt-2 text-xs leading-5 text-ink/50">
                  A third-party reference signal that helps compare how natural
                  the draft and rewrite feel. It is not a guarantee; review
                  before sending.
                </p>
              </details>

              <details className="rounded-lg border border-line bg-white/70 p-4 shadow-soft">
                <summary className="flex cursor-pointer list-none items-center justify-between gap-3 font-semibold">
                  <span>Before you send</span>
                  <span className="text-xs font-medium text-ink/45">
                    Quick checklist
                  </span>
                </summary>
                <ul className="mt-3 space-y-2 text-sm leading-6 text-ink/65">
                  {beforeSendChecks.map((item) => (
                    <li className="flex gap-2" key={item}>
                      <CheckCircle2
                        className="mt-0.5 h-4 w-4 flex-none text-clay"
                        aria-hidden="true"
                      />
                      <span>{item}</span>
                    </li>
                  ))}
                </ul>
              </details>
            </div>

            <details className="rounded-lg border border-line bg-white/70 p-4 shadow-soft">
              <summary className="flex cursor-pointer list-none items-center justify-between gap-3 font-semibold">
                <span>Recent rewrites</span>
                <span className="text-xs font-medium text-ink/45">
                  {history.length ? `${history.length} saved locally` : "Empty"}
                </span>
              </summary>
              <div className="mt-4 flex justify-end">
                <Button
                  onClick={clearHistory}
                  type="button"
                  variant="ghost"
                >
                  <Trash2 className="h-4 w-4" aria-hidden="true" />
                  Clear
                </Button>
              </div>
              <div className="mt-3 space-y-3">
                {history.length ? (
                  history.map((item) => (
                    <button
                      className="w-full rounded-lg border border-line bg-white p-3 text-left text-sm transition hover:bg-paper focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/35"
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
                            selectionStatus: "passed",
                          },
                        })
                      }
                      type="button"
                    >
                      <p className="font-medium">
                        {item.mode} - {item.tonePreset}
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
      </div>
    </main>
  );
}
