"use client";

import {
  CheckCircle2,
  Clipboard,
  CopyCheck,
  CreditCard,
  FilePlus2,
  Loader2,
  RefreshCw,
  Send,
  Sparkles,
  Ticket,
  X,
} from "lucide-react";
import Link from "next/link";
import {
  type FormEvent,
  type KeyboardEvent,
  type ReactNode,
  useEffect,
  useRef,
  useState,
} from "react";

import { openBillingPortal } from "../../lib/billing-portal";
import { failureCopy } from "../../lib/failure-copy";
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
import { homepageSampleCases } from "../landing/sample-cases";
import { Button, LinkButton } from "../ui/button";
import { Textarea } from "../ui/textarea";
import { workspacePacks } from "./buy-rewrites-dialog";
import type { CheckoutStatus } from "./checkout-banner";
import { FirstRunChecklist } from "./first-run-checklist";
import { RedeemCodeCard } from "./redeem-code-card";
import shell from "./shell/shell.module.css";
import { SubscriptionStatus } from "./subscription-status";

const rewriteAttemptPollLimit = 30;
const rewriteAttemptPollDelayMs = 1500;
const rewriteSlowPathDelayMs = 35000;
const draftAutosaveKey = "rimv.workspace.draft.v1";
const minimumDraftLength = 10;

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
  checkoutStatus?: CheckoutStatus | null;
};

const progressSteps = [
  "Extracting the facts",
  "Building candidate replies",
  "Reviewing quality gates",
];
const workspaceExampleSample = homepageSampleCases[0];
const signalRegressionTooltip =
  "the rewrite reads less natural than your draft — can happen with very short or already-natural drafts; review before sending.";

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
    return "AI Signal unavailable";
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
    return "AI Signal unavailable";
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
      failureCopy.workspace.qualityDefault,
    naturalness: payloadNaturalness(payload),
    reason: payloadString(payload, "reason"),
    charged: false,
  };
}

function readSavedDraft() {
  try {
    return window.localStorage.getItem(draftAutosaveKey) ?? "";
  } catch {
    return "";
  }
}

function writeSavedDraft(draft: string) {
  try {
    if (draft.length > 0) {
      window.localStorage.setItem(draftAutosaveKey, draft);
      return;
    }

    window.localStorage.removeItem(draftAutosaveKey);
  } catch {
    return;
  }
}

class RewriteQualityFailureError extends Error {
  constructor(readonly failure: QualityFailure) {
    super(failure.error);
  }
}

function createAbortError() {
  const error = new Error("Rewrite cancelled.");
  error.name = "AbortError";
  return error;
}

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === "AbortError";
}

function delay(milliseconds: number, signal?: AbortSignal) {
  return new Promise<void>((resolve, reject) => {
    if (signal?.aborted) {
      reject(createAbortError());
      return;
    }

    function handleAbort() {
      window.clearTimeout(timer);
      reject(createAbortError());
    }

    const timer = window.setTimeout(() => {
      signal?.removeEventListener("abort", handleAbort);
      resolve();
    }, milliseconds);

    signal?.addEventListener("abort", handleAbort, { once: true });
  });
}

async function readJsonPayload(response: Response) {
  return (await response.json().catch(() => null)) as unknown;
}

async function pollRewriteAttempt(attemptId: string, signal: AbortSignal) {
  for (let attempt = 0; attempt < rewriteAttemptPollLimit; attempt += 1) {
    await delay(rewriteAttemptPollDelayMs, signal);

    const response = await fetch(
      `/api/rewrite-attempts/${encodeURIComponent(attemptId)}`,
      { cache: "no-store", signal },
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
    return "Your monthly rewrite quota has been used for this billing period.";
  }

  return canRedeem
    ? "You're out of rewrites. Redeem a code or buy a pack to keep going."
    : "You're out of rewrites. Buy a pack to keep going.";
}

function ManageBillingButton({ className = "" }: { className?: string }) {
  const [billingLoading, setBillingLoading] = useState(false);
  const [billingError, setBillingError] = useState("");

  async function handleManageBilling() {
    setBillingLoading(true);
    setBillingError("");

    try {
      await openBillingPortal();
    } catch (billingPortalError) {
      setBillingError(
        billingPortalError instanceof Error
          ? billingPortalError.message
          : "Could not open billing.",
      );
      setBillingLoading(false);
    }
  }

  return (
    <div className={className}>
      <Button
        className="w-full sm:w-auto"
        disabled={billingLoading}
        onClick={() => void handleManageBilling()}
        type="button"
        variant="secondary"
      >
        <CreditCard className="h-4 w-4" aria-hidden="true" />
        {billingLoading ? "Opening..." : "Manage billing"}
      </Button>
      {billingError ? (
        <p className="mt-2 text-sm font-medium text-sage" role="alert">
          {billingError}
        </p>
      ) : null}
    </div>
  );
}

function LowCreditBanner({
  paid,
  remaining,
  quota,
}: {
  paid: boolean;
  remaining: number;
  quota: number;
}) {
  const total = Math.max(quota, 0);
  const visibleRemaining = Math.max(remaining, 0);

  return (
    <section className="mt-4 flex flex-col gap-3 rounded-lg border border-clay/25 bg-clay/10 p-4 text-sm text-ink/70 md:flex-row md:items-center md:justify-between">
      <div>
        <p className="font-semibold text-ink">Low rewrite balance</p>
        <p className="mt-1 leading-6">
          {visibleRemaining} of {total || "your"} rewrites left. Add more before
          your next batch of replies.
        </p>
      </div>
      {paid ? (
        <ManageBillingButton className="shrink-0" />
      ) : (
        <LinkButton
          className="w-full shrink-0 sm:w-auto"
          href="/pricing"
          variant="primary"
        >
          Get more rewrites
        </LinkButton>
      )}
    </section>
  );
}

function OutOfCreditsNudge({
  canRedeem,
  onRedeemClick,
  paid,
}: {
  canRedeem: boolean;
  onRedeemClick: () => void;
  paid: boolean;
}) {
  return (
    <div className="mb-4 rounded-lg border border-sage/25 bg-paper-deep/70 p-4 text-left text-sm text-ink/70">
      <p className="font-semibold text-ink">
        {paid ? "Your monthly limit has been reached." : "No rewrites left."}
      </p>
      <p className="mt-1 leading-6">{outOfCreditsHint(paid, canRedeem)}</p>
      {paid ? (
        <ManageBillingButton className="mt-3" />
      ) : (
        <div className="mt-3 flex flex-col gap-2 sm:flex-row">
          {canRedeem ? (
            <Button
              className="w-full sm:w-auto"
              onClick={onRedeemClick}
              type="button"
              variant="secondary"
            >
              <Ticket className="h-4 w-4" aria-hidden="true" />
              Redeem code
            </Button>
          ) : null}
          <LinkButton
            className="w-full sm:w-auto"
            href="/pricing"
            variant="primary"
          >
            Buy rewrites
          </LinkButton>
        </div>
      )}
    </div>
  );
}

export function RewriteWorkspace({
  appExperience,
  canRedeem,
  checkoutStatus = null,
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
  const activeRewriteAbortRef = useRef<AbortController | null>(null);
  const resultHeadingRef = useRef<HTMLHeadingElement | null>(null);
  const [draft, setDraft] = useState("");
  const [submittedDraft, setSubmittedDraft] = useState("");
  const [result, setResult] = useState<RewriteResponse | null>(null);
  const [compareOpen, setCompareOpen] = useState(false);
  const [qualityFailure, setQualityFailure] = useState<QualityFailure | null>(
    null,
  );
  const [loading, setLoading] = useState(false);
  const [rewriteIsSlow, setRewriteIsSlow] = useState(false);
  const [error, setError] = useState("");
  const [copied, setCopied] = useState(false);
  const [loadingStepIndex, setLoadingStepIndex] = useState(0);
  const [resultAnnouncement, setResultAnnouncement] = useState("");
  const [rewriteSucceededThisSession, setRewriteSucceededThisSession] =
    useState(false);
  const [freeRewritesRemaining, setFreeRewritesRemaining] = useState(
    () => visiblePlanRemaining,
  );
  const [showPostCopyNudge, setShowPostCopyNudge] = useState(false);
  const [qualityTipsOpen, setQualityTipsOpen] = useState(false);
  const [redeemModalOpen, setRedeemModalOpen] = useState(false);
  const [draftAutosaveReady, setDraftAutosaveReady] = useState(false);
  const [draftRestoreVisible, setDraftRestoreVisible] = useState(false);

  const trimmedDraftLength = draft.trim().length;
  const draftBelowMinimum = trimmedDraftLength < minimumDraftLength;
  const canSubmit =
    !loading &&
    trimmedDraftLength >= minimumDraftLength &&
    draft.length <= rewriteInputLimits.roughDraftReply;

  useEffect(() => {
    const savedDraft = readSavedDraft();
    if (savedDraft) {
      setDraft(savedDraft);
      setDraftRestoreVisible(true);
    }
    setDraftAutosaveReady(true);
  }, []);

  useEffect(() => {
    if (!draftAutosaveReady) {
      return;
    }

    writeSavedDraft(draft);
  }, [draft, draftAutosaveReady]);

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
    if (!loading) {
      setRewriteIsSlow(false);
      return;
    }

    const timer = window.setTimeout(
      () => setRewriteIsSlow(true),
      rewriteSlowPathDelayMs,
    );

    return () => window.clearTimeout(timer);
  }, [loading]);

  useEffect(() => {
    if (!result?.rewrittenText) {
      return;
    }

    setResultAnnouncement(
      "Rewrite complete. Your reply is ready to review and copy.",
    );
    const frame = window.requestAnimationFrame(() => {
      resultHeadingRef.current?.focus();
    });

    return () => window.cancelAnimationFrame(frame);
  }, [result?.rewrittenText]);

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

    const draftForAttempt = draft;
    const abortController = new AbortController();
    activeRewriteAbortRef.current = abortController;
    setLoading(true);
    setCompareOpen(false);
    setError("");
    setQualityFailure(null);
    setQualityTipsOpen(false);
    setResultAnnouncement("");
    setShowPostCopyNudge(false);

    try {
      const response = await fetch("/api/rewrite", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          roughDraftReply: draftForAttempt,
          tone: "warm",
        }),
        signal: abortController.signal,
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
        normalizedPayload = await pollRewriteAttempt(attemptId, abortController.signal);
      } else {
        const immediatePayload = normalizeRewriteResponse(payload);
        if (!immediatePayload) {
          throw new Error(
            "Rewrite response was incomplete. Try again in a moment.",
          );
        }
        normalizedPayload = immediatePayload;
      }

      if (abortController.signal.aborted) {
        return;
      }

      setQualityFailure(null);
      setSubmittedDraft(draftForAttempt);
      setResult(normalizedPayload);
      setRewriteSucceededThisSession(true);
      if (!paid) {
        setFreeRewritesRemaining((current) => Math.max(current - 1, 0));
      }
    } catch (submitError) {
      if (isAbortError(submitError) || abortController.signal.aborted) {
        setError("");
        setQualityFailure(null);
        return;
      }

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
      if (activeRewriteAbortRef.current === abortController) {
        activeRewriteAbortRef.current = null;
        setLoading(false);
      }
    }
  }

  function updateDraft(value: string) {
    setDraft(value);
    if (draftRestoreVisible) {
      setDraftRestoreVisible(false);
    }
  }

  function clearSavedDraft() {
    writeSavedDraft("");
    setDraft("");
    setDraftRestoreVisible(false);
  }

  function handleDraftKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key !== "Enter" || (!event.metaKey && !event.ctrlKey)) {
      return;
    }

    if (!canSubmit) {
      return;
    }

    event.preventDefault();
    void submit();
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
    activeRewriteAbortRef.current?.abort();
    activeRewriteAbortRef.current = null;
    setDraft("");
    writeSavedDraft("");
    setDraftRestoreVisible(false);
    setSubmittedDraft("");
    setResult(null);
    setCompareOpen(false);
    setQualityFailure(null);
    setError("");
    setCopied(false);
    setLoading(false);
    setRewriteIsSlow(false);
    setResultAnnouncement("");
    setQualityTipsOpen(false);
    setShowPostCopyNudge(false);
  }

  function loadExampleDraft() {
    setDraft(workspaceExampleSample.draft);
    setDraftRestoreVisible(false);
    setSubmittedDraft("");
    setResult(null);
    setCompareOpen(false);
    setQualityFailure(null);
    setError("");
    setCopied(false);
    setResultAnnouncement("");
    setQualityTipsOpen(false);
    setShowPostCopyNudge(false);
  }

  function cancelRewrite() {
    activeRewriteAbortRef.current?.abort();
    activeRewriteAbortRef.current = null;
    setLoading(false);
    setRewriteIsSlow(false);
    setError("");
    setQualityFailure(null);
    setCopied(false);
    setCompareOpen(false);
    setResultAnnouncement("");
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
  const showLowCreditBanner =
    !showOutOfCreditsNudge &&
    remaining > 0 &&
    (remaining <= 2 || (quota > 0 && remaining / quota <= 0.15));
  const showRedeemAction =
    canRedeem && (!promoState.hasRedeemed || promoState.trialRemaining === 0);
  const firstRunRewriteBalance = Math.max(
    remaining,
    visiblePlanRemaining,
    freeRewritesRemaining,
    promoState.trialRemaining,
    0,
  );

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
            see the AI Signal (naturalness) before and after.
          </p>
        </header>
        <Button onClick={resetWorkspace} type="button" variant="secondary">
          <FilePlus2 className="h-4 w-4" aria-hidden="true" />
          New draft
        </Button>
      </div>

      <FirstRunChecklist
        canRedeem={showRedeemAction}
        onRedeemClick={openRedeemModal}
        rewriteBalance={firstRunRewriteBalance}
        rewriteHistoryUserKey={rewriteHistoryUserKey}
        rewriteSucceededThisSession={rewriteSucceededThisSession}
      />

      <div>
          <SubscriptionStatus
            canRedeem={showRedeemAction}
            checkoutStatus={checkoutStatus}
            onRedeemClick={openRedeemModal}
            paid={paid}
            paymentGraceEndsAt={paymentGraceEndsAt}
            status={subscriptionStatus}
            usageLabel={usageLabel}
          />
        </div>

        {showLowCreditBanner ? (
          <LowCreditBanner paid={paid} quota={quota} remaining={remaining} />
        ) : null}

        <div className="mt-6 grid overflow-hidden rounded-2xl border border-line bg-white shadow-soft md:grid-cols-2 md:divide-x md:divide-line">
          <form className="flex flex-col p-6 md:p-8" onSubmit={submit}>
            <div className="mb-4 flex min-h-11 items-center justify-between gap-3">
              <Eyebrow>YOUR DRAFT</Eyebrow>
              <span className="font-mono text-[11px] text-ink/50">
                {draft.length}/{rewriteInputLimits.roughDraftReply}
              </span>
            </div>
            <Textarea
              className="min-h-[16rem] max-h-[min(28rem,44svh)] flex-1 overflow-y-auto bg-paper/50 p-5 text-base leading-relaxed focus:bg-white md:min-h-[28rem] md:max-h-[min(34rem,68svh)]"
              id="roughDraftReply"
              maxLength={rewriteInputLimits.roughDraftReply}
              minLength={minimumDraftLength}
              onChange={(event) => updateDraft(event.target.value)}
              onKeyDown={handleDraftKeyDown}
              placeholder="Paste the email, message, or note you want to rewrite. The rewrite keeps your facts intact."
              rows={16}
              value={draft}
            />
            <div className="mt-3 flex flex-col gap-2 text-xs sm:flex-row sm:items-center sm:justify-between">
              <p
                aria-live="polite"
                className={`font-mono font-semibold ${
                  draftBelowMinimum ? "text-clay" : "text-sage"
                }`}
              >
                {trimmedDraftLength} / {minimumDraftLength} min
              </p>
              {draftBelowMinimum ? (
                <p className="text-ink/50" role="status">
                  Add {minimumDraftLength - trimmedDraftLength} more
                  characters.
                </p>
              ) : null}
            </div>
            {draftRestoreVisible ? (
              <div
                className="mt-3 flex flex-col gap-2 rounded-lg border border-sage/20 bg-mint/50 px-3 py-2 text-sm text-ink/65 sm:flex-row sm:items-center sm:justify-between"
                role="status"
              >
                <span>Restored your draft.</span>
                <Button
                  className="min-h-9 w-full px-3 text-ink/65 hover:bg-white sm:w-auto"
                  onClick={clearSavedDraft}
                  type="button"
                  variant="ghost"
                >
                  Clear saved draft
                </Button>
              </div>
            ) : null}
            {!draft.trim() && !loading && !result && !qualityFailure ? (
              <div className="mt-4 rounded-lg border border-line bg-paper/60 p-4">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-ink">
                      Want a quick feel for it?
                    </p>
                    <p className="mt-1 text-xs leading-5 text-ink/50">
                      This only fills the draft with a sample. Rewrite uses a
                      real attempt once quality checks pass.
                    </p>
                  </div>
                  <Button
                    className="w-full shrink-0 sm:w-auto"
                    onClick={loadExampleDraft}
                    type="button"
                    variant="secondary"
                  >
                    <Sparkles className="h-4 w-4" aria-hidden="true" />
                    Try an example
                  </Button>
                </div>
              </div>
            ) : null}
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
            <p aria-live="polite" className="sr-only">
              {resultAnnouncement}
            </p>
            <div className="mb-4 flex min-h-11 flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <h2
                className="font-mono text-[11px] font-semibold uppercase tracking-[0.18em] text-sage outline-none focus-visible:rounded-lg focus-visible:ring-2 focus-visible:ring-sage/40 focus-visible:ring-offset-4"
                id="rewrite-result-heading"
                ref={resultHeadingRef}
                tabIndex={-1}
              >
                In your voice
              </h2>
              <div className="flex flex-wrap items-center justify-end gap-3">
                {result?.rewrittenText ? (
                  <div
                    aria-label="Switch rewrite result view"
                    className="inline-flex rounded-lg border border-line bg-white p-1"
                    role="group"
                  >
                    <button
                      aria-pressed={!compareOpen}
                      className={`min-h-9 rounded-md px-3 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-[0.12em] transition ${
                        compareOpen
                          ? "text-ink/45 hover:bg-paper hover:text-ink"
                          : "bg-mint text-sage shadow-sm"
                      }`}
                      onClick={() => setCompareOpen(false)}
                      type="button"
                    >
                      View rewrite
                    </button>
                    <button
                      aria-pressed={compareOpen}
                      className={`min-h-9 rounded-md px-3 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-[0.12em] transition ${
                        compareOpen
                          ? "bg-mint text-sage shadow-sm"
                          : "text-ink/45 hover:bg-paper hover:text-ink"
                      }`}
                      onClick={() => setCompareOpen(true)}
                      type="button"
                    >
                      View compare
                    </button>
                  </div>
                ) : null}
                <Button
                  aria-label="Copy rewrite to clipboard"
                  className="px-3 text-ink/70 hover:bg-paper"
                  disabled={!result?.rewrittenText}
                  onClick={() => void copyReply()}
                  title="Copy rewrite to clipboard"
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

            <div
              aria-labelledby="rewrite-result-heading"
              className="flex min-h-[28rem] flex-1 flex-col rounded-lg border border-line/70 bg-mint/20 p-5 md:p-6"
            >
              {showOutOfCreditsNudge ? (
                <OutOfCreditsNudge
                  canRedeem={showRedeemAction}
                  onRedeemClick={openRedeemModal}
                  paid={paid}
                />
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
                  <div className="space-y-2 text-xs leading-5 text-ink/50">
                    <p>Usually 10–60 seconds.</p>
                    {rewriteIsSlow ? (
                      <p className="font-medium text-ink/65" role="status">
                        Still working — longer than usual.
                      </p>
                    ) : null}
                  </div>
                  <p className="text-xs text-ink/50">
                    Keeping your facts intact and checking quality.
                  </p>
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                    <p className="text-xs text-ink/50">
                      Unfinished rewrites are not charged.
                    </p>
                    <Button
                      className="w-full sm:w-auto"
                      onClick={cancelRewrite}
                      type="button"
                      variant="secondary"
                    >
                      <X className="h-4 w-4" aria-hidden="true" />
                      Cancel
                    </Button>
                  </div>
                </div>
              ) : result?.rewrittenText ? (
                compareOpen ? (
                  <div
                    aria-label="Draft and rewritten reply comparison"
                    className="grid gap-4 lg:grid-cols-2"
                  >
                    <div className="min-w-0 rounded-lg border border-line bg-white/75 p-4">
                      <Eyebrow>Original draft</Eyebrow>
                      <div className="mt-3 max-h-[24rem] overflow-y-auto whitespace-pre-wrap text-sm leading-7 text-ink/70">
                        {submittedDraft || draft}
                      </div>
                    </div>
                    <div className="min-w-0 rounded-lg border border-sage/20 bg-white p-4">
                      <Eyebrow tone="accent">Rewritten reply</Eyebrow>
                      <div className="mt-3 max-h-[24rem] overflow-y-auto whitespace-pre-wrap text-sm leading-7 text-ink">
                        {result.rewrittenText}
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="whitespace-pre-wrap text-base leading-8 text-ink">
                    {result.rewrittenText}
                  </div>
                )
              ) : qualityFailure ? (
                <div className="flex flex-1 flex-col items-center justify-center text-center">
                  <Sparkles className="mb-3 h-5 w-5 text-sage" aria-hidden="true" />
                  <p className="font-semibold text-ink">
                    {titleForQualityFailure(qualityFailure.reason)}
                  </p>
                  <p className="mt-2 max-w-md text-sm leading-relaxed text-ink/60">
                    {qualityFailure.error}
                  </p>
                  <p className="mt-4 rounded-lg border border-sage/20 bg-white/80 px-4 py-3 text-sm font-semibold text-sage">
                    {failureCopy.workspace.notCharged}
                  </p>
                  <div className="mt-4 w-full max-w-md rounded-lg border border-line/70 bg-white/70 p-3 text-left">
                    <Button
                      aria-controls="quality-failure-tips"
                      aria-expanded={qualityTipsOpen}
                      className="w-full justify-between px-3 text-ink/75 hover:bg-paper"
                      onClick={() => setQualityTipsOpen((open) => !open)}
                      type="button"
                      variant="ghost"
                    >
                      {failureCopy.workspace.tips.prompt}
                    </Button>
                    {qualityTipsOpen ? (
                      <ul
                        className="mt-3 space-y-2 px-3 pb-1 text-sm leading-6 text-ink/65"
                        id="quality-failure-tips"
                      >
                        <li>{failureCopy.workspace.tips.longerDraft}</li>
                        <li>{failureCopy.workspace.tips.clearerFacts}</li>
                        <li>{failureCopy.workspace.tips.differentTone}</li>
                      </ul>
                    ) : null}
                  </div>
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
                  <div className="min-w-0">
                    <p className="font-semibold text-ink">
                      Keep writing in your own voice.
                    </p>
                    <p className="mt-1 leading-6">
                      Choose the pack that fits your next batch of emails and
                      messages.
                    </p>
                    <div className="mt-3 grid gap-2 sm:grid-cols-3">
                      {workspacePacks.map((pack) => (
                        <div
                          className="rounded-lg border border-line bg-white/70 p-3"
                          key={pack.sku}
                        >
                          <p className="font-semibold text-ink">{pack.name}</p>
                          <p className="mt-1 text-sm text-ink/70">
                            {pack.price}
                          </p>
                          <p className="mt-1 text-xs leading-5 text-ink/50">
                            {pack.allowance} · {pack.term}
                          </p>
                        </div>
                      ))}
                    </div>
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

        {/* AI Signal (naturalness) — focal before/after band */}
        <section className="mt-6 rounded-2xl border border-line bg-white p-5 shadow-soft md:p-6">
          {hasSignal ? (
            <>
              <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                <Eyebrow>AI SIGNAL (NATURALNESS) · BEFORE VS AFTER</Eyebrow>
                {signalDelta !== null ? (
                  <span
                    className={`rounded-lg px-3 py-2 font-mono text-sm font-semibold ${
                      signalDelta > 0
                        ? "bg-mint text-sage"
                        : signalRegressed
                          ? "bg-rust/10 text-rust"
                          : "bg-paper-deep text-ink/60"
                    }`}
                    title={
                      signalRegressed
                        ? signalRegressionTooltip
                        : labelForNaturalness(visibleNaturalness)
                    }
                  >
                    {signalDelta > 0
                      ? `−${signalDelta} pts more natural`
                      : signalRegressed
                        ? `+${Math.abs(signalDelta)} pts`
                        : "Reads natural"}
                    {signalRegressed ? (
                      <span className="sr-only">: {signalRegressionTooltip}</span>
                    ) : null}
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
              Run a rewrite to see the AI Signal (naturalness) before and after.
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
