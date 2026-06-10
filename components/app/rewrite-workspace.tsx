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
  X,
} from "lucide-react";
import Link from "next/link";
import { FormEvent, ReactNode, useEffect, useState } from "react";

import type { AppExperience, PromoAccountState } from "../../lib/promo-app-state";
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
import { Textarea } from "../ui/textarea";
import { RedeemCodeCard } from "./redeem-code-card";
import shell from "./shell/shell.module.css";
import { SubscriptionStatus } from "./subscription-status";

const rewriteAttemptPollLimit = 30;
const rewriteAttemptPollDelayMs = 1500;

type QualityFailure = {
  error: string;
  naturalness?: Naturalness;
  reason?: string;
  charged?: false;
};

type QuotaCreditSource = {
  source: string;
  label: string;
  remaining: number;
  expiresAt: string | null;
  expiresInDays: number | null;
};

type Props = {
  rewriteHistoryUserKey: string;
  usageLabel: string;
  subscriptionStatus: string;
  paymentGraceEndsAt: string | null;
  paid: boolean;
  remaining: number;
  quota: number;
  planRemaining: number;
  appExperience: AppExperience;
  canRedeem: boolean;
  outOfCredits: boolean;
  promoState: PromoAccountState;
  usageExhausted: boolean;
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
        tone === "accent" ? "text-sage" : "text-ink/50"
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

function outOfCreditsHint(paid: boolean, canRedeem: boolean) {
  if (paid) {
    return "Your monthly rewrite quota has been used for this billing period. Use Manage billing in the bar above.";
  }

  return canRedeem
    ? "You're out of rewrites. Use Redeem code or Buy rewrites in the bar above."
    : "You're out of rewrites. Use Buy rewrites in the bar above.";
}

function OutOfCreditsNudge({
  canRedeem,
  paid,
}: {
  canRedeem: boolean;
  paid: boolean;
}) {
  return (
    <div className="mb-4 rounded-lg border border-sage/25 bg-paper-deep/70 p-4 text-left text-sm text-ink/70">
      <p className="font-semibold text-ink">
        {paid ? "Your monthly limit has been reached." : "No rewrites left."}
      </p>
      <p className="mt-1 leading-6">{outOfCreditsHint(paid, canRedeem)}</p>
    </div>
  );
}

export function RewriteWorkspace({
  appExperience,
  canRedeem,
  outOfCredits,
  usageLabel,
  subscriptionStatus,
  paymentGraceEndsAt,
  paid,
  quota,
  planRemaining,
  promoState,
  rewriteHistoryUserKey,
  remaining,
  usageExhausted,
}: Props) {
  const visiblePlanRemaining = Math.max(Math.min(planRemaining, quota), 0);
  const [draft, setDraft] = useState("");
  const [result, setResult] = useState<RewriteResponse | null>(null);
  const [qualityFailure, setQualityFailure] = useState<QualityFailure | null>(
    null,
  );
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [copied, setCopied] = useState(false);
  const [loadingStepIndex, setLoadingStepIndex] = useState(0);
  const [freeRewritesRemaining, setFreeRewritesRemaining] = useState(
    () => visiblePlanRemaining,
  );
  const [showPostCopyNudge, setShowPostCopyNudge] = useState(false);
  const [redeemModalOpen, setRedeemModalOpen] = useState(false);

  const trimmedDraftLength = draft.trim().length;
  const canSubmit =
    !loading &&
    trimmedDraftLength >= 10 &&
    draft.length <= rewriteInputLimits.roughDraftReply;

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
  // Only flag a regression when the OUTPUT itself still reads AI-ish. A warmer rewrite of an
  // already-natural draft can nudge the signal up (the reference signal scores short
  // pleasantries high) — that is the product doing its job, not a failure, so we never punish it.
  const outputReadsNatural = rewriteSignal !== null && rewriteSignal <= 40;
  const signalRegressed =
    signalDelta !== null && signalDelta < 0 && !outputReadsNatural;
  const showCopyNudge = !paid && showPostCopyNudge && result !== null;
  const showOutOfCreditsNudge =
    remaining <= 0 ||
    outOfCredits ||
    usageExhausted ||
    appExperience === "needsRedeem" ||
    appExperience === "needsBuy";
  const showRedeemAction =
    canRedeem && (!promoState.hasRedeemed || promoState.trialRemaining === 0);

  function openRedeemModal() {
    setRedeemModalOpen(true);
  }

  function closeRedeemModal() {
    setRedeemModalOpen(false);
  }

  return (
    <div className="w-full text-ink">
      <div className={shell.pageHeaderRow}>
        <header className={shell.pageHeader}>
          <h1 className={shell.pageTitle}>Rewrite</h1>
          <p className={shell.pageDesc}>
            Paste a draft. Get a clearer version that keeps your facts — and
            see the AI Signal before and after.
          </p>
        </header>
        <Button onClick={resetWorkspace} type="button" variant="secondary">
          <FilePlus2 className="h-4 w-4" aria-hidden="true" />
          New draft
        </Button>
      </div>

      <div>
          <SubscriptionStatus
            canRedeem={showRedeemAction}
            onRedeemClick={openRedeemModal}
            paid={paid}
            paymentGraceEndsAt={paymentGraceEndsAt}
            status={subscriptionStatus}
            usageLabel={usageLabel}
          />
        </div>

        <div className="mt-6 grid overflow-hidden rounded-2xl border border-line bg-white shadow-soft md:grid-cols-2 md:divide-x md:divide-line">
          <form className="flex flex-col p-6 md:p-8" onSubmit={submit}>
            <div className="mb-4 flex min-h-11 items-center justify-between gap-3">
              <Eyebrow>YOUR DRAFT</Eyebrow>
              <span className="font-mono text-[11px] text-ink/50">
                {draft.length}/{rewriteInputLimits.roughDraftReply}
              </span>
            </div>
            <Textarea
              className="min-h-[28rem] flex-1 bg-paper/50 p-5 text-base leading-relaxed focus:bg-white"
              id="roughDraftReply"
              maxLength={rewriteInputLimits.roughDraftReply}
              minLength={10}
              onChange={(event) => setDraft(event.target.value)}
              placeholder="Paste the email, message, or note you want to rewrite. The rewrite keeps your facts intact."
              rows={16}
              value={draft}
            />
            <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <p className="text-xs text-ink/50">
                Counts only after quality checks pass.
              </p>
              <Button className="w-full sm:w-auto" disabled={!canSubmit} type="submit">
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
            <p className="mt-4 text-xs leading-5 text-ink/50">
              By choosing Rewrite, you agree that pasted messages and rewrites
              are processed for this request and retained for up to 90 days by
              default. Raw content is then removed, and you can delete history
              items from your History page.
            </p>
          </form>

          <section className="flex flex-col border-t border-line p-6 md:border-t-0 md:p-8">
            <div className="mb-4 flex min-h-11 flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <Eyebrow tone="accent">IN YOUR VOICE</Eyebrow>
              <div className="flex items-center gap-3">
                <Button
                  className="px-3 text-ink/70 hover:bg-paper"
                  disabled={!result?.rewrittenText}
                  onClick={() => void copyReply()}
                  type="button"
                  variant="ghost"
                >
                  {copied ? (
                    <CopyCheck className="h-4 w-4 text-sage" aria-hidden="true" />
                  ) : (
                    <Clipboard className="h-4 w-4" aria-hidden="true" />
                  )}
                  Copy
                </Button>
                <Button
                  className="px-3 text-ink/70 hover:bg-paper"
                  disabled={!canSubmit}
                  onClick={() => void submit()}
                  type="button"
                  variant="ghost"
                >
                  <RefreshCw className="h-4 w-4" aria-hidden="true" />
                  Retry
                </Button>
              </div>
            </div>

            <div className="flex min-h-[28rem] flex-1 flex-col rounded-lg border border-line/70 bg-mint/20 p-5 md:p-6">
              {showOutOfCreditsNudge ? (
                <OutOfCreditsNudge canRedeem={showRedeemAction} paid={paid} />
              ) : null}

              {loading ? (
                <div className="flex flex-1 flex-col justify-center gap-5">
                  <div className="flex items-center gap-3">
                    <Loader2
                      className="h-4 w-4 animate-spin text-sage"
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
                              ? "text-ink/55"
                              : "text-ink/35"
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
                              className="h-4 w-4 animate-spin text-sage"
                              aria-hidden="true"
                            />
                          ) : (
                            <span className="h-2 w-2 rounded-lg bg-line" />
                          )}
                        </span>
                        {step}
                      </li>
                    ))}
                  </ol>
                  <p className="text-xs text-ink/50">
                    Keeping your facts intact and checking quality.
                  </p>
                </div>
              ) : result?.rewrittenText ? (
                <div className="whitespace-pre-wrap text-base leading-8 text-ink">
                  {result.rewrittenText}
                </div>
              ) : qualityFailure ? (
                <div className="flex flex-1 flex-col items-center justify-center text-center">
                  <Sparkles className="mb-3 h-5 w-5 text-sage" aria-hidden="true" />
                  <p className="font-semibold text-ink">
                    {titleForQualityFailure(qualityFailure.reason)}
                  </p>
                  <p className="mt-2 max-w-md text-sm leading-relaxed text-ink/60">
                    {qualityFailure.error}
                  </p>
                </div>
              ) : (
                <div className="flex flex-1 flex-col items-center justify-center text-center">
                  <Sparkles className="mb-3 h-5 w-5 text-ink/25" aria-hidden="true" />
                  <p className="max-w-xs text-sm leading-6 text-ink/45">
                    Your rewritten reply will appear here, ready to copy.
                  </p>
                </div>
              )}
            </div>

            {showCopyNudge ? (
              <div className="mt-4 rounded-2xl border border-line bg-mint/70 p-4 text-sm text-ink/70">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold text-ink">
                      Keep writing in your own voice.
                    </p>
                    <p className="mt-1 leading-6">
                      The Value Pack gives you 30 rewrites for the emails and
                      messages you actually need to send.
                    </p>
                    <p className="mt-2 font-mono text-[11px] text-ink/50">
                      {freeRewritesRemaining > 0
                        ? `${freeRewritesRemaining} trial rewrite${freeRewritesRemaining === 1 ? "" : "s"} left after this copy.`
                        : "That was your last trial rewrite."}
                    </p>
                  </div>
                  <Button
                    aria-label="Dismiss"
                    className="min-h-11 w-11 shrink-0 p-0 text-ink/45 hover:bg-white hover:text-ink"
                    onClick={() => setShowPostCopyNudge(false)}
                    type="button"
                    variant="ghost"
                  >
                    <X className="h-4 w-4" aria-hidden="true" />
                  </Button>
                </div>
                <Link
                  className="mt-3 inline-flex font-semibold text-sage underline-offset-4 hover:underline"
                  href="/pricing"
                >
                  See plans
                </Link>
              </div>
            ) : null}
          </section>
        </div>

        {/* AI Signal — focal before/after band */}
        <section className="mt-6 rounded-2xl border border-line bg-white p-5 shadow-soft md:p-6">
          {hasSignal ? (
            <>
              <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                <Eyebrow>AI SIGNAL · BEFORE VS AFTER</Eyebrow>
                {signalDelta !== null ? (
                  <span
                    className={`rounded-lg px-3 py-2 font-mono text-sm font-semibold ${
                      signalDelta > 0
                        ? "bg-mint text-sage"
                        : signalRegressed
                          ? "bg-rust/10 text-rust"
                          : "bg-paper-deep text-ink/60"
                    }`}
                    title={labelForNaturalness(visibleNaturalness)}
                  >
                    {signalDelta > 0
                      ? `−${signalDelta} pts more natural`
                      : signalRegressed
                        ? `+${Math.abs(signalDelta)} pts`
                        : "Reads natural"}
                  </span>
                ) : null}
              </div>
              <div className="mt-4 flex flex-col gap-4 lg:flex-row lg:items-center">
                <div className="min-w-0 flex-1 basis-64">
                  <NatBar after={rewriteSignal} animate before={draftSignal} />
                </div>
                <div className="flex flex-wrap items-center gap-3 font-mono text-[11px] text-ink/60">
                  <span className="rounded-lg bg-paper-deep px-3 py-2">
                    Draft {draftSignal}%
                  </span>
                  <span className="rounded-lg bg-mint px-3 py-2 text-sage">
                    Rewrite {rewriteSignal}%
                  </span>
                </div>
              </div>
              <p className="mt-4 text-xs leading-5 text-ink/50">
                A third-party reference signal — lower reads more natural. It is
                not a guarantee; review before sending.
              </p>
            </>
          ) : (
            <p className="rounded-lg border border-line bg-paper/60 px-4 py-3 text-sm text-ink/55">
              Run a rewrite to see the AI Signal before and after.
            </p>
          )}
        </section>

        {error ? (
          <p className="mt-6 rounded-lg border border-rust/30 bg-rust/5 px-4 py-3 text-sm text-rust">
            {error}
          </p>
        ) : null}

      <RedeemCodeCard open={redeemModalOpen} onClose={closeRedeemModal} />
    </div>
  );
}
