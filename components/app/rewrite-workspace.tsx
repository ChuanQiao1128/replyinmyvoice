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
  X,
} from "lucide-react";
import Link from "next/link";
import { FormEvent, ReactNode, useEffect, useState } from "react";

import { rewriteInputLimits } from "../../lib/rewrite-limits";
import {
  getRewriteAttemptId,
  isRewritePendingPayload,
  normalizeNaturalness,
  normalizeRewriteResponse,
  type Naturalness,
  type RewriteResponse,
} from "../../lib/rewrite-response";
import { NatBar } from "../landing/nat-bar";
import { Button } from "../ui/button";
import { SubscriptionStatus } from "./subscription-status";

const HISTORY_KEY = "rimv.rewrite.history.v1";
const rewriteAttemptPollLimit = 30;
const rewriteAttemptPollDelayMs = 1500;

type QualityFailure = {
  error: string;
  naturalness?: Naturalness;
  reason?: string;
  charged?: false;
};

type HistoryItem = {
  roughDraftReply: string;
  rewrittenText: string;
  naturalness: Naturalness;
  changeSummary: string[];
  riskNotes: string[];
  createdAt: string;
};

type QuotaCreditSource = {
  source: string;
  label: string;
  remaining: number;
  expiresAt: string | null;
  expiresInDays: number | null;
};

type Props = {
  usageLabel: string;
  subscriptionStatus: string;
  paid: boolean;
  remaining: number;
  quota: number;
  planRemaining: number;
  quotaSources?: QuotaCreditSource[];
};

const progressSteps = [
  "Extracting the facts",
  "Building candidate replies",
  "Reviewing quality gates",
];

function Eyebrow({
  children,
  tone = "muted",
}: {
  children: ReactNode;
  tone?: "muted" | "accent";
}) {
  return (
    <p
      className={`font-mono text-[11px] font-semibold uppercase tracking-[0.18em] ${
        tone === "accent" ? "text-sage" : "text-ink/45"
      }`}
    >
      {children}
    </p>
  );
}

function labelForNaturalness(naturalness?: Naturalness) {
  if (!naturalness || naturalness.label === "unavailable") {
    return "Signal unavailable";
  }
  if (naturalness.label === "lower") {
    return "AI Signal improved";
  }
  if (naturalness.label === "low_signal") {
    return "Already low";
  }
  return "Still high";
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

function payloadString(payload: unknown, key: string) {
  if (payload === null || typeof payload !== "object" || Array.isArray(payload)) {
    return undefined;
  }

  const value = (payload as Record<string, unknown>)[key];
  return typeof value === "string" ? value : undefined;
}

function payloadNaturalness(payload: unknown) {
  if (payload === null || typeof payload !== "object" || Array.isArray(payload)) {
    return undefined;
  }

  const value = (payload as Record<string, unknown>).naturalness;
  return value === undefined ? undefined : normalizeNaturalness(value);
}

function qualityFailureFromPayload(payload: unknown): QualityFailure {
  return {
    error:
      payloadString(payload, "error") ??
      "We could not produce a better version yet. Try again or adjust the draft.",
    naturalness: payloadNaturalness(payload),
    reason: payloadString(payload, "reason"),
    charged: false,
  };
}

class RewriteQualityFailureError extends Error {
  constructor(readonly failure: QualityFailure) {
    super(failure.error);
  }
}

function delay(milliseconds: number) {
  return new Promise((resolve) => window.setTimeout(resolve, milliseconds));
}

async function readJsonPayload(response: Response) {
  return (await response.json().catch(() => null)) as unknown;
}

async function pollRewriteAttempt(attemptId: string) {
  for (let attempt = 0; attempt < rewriteAttemptPollLimit; attempt += 1) {
    await delay(rewriteAttemptPollDelayMs);

    const response = await fetch(
      `/api/rewrite-attempts/${encodeURIComponent(attemptId)}`,
      { cache: "no-store" },
    );
    const payload = await readJsonPayload(response);

    if (response.status === 202 || isRewritePendingPayload(payload)) {
      continue;
    }

    if (!response.ok) {
      if (
        response.status === 422 &&
        payloadString(payload, "code") === "quality_gate_failed"
      ) {
        throw new RewriteQualityFailureError(qualityFailureFromPayload(payload));
      }

      throw new Error(
        payloadString(payload, "error") ?? "Could not rewrite this draft.",
      );
    }

    const normalizedPayload = normalizeRewriteResponse(payload);
    if (!normalizedPayload) {
      throw new Error("Rewrite response was incomplete. Try again in a moment.");
    }

    return normalizedPayload;
  }

  throw new Error("Rewrite is still processing. Try again in a moment.");
}

function normalizeHistoryItems(payload: unknown): HistoryItem[] {
  if (!Array.isArray(payload)) {
    return [];
  }

  return payload
    .map((item) => normalizeHistoryItem(item))
    .filter((item): item is HistoryItem => item !== null)
    .slice(0, 5);
}

function normalizeHistoryItem(payload: unknown): HistoryItem | null {
  if (payload === null || typeof payload !== "object" || Array.isArray(payload)) {
    return null;
  }

  const record = payload as Record<string, unknown>;
  const rewrittenText =
    typeof record.rewrittenText === "string" ? record.rewrittenText : "";
  if (!rewrittenText.trim()) {
    return null;
  }

  return {
    roughDraftReply:
      typeof record.roughDraftReply === "string" ? record.roughDraftReply : "",
    rewrittenText,
    naturalness: normalizeNaturalness(record.naturalness),
    changeSummary: Array.isArray(record.changeSummary)
      ? record.changeSummary.filter(
          (item): item is string => typeof item === "string",
        )
      : [],
    riskNotes: Array.isArray(record.riskNotes)
      ? record.riskNotes.filter((item): item is string => typeof item === "string")
      : [],
    createdAt:
      typeof record.createdAt === "string"
        ? record.createdAt
        : new Date().toISOString(),
  };
}

function historyTitle(item: HistoryItem) {
  const firstLine = item.roughDraftReply
    .split("\n")
    .map((line) => line.trim())
    .find(Boolean);
  if (firstLine) {
    return firstLine.length > 60 ? `${firstLine.slice(0, 60)}…` : firstLine;
  }

  return new Date(item.createdAt).toLocaleString();
}

function signalDeltaOf(naturalness: Naturalness) {
  return naturalness.draftAiLikePercent !== null &&
    naturalness.rewriteAiLikePercent !== null
    ? naturalness.draftAiLikePercent - naturalness.rewriteAiLikePercent
    : null;
}

export function RewriteWorkspace({
  usageLabel,
  subscriptionStatus,
  paid,
  quota,
  planRemaining,
}: Props) {
  const visiblePlanRemaining = Math.max(Math.min(planRemaining, quota), 0);
  const [draft, setDraft] = useState("");
  const [result, setResult] = useState<RewriteResponse | null>(null);
  const [qualityFailure, setQualityFailure] = useState<QualityFailure | null>(
    null,
  );
  const [history, setHistory] = useState<HistoryItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [copied, setCopied] = useState(false);
  const [loadingStepIndex, setLoadingStepIndex] = useState(0);
  const [freeRewritesRemaining, setFreeRewritesRemaining] = useState(
    () => visiblePlanRemaining,
  );
  const [showPostCopyNudge, setShowPostCopyNudge] = useState(false);

  const trimmedDraftLength = draft.trim().length;
  const canSubmit =
    !loading &&
    trimmedDraftLength >= 10 &&
    draft.length <= rewriteInputLimits.roughDraftReply;

  useEffect(() => {
    try {
      const saved = localStorage.getItem(HISTORY_KEY);
      setHistory(saved ? normalizeHistoryItems(JSON.parse(saved)) : []);
    } catch {
      setHistory([]);
    }
  }, []);

  useEffect(() => {
    setFreeRewritesRemaining(visiblePlanRemaining);
  }, [visiblePlanRemaining]);

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
      roughDraftReply: draft,
      rewrittenText: response.rewrittenText,
      naturalness: response.naturalness,
      changeSummary: response.changeSummary,
      riskNotes: response.riskNotes,
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
    setShowPostCopyNudge(false);

    try {
      const response = await fetch("/api/rewrite", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          roughDraftReply: draft,
          tone: "warm",
        }),
      });
      const payload = await readJsonPayload(response);

      if (!response.ok) {
        if (
          response.status === 422 &&
          payloadString(payload, "code") === "quality_gate_failed"
        ) {
          setResult(null);
          setQualityFailure(qualityFailureFromPayload(payload));
          setError("");
          return;
        }
        throw new Error(
          payloadString(payload, "error") ?? "Could not rewrite this draft.",
        );
      }

      let normalizedPayload: RewriteResponse;
      if (response.status === 202 || isRewritePendingPayload(payload)) {
        const attemptId = getRewriteAttemptId(payload);
        if (!attemptId) {
          throw new Error(
            payloadString(payload, "error") ??
              "Rewrite is still processing. Try again in a moment.",
          );
        }
        normalizedPayload = await pollRewriteAttempt(attemptId);
      } else {
        const immediatePayload = normalizeRewriteResponse(payload);
        if (!immediatePayload) {
          throw new Error(
            "Rewrite response was incomplete. Try again in a moment.",
          );
        }
        normalizedPayload = immediatePayload;
      }

      setQualityFailure(null);
      setResult(normalizedPayload);
      if (!paid) {
        setFreeRewritesRemaining((current) => Math.max(current - 1, 0));
      }
      saveHistory(normalizedPayload);
    } catch (submitError) {
      if (submitError instanceof RewriteQualityFailureError) {
        setResult(null);
        setQualityFailure(submitError.failure);
        setError("");
        return;
      }

      setError(
        submitError instanceof Error
          ? submitError.message
          : "Could not rewrite this draft.",
      );
    } finally {
      setLoading(false);
    }
  }

  async function copyReply() {
    if (!result?.rewrittenText) {
      return;
    }

    await navigator.clipboard.writeText(result.rewrittenText);
    setCopied(true);
    if (!paid) {
      setShowPostCopyNudge(true);
    }
  }

  function clearHistory() {
    setHistory([]);
    localStorage.removeItem(HISTORY_KEY);
  }

  function restoreHistory(item: HistoryItem) {
    setDraft(item.roughDraftReply);
    setQualityFailure(null);
    setError("");
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
    });
  }

  function resetWorkspace() {
    setDraft("");
    setResult(null);
    setQualityFailure(null);
    setError("");
    setCopied(false);
    setShowPostCopyNudge(false);
  }

  const visibleNaturalness = result?.naturalness ?? qualityFailure?.naturalness;
  const draftSignal = visibleNaturalness?.draftAiLikePercent ?? null;
  const rewriteSignal = visibleNaturalness?.rewriteAiLikePercent ?? null;
  const hasSignal = draftSignal !== null && rewriteSignal !== null;
  const signalDelta = hasSignal ? draftSignal - rewriteSignal : null;
  const showCopyNudge = !paid && showPostCopyNudge && result !== null;

  return (
    <main className="min-h-screen bg-paper text-ink">
      <div className="mx-auto max-w-5xl px-4 py-8 md:px-6 md:py-10">
        <header className="flex flex-wrap items-end justify-between gap-4">
          <div className="max-w-xl">
            <p className="flex items-center gap-2 font-mono text-[11px] font-semibold uppercase tracking-[0.2em] text-sage">
              <span className="h-1.5 w-1.5 rounded-full bg-sage" aria-hidden="true" />
              Workspace
            </p>
            <h1 className="mt-3 font-serif text-[2.6rem] leading-[1.05] text-ink md:text-5xl">
              Rewrite workspace
            </h1>
            <p className="mt-3 text-sm leading-6 text-ink/55">
              Paste a draft. Get a clearer version that keeps your facts — and
              see the AI Signal before and after.
            </p>
          </div>
          <Button onClick={resetWorkspace} type="button" variant="secondary">
            <FilePlus2 className="h-4 w-4" aria-hidden="true" />
            New draft
          </Button>
        </header>

        <div className="mt-5">
          <SubscriptionStatus
            paid={paid}
            status={subscriptionStatus}
            usageLabel={usageLabel}
          />
        </div>

        {/* Workspace panel — draft ↔ in-your-voice */}
        <div className="mt-5 overflow-hidden rounded-3xl border border-line bg-white shadow-soft">
          <div className="grid md:grid-cols-2 md:divide-x md:divide-line">
            {/* Draft input */}
            <form className="flex flex-col p-5 md:p-6" onSubmit={submit}>
              <div className="mb-3 flex items-center justify-between gap-3">
                <Eyebrow>Your draft</Eyebrow>
                <span className="font-mono text-[11px] text-ink/40">
                  {draft.length}/{rewriteInputLimits.roughDraftReply}
                </span>
              </div>
              <textarea
                className="min-h-[19rem] w-full flex-1 resize-y rounded-2xl border border-line/70 bg-paper/50 px-4 py-3.5 text-[15px] leading-7 text-ink outline-none transition placeholder:text-ink/35 focus:border-clay focus:bg-white focus:ring-4 focus:ring-clay/10"
                id="roughDraftReply"
                maxLength={rewriteInputLimits.roughDraftReply}
                minLength={10}
                onChange={(event) => setDraft(event.target.value)}
                placeholder="Paste the email, message, or note you want to rewrite. The rewrite keeps your facts intact."
                rows={12}
                value={draft}
              />
              <div className="mt-4 flex items-center justify-between gap-3">
                <p className="text-xs text-ink/45">
                  Counts only after quality checks pass.
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
                  Rewrite
                </Button>
              </div>
            </form>

            {/* Result */}
            <div className="flex flex-col bg-paper/35 p-5 md:p-6">
              <div className="mb-3 flex items-center justify-between gap-3">
                <Eyebrow tone="accent">In your voice</Eyebrow>
                <div className="flex items-center gap-1">
                  <button
                    className="inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-sm font-semibold text-ink/70 transition hover:bg-white hover:text-ink disabled:pointer-events-none disabled:opacity-40"
                    disabled={!result?.rewrittenText}
                    onClick={() => void copyReply()}
                    type="button"
                  >
                    {copied ? (
                      <CopyCheck className="h-4 w-4 text-sage" aria-hidden="true" />
                    ) : (
                      <Clipboard className="h-4 w-4" aria-hidden="true" />
                    )}
                    {copied ? "Copied" : "Copy"}
                  </button>
                  <button
                    className="inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-sm font-semibold text-ink/70 transition hover:bg-white hover:text-ink disabled:pointer-events-none disabled:opacity-40"
                    disabled={!canSubmit}
                    onClick={() => void submit()}
                    type="button"
                  >
                    <RefreshCw className="h-4 w-4" aria-hidden="true" />
                    Retry
                  </button>
                </div>
              </div>

              <div className="flex flex-1 flex-col rounded-2xl border border-line/70 bg-white p-4">
                {loading ? (
                  <div className="flex min-h-[16rem] flex-1 flex-col justify-center gap-5">
                    <div className="flex items-center gap-2.5">
                      <Loader2
                        className="h-4 w-4 animate-spin text-clay"
                        aria-hidden="true"
                      />
                      <span className="font-mono text-[11px] font-semibold uppercase tracking-[0.18em] text-ink/60">
                        Rewriting
                      </span>
                    </div>
                    <ol aria-live="polite" className="space-y-3">
                      {progressSteps.map((step, index) => (
                        <li
                          className={`flex items-center gap-3 text-sm transition ${
                            index === loadingStepIndex
                              ? "font-medium text-ink"
                              : index < loadingStepIndex
                                ? "text-ink/50"
                                : "text-ink/30"
                          }`}
                          key={step}
                        >
                          <span className="flex h-5 w-5 items-center justify-center">
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
                              <span className="h-1.5 w-1.5 rounded-full bg-line" />
                            )}
                          </span>
                          {step}
                        </li>
                      ))}
                    </ol>
                    <p className="text-xs text-ink/45">
                      Keeping your facts intact and checking quality.
                    </p>
                  </div>
                ) : result?.rewrittenText ? (
                  <div className="min-h-[16rem] whitespace-pre-wrap text-[15px] leading-8 text-ink">
                    {result.rewrittenText}
                  </div>
                ) : qualityFailure ? (
                  <div className="flex min-h-[16rem] flex-1 flex-col items-center justify-center text-center">
                    <Sparkles
                      className="mb-3 h-5 w-5 text-clay"
                      aria-hidden="true"
                    />
                    <p className="font-semibold text-ink">
                      {titleForQualityFailure(qualityFailure.reason)}
                    </p>
                    <p className="mt-1 max-w-md text-xs leading-5 text-ink/55">
                      {qualityFailure.error}
                    </p>
                  </div>
                ) : (
                  <div className="flex min-h-[16rem] flex-1 flex-col items-center justify-center text-center">
                    <Sparkles
                      className="mb-3 h-5 w-5 text-ink/25"
                      aria-hidden="true"
                    />
                    <p className="max-w-[15rem] text-sm leading-6 text-ink/40">
                      Your rewritten reply will appear here, ready to copy.
                    </p>
                  </div>
                )}
              </div>

              {showCopyNudge ? (
                <div className="mt-3 rounded-2xl border border-line bg-mint/70 p-3.5 text-sm text-ink/70">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="font-semibold text-ink">
                        Keep writing in your own voice.
                      </p>
                      <p className="mt-1 leading-6">
                        Starter gives you 55 rewrites a month for the emails and
                        messages you actually need to send.
                      </p>
                      <p className="mt-1 font-mono text-[11px] text-ink/45">
                        {freeRewritesRemaining > 0
                          ? `${freeRewritesRemaining} free rewrite${freeRewritesRemaining === 1 ? "" : "s"} left after this copy.`
                          : "That was your last free rewrite."}
                      </p>
                    </div>
                    <button
                      aria-label="Dismiss"
                      className="rounded-md p-1 text-ink/45 transition hover:bg-white hover:text-ink"
                      onClick={() => setShowPostCopyNudge(false)}
                      type="button"
                    >
                      <X className="h-4 w-4" aria-hidden="true" />
                    </button>
                  </div>
                  <Link
                    className="mt-2 inline-flex font-semibold text-sage underline-offset-4 hover:underline"
                    href="/pricing"
                  >
                    See plans
                  </Link>
                </div>
              ) : null}
            </div>
          </div>
        </div>

        {/* AI Signal — focal before/after band */}
        <div className="mt-5 rounded-3xl border border-line bg-white p-5 shadow-soft md:p-6">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <Eyebrow>AI Signal · before vs after</Eyebrow>
            {hasSignal ? (
              <span className="rounded-full bg-mint px-3 py-1 font-mono text-[11px] font-semibold text-sage">
                {labelForNaturalness(visibleNaturalness)}
              </span>
            ) : null}
          </div>
          {hasSignal ? (
            <div className="space-y-3.5">
              <div className="flex flex-wrap items-center gap-4">
                <div className="flex min-w-[240px] flex-1">
                  <NatBar after={rewriteSignal} animate before={draftSignal} />
                </div>
                {signalDelta !== null ? (
                  <span
                    className={`rounded-lg px-3 py-2 font-mono text-sm font-semibold ${
                      signalDelta > 0
                        ? "bg-mint text-sage"
                        : signalDelta < 0
                          ? "bg-rust/10 text-rust"
                          : "bg-paper-deep text-ink/60"
                    }`}
                  >
                    {signalDelta > 0
                      ? `−${signalDelta} pts more natural`
                      : signalDelta < 0
                        ? `+${Math.abs(signalDelta)} pts`
                        : "No change"}
                  </span>
                ) : null}
              </div>
              <div className="flex flex-wrap items-center gap-x-5 gap-y-1 font-mono text-[11px] text-ink/55">
                <span className="flex items-center gap-1.5">
                  <span className="h-2 w-2 rounded-sm bg-rust" aria-hidden="true" />
                  Draft {draftSignal}%
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="h-2 w-2 rounded-sm bg-sage" aria-hidden="true" />
                  Rewrite {rewriteSignal}%
                </span>
              </div>
            </div>
          ) : (
            <div className="rounded-2xl border border-dashed border-line bg-paper/40 px-4 py-7 text-center text-sm text-ink/40">
              Run a rewrite to see the AI Signal before and after.
            </div>
          )}
          <p className="mt-4 text-xs leading-5 text-ink/45">
            A third-party reference signal — lower reads more natural. It is not
            a guarantee; review before sending.
          </p>
        </div>

        {error ? (
          <p className="mt-5 rounded-xl border border-rust/30 bg-rust/5 px-4 py-3 text-sm text-rust">
            {error}
          </p>
        ) : null}

        {/* Recent rewrites */}
        <details className="group mt-5 rounded-2xl border border-line bg-white/60 px-5 py-4">
          <summary className="flex cursor-pointer list-none items-center justify-between gap-3">
            <Eyebrow>Recent rewrites</Eyebrow>
            <span className="font-mono text-[11px] text-ink/40">
              {history.length ? `${history.length} saved locally` : "Empty"}
            </span>
          </summary>
          {history.length ? (
            <>
              <div className="mt-3 flex justify-end">
                <button
                  className="inline-flex items-center gap-1.5 text-xs font-semibold text-ink/50 transition hover:text-rust"
                  onClick={clearHistory}
                  type="button"
                >
                  <Trash2 className="h-3.5 w-3.5" aria-hidden="true" />
                  Clear
                </button>
              </div>
              <div className="mt-2 grid gap-2.5 sm:grid-cols-2">
                {history.map((item) => {
                  const delta = signalDeltaOf(item.naturalness);
                  return (
                    <button
                      className="rounded-2xl border border-line bg-white p-3.5 text-left transition hover:border-clay/40 hover:shadow-crisp focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-clay/30"
                      key={item.createdAt}
                      onClick={() => restoreHistory(item)}
                      type="button"
                    >
                      <div className="flex items-start justify-between gap-2">
                        <p className="line-clamp-1 text-sm font-medium text-ink">
                          {historyTitle(item)}
                        </p>
                        {delta !== null && delta > 0 ? (
                          <span className="shrink-0 rounded-md bg-mint px-1.5 py-0.5 font-mono text-[10px] font-semibold text-sage">
                            −{delta}
                          </span>
                        ) : null}
                      </div>
                      <p className="mt-1 line-clamp-2 text-sm leading-6 text-ink/55">
                        {item.rewrittenText}
                      </p>
                    </button>
                  );
                })}
              </div>
            </>
          ) : (
            <p className="mt-3 text-sm text-ink/50">
              Rewrites stay in this browser only and are not saved to the
              database.
            </p>
          )}
        </details>
      </div>
    </main>
  );
}
