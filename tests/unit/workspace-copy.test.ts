import { readFileSync } from "node:fs";
import { Window } from "happy-dom";
import { afterEach, describe, expect, it, vi } from "vitest";

import { failureCopy } from "../../lib/failure-copy";

type Act = typeof import("react").act;
type CreateElement = typeof import("react").createElement;
type CreateRoot = typeof import("react-dom/client").createRoot;

const workspaceSource = readFileSync(
  new URL("../../components/app/rewrite-workspace.tsx", import.meta.url),
  "utf8",
);
const rewriteHistorySource = readFileSync(
  new URL("../../lib/rewrite-history.ts", import.meta.url),
  "utf8",
);
const historyListSource = readFileSync(
  new URL("../../components/app/history-list.tsx", import.meta.url),
  "utf8",
);
const subscriptionStatusSource = readFileSync(
  new URL("../../components/app/subscription-status.tsx", import.meta.url),
  "utf8",
);
const firstRunChecklistSource = readFileSync(
  new URL("../../components/app/first-run-checklist.tsx", import.meta.url),
  "utf8",
);
const quotaPillSource = readFileSync(
  new URL("../../components/app/shell/quota-pill.tsx", import.meta.url),
  "utf8",
);
const appShellSource = readFileSync(
  new URL("../../components/app/shell/app-shell.tsx", import.meta.url),
  "utf8",
);
const pastDueBannerSource = readFileSync(
  new URL("../../components/app/past-due-banner.tsx", import.meta.url),
  "utf8",
);
const paywallSource = readFileSync(
  new URL("../../components/app/paywall-card.tsx", import.meta.url),
  "utf8",
);
const redeemCardSource = readFileSync(
  new URL("../../components/app/redeem-code-card.tsx", import.meta.url),
  "utf8",
);
const buttonSource = readFileSync(
  new URL("../../components/ui/button.tsx", import.meta.url),
  "utf8",
);
const appPageSource = readFileSync(
  new URL("../../app/app/page.tsx", import.meta.url),
  "utf8",
);
const privacySource = readFileSync(
  new URL("../../app/privacy/page.tsx", import.meta.url),
  "utf8",
);
const termsSource = readFileSync(
  new URL("../../app/terms/page.tsx", import.meta.url),
  "utf8",
);

const sharedRetentionSentence =
  "Workspace history may be retained for up to 90 days, while API request and result records use a separate 30-day retention window.";

function phrase(...parts: string[]) {
  return parts.join(" ");
}

function installDom() {
  const testWindow = new Window({ url: "http://localhost/app" });

  vi.stubGlobal("window", testWindow);
  vi.stubGlobal("self", testWindow);
  vi.stubGlobal("document", testWindow.document);
  vi.stubGlobal("navigator", testWindow.navigator);
  vi.stubGlobal("HTMLElement", testWindow.HTMLElement);
  vi.stubGlobal("HTMLTextAreaElement", testWindow.HTMLTextAreaElement);
  vi.stubGlobal("Element", testWindow.Element);
  vi.stubGlobal("Node", testWindow.Node);
  vi.stubGlobal("Text", testWindow.Text);
  vi.stubGlobal("Event", testWindow.Event);
  vi.stubGlobal("MouseEvent", testWindow.MouseEvent);
  vi.stubGlobal("KeyboardEvent", testWindow.KeyboardEvent);
  vi.stubGlobal(
    "requestAnimationFrame",
    testWindow.requestAnimationFrame.bind(testWindow),
  );
  vi.stubGlobal(
    "cancelAnimationFrame",
    testWindow.cancelAnimationFrame.bind(testWindow),
  );

  (
    globalThis as typeof globalThis & {
      IS_REACT_ACT_ENVIRONMENT: boolean;
    }
  ).IS_REACT_ACT_ENVIRONMENT = true;

  return testWindow;
}

async function loadWorkspaceModules() {
  const react = await import("react");
  vi.stubGlobal("React", react);
  vi.doMock("next/navigation", () => ({
    useRouter: () => ({
      refresh: vi.fn(),
    }),
  }));

  const [reactDom, workspace, samples] = await Promise.all([
    import("react-dom/client"),
    import("../../components/app/rewrite-workspace"),
    import("../../components/landing/sample-cases"),
  ]);

  return {
    act: react.act as Act,
    createElement: react.createElement as CreateElement,
    createRoot: reactDom.createRoot as CreateRoot,
    RewriteWorkspace: workspace.RewriteWorkspace,
    homepageSampleCases: samples.homepageSampleCases,
  };
}

const workspaceProps = {
  appExperience: "ok" as const,
  canRedeem: false,
  checkoutStatus: null,
  outOfCredits: false,
  usageLabel: "3 rewrites remaining",
  subscriptionStatus: "Active",
  paymentGraceEndsAt: null,
  paid: false,
  quota: 3,
  planRemaining: 3,
  promoState: {
    hasRedeemed: true,
    trialExpiresAt: null,
    trialRemaining: 3,
  },
  rewriteHistoryUserKey: "test-user",
  remaining: 3,
  usageExhausted: false,
};

function buttonByText(container: HTMLElement, text: string) {
  return Array.from(container.querySelectorAll("button")).find((button) =>
    button.textContent?.includes(text),
  );
}

describe("rewrite workspace surface copy", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.resetModules();
  });

  it("is a single-input workspace without the old wizard scaffolding", () => {
    // The simplified workspace submits only the draft plus a default tone,
    // so the backend RewriteRequest contract is unchanged.
    expect(workspaceSource).toContain("roughDraftReply: draft");
    expect(workspaceSource).toContain('tone: "warm"');

    // The multi-step wizard, tone presets, and extra context fields are gone.
    expect(workspaceSource).not.toContain("workspaceScenarioOptions");
    expect(workspaceSource).not.toContain("tonePresetOptions");
    expect(workspaceSource).not.toContain("messageToReplyTo");
    expect(workspaceSource).not.toContain("factsToPreserve");
    expect(workspaceSource).not.toContain("What message are you replying to?");
    expect(workspaceSource).not.toContain("Add facts that must stay true");
  });

  it("keeps a safe failure state and the reference-signal disclaimer", () => {
    expect(workspaceSource).toContain("titleForQualityFailure");
    expect(workspaceSource).toContain("Still high");
    expect(workspaceSource).toContain("failureCopy.workspace.qualityDefault");
    expect(workspaceSource).toContain("failureCopy.workspace.notCharged");
    expect(workspaceSource).toContain("failureCopy.workspace.tips.prompt");
    expect(workspaceSource).toContain("failureCopy.workspace.tips.longerDraft");
    expect(workspaceSource).toContain("failureCopy.workspace.tips.clearerFacts");
    expect(workspaceSource).toContain("failureCopy.workspace.tips.differentTone");
    expect(workspaceSource).toContain("reference signal");
  });

  it("keeps failure-state copy in one shared module", () => {
    expect(failureCopy.checkout.start).toBe("Could not start checkout.");
    expect(failureCopy.checkout.continue).toBe("Could not continue checkout.");
    expect(failureCopy.checkout.retry).toBe(
      "Use a pack button below to start checkout again.",
    );
    expect(failureCopy.auth.rateLimited).toBe(
      "Too many attempts. Please try again in a few minutes.",
    );
    expect(failureCopy.auth.credentials).toBe(
      "Email or password is incorrect.",
    );
    expect(failureCopy.auth.signInUnavailable).toBe(
      "Sign-in is temporarily unavailable. Please try again in a few minutes.",
    );
    expect(failureCopy.workspace.qualityDefault).toBe(
      "We could not produce a better version yet. Try again or adjust the draft.",
    );
    expect(failureCopy.workspace.notCharged).toContain("not charged");
    expect(failureCopy.workspace.tips.longerDraft).toContain("longer draft");
  });

  it("sets wait expectations, allows cancellation, and announces results accessibly", () => {
    expect(workspaceSource).toContain("rewriteSlowPathDelayMs = 35000");
    expect(workspaceSource).toContain("Usually 10–60 seconds.");
    expect(workspaceSource).toContain("Still working — longer than usual.");
    expect(workspaceSource).toContain("cancelRewrite");
    expect(workspaceSource).toContain("AbortController");
    expect(workspaceSource).toContain("pollRewriteAttempt(attemptId, abortController.signal)");
    expect(workspaceSource).toContain("Unfinished rewrites are not charged.");
    expect(workspaceSource).toContain("resultHeadingRef");
    expect(workspaceSource).toContain("Rewrite complete. Your reply is ready to review and copy.");
    expect(workspaceSource).toContain('aria-live="polite"');
    expect(workspaceSource).toContain('title="Copy rewrite to clipboard"');
    expect(workspaceSource).toContain('aria-label="Copy rewrite to clipboard"');
  });

  it("shows the before/after AI Signal with the shared two-tone meter", () => {
    const aiSignalSource = workspaceSource.slice(
      workspaceSource.indexOf("{/* AI Signal"),
      workspaceSource.indexOf("{error ?"),
    );

    expect(workspaceSource).toContain('from "../landing/nat-bar"');
    expect(workspaceSource).toContain("NatBar");
    expect(workspaceSource).toContain("AI Signal");
    expect(workspaceSource).toContain("draftAiLikePercent");
    expect(workspaceSource).toContain("rewriteAiLikePercent");
    expect(aiSignalSource).toContain(
      "Run a rewrite to see the AI Signal before and after.",
    );
    expect(aiSignalSource).not.toContain("border-dashed");
    expect(aiSignalSource).not.toContain("py-7");
  });

  it("adds a result compare view and explains AI Signal regressions", () => {
    const resultPanelSource = workspaceSource.slice(
      workspaceSource.indexOf('id="rewrite-result-heading"'),
      workspaceSource.indexOf("{/* AI Signal"),
    );

    expect(workspaceSource).toContain("compareOpen");
    expect(workspaceSource).toContain("submittedDraft");
    expect(resultPanelSource).toContain('aria-label="Switch rewrite result view"');
    expect(resultPanelSource).toContain("View rewrite");
    expect(resultPanelSource).toContain("View compare");
    expect(resultPanelSource).toContain("Original draft");
    expect(resultPanelSource).toContain("Rewritten reply");
    expect(workspaceSource).toContain(
      "the rewrite reads more AI-like than your draft",
    );
    expect(workspaceSource).toContain("very short or already-natural drafts");
    expect(workspaceSource).toContain("review before sending");
  });

  it("shows the AI Signal meter in history details", () => {
    expect(historyListSource).toContain('from "../landing/nat-bar"');
    expect(historyListSource).toContain("NatBar");
    expect(historyListSource).toContain("AI Signal · before vs after");
    expect(historyListSource).toContain("Draft {detail.draftSignal}%");
    expect(historyListSource).toContain("Rewrite {detail.rewriteSignal}%");
    expect(historyListSource).toContain("A third-party reference signal");
    expect(historyListSource).toContain("lower reads more natural");
    expect(historyListSource).toContain("review before sending");
  });

  it("offers a local Try an example draft without starting a rewrite", () => {
    const loadExampleSource = workspaceSource.slice(
      workspaceSource.indexOf("function loadExampleDraft"),
      workspaceSource.indexOf("function cancelRewrite"),
    );

    expect(workspaceSource).toContain(
      'import { homepageSampleCases } from "../landing/sample-cases";',
    );
    expect(workspaceSource).toContain("workspaceExampleSample.draft");
    expect(workspaceSource).toContain("Try an example");
    expect(workspaceSource).toContain("This only fills the draft with a sample.");
    expect(workspaceSource).toContain("real attempt once quality checks pass");
    expect(loadExampleSource).toContain("setDraft(workspaceExampleSample.draft)");
    expect(loadExampleSource).not.toContain("fetch(");
    expect(loadExampleSource).not.toContain("submit(");
  });

  it("describes rewrite history and retention accurately", () => {
    expect(rewriteHistorySource).toContain("rimv.rewrite.history.v1");
    expect(rewriteHistorySource).toContain("REWRITE_HISTORY_STORAGE_KEY_PREFIX");
    // History now lives on the server-backed /app/history page, not inside the
    // rewrite workspace (no localStorage list there anymore).
    expect(workspaceSource).not.toContain("readLocalRewriteHistory");
    expect(workspaceSource).not.toContain("writeLocalRewriteHistory");
    expect(workspaceSource).not.toContain("RECENT REWRITES");
    expect(workspaceSource).toContain("By choosing Rewrite");
    expect(workspaceSource).toContain("pasted messages and rewrites");
    expect(workspaceSource).toContain("processed for this request and retained");
    expect(workspaceSource).toContain("up to 90 days");
    expect(workspaceSource).toContain("default. Raw content is then removed");
    expect(workspaceSource).toContain("delete history");
    expect(workspaceSource).toContain("items from your History page");
    expect(privacySource).toContain("up to the configured retention window");
    expect(privacySource).toContain("default 90 days");
    expect(termsSource).toContain("default 90 days");
  });

  it("keeps legal copy aligned with AI Signal, review, and retention naming", () => {
    expect(termsSource).toContain("AI Signal percentages");
    expect(termsSource).toContain("reference signals for comparison");
    expect(termsSource).not.toMatch(/tone check/i);

    expect(privacySource).toContain("automated analysis");
    expect(privacySource).toContain("AI Signal results");
    expect(privacySource).toContain(
      "Human review happens only when you report a specific issue",
    );
    expect(privacySource).toContain("data export requests, or deletion requests");
    expect(privacySource).toContain(sharedRetentionSentence);
  });

  it("keeps the quota strip as real buttons aligned with the rewrite-packs model", () => {
    expect(subscriptionStatusSource).toContain('from "../ui/button"');
    expect(subscriptionStatusSource).toContain("<Button");
    expect(subscriptionStatusSource).not.toContain("<button");
    expect(subscriptionStatusSource).toContain("bg-sky");
    expect(subscriptionStatusSource).toContain("bg-sky/50");
    expect(subscriptionStatusSource).toContain("rounded-2xl");
    expect(subscriptionStatusSource).toContain("Manage billing");
    expect(subscriptionStatusSource).toContain("Buy rewrites");
    expect(subscriptionStatusSource).not.toContain("Upgrade");
    expect(subscriptionStatusSource).toContain("Redeem code");
    expect(paywallSource).toContain("Value Pack");
    expect(paywallSource).toContain("NZ$6.90");
    expect(paywallSource).toContain("30 rewrites");
    expect(paywallSource).toContain("Pro/API");
    expect(paywallSource).toContain('href="/pricing"');
    expect(paywallSource).not.toContain("Starter");
    expect(paywallSource).not.toContain("Exam Week Pass");
    expect(paywallSource).not.toContain("NZD $9/month");
    expect(paywallSource).not.toContain("40 rewrites");
  });

  it("shows a non-blocking PastDue grace banner with billing portal action", () => {
    expect(appPageSource).toContain("paymentGraceEndsAt={account.paymentGraceEndsAt}");
    expect(appPageSource).toContain('account.subscriptionStatus === "PastDue"');
    expect(subscriptionStatusSource).toContain("PastDueBanner");
    expect(pastDueBannerSource).toContain("Payment failed");
    expect(pastDueBannerSource).toContain("update your payment method by");
    expect(pastDueBannerSource).toContain("to keep your plan");
    expect(pastDueBannerSource).toContain("Manage billing");
    expect(pastDueBannerSource).toContain("openBillingPortal");
  });

  it("uses the console workspace design without old layout artifacts", () => {
    // The workspace fills the app shell (no nested page container) and uses
    // the shared shell header typography for a consistent console look.
    expect(workspaceSource).toContain('import shell from "./shell/shell.module.css"');
    expect(workspaceSource).toContain("shell.pageHeaderRow");
    expect(workspaceSource).toContain("shell.pageTitle");
    expect(workspaceSource).not.toContain("max-w-6xl");
    expect(workspaceSource).not.toContain("min-h-screen");
    expect(workspaceSource).toContain("draftAutosaveKey");
    expect(workspaceSource).toContain("rimv.workspace.draft.v1");
    expect(workspaceSource).toContain("handleDraftKeyDown");
    expect(workspaceSource).toContain("event.metaKey");
    expect(workspaceSource).toContain("event.ctrlKey");
    expect(workspaceSource).toContain("min-h-[16rem]");
    expect(workspaceSource).toContain("max-h-[min(28rem,44svh)]");
    expect(workspaceSource).toContain("md:min-h-[28rem]");
    expect(workspaceSource).toContain('import { Textarea } from "../ui/textarea"');
    expect(workspaceSource).toContain("<Textarea");
    expect(workspaceSource).not.toContain("<textarea");
    expect(workspaceSource).toContain("md:grid-cols-2");
    expect(workspaceSource).toContain("md:divide-x md:divide-line");
    expect(workspaceSource).toContain("bg-mint/20");
    expect(workspaceSource).not.toContain("<details");
    expect(workspaceSource).not.toContain("<summary");
    expect(workspaceSource).not.toContain("max-w-5xl");
    expect(workspaceSource).not.toContain("rounded-3xl");
    expect(workspaceSource).not.toContain("rounded-xl");
    expect(workspaceSource).not.toContain("shadow-crisp");
    expect(buttonSource).toContain("rounded-lg");
    expect(buttonSource).toContain("bg-sage");
  });

  it("wires Azure-backed quota and post-copy purchase choices", () => {
    expect(appPageSource).toContain("remaining={usage.remaining}");
    expect(appPageSource).toContain("quota={workspaceQuota}");
    expect(appPageSource).toContain("planRemaining={workspacePlanRemaining}");
    expect(appPageSource).toContain("quotaSources={quotaSources}");
    expect(appPageSource).toContain("trialCreditSummary");
    expect(appPageSource).toContain("trialCredits.granted");
    expect(appPageSource).not.toContain("trialQuota = 3");
    expect(appPageSource).toContain("rewrite credits remaining");
    expect(appPageSource).not.toContain(phrase("free", "rewrites", "remaining"));
    expect(workspaceSource).toContain("quota: number");
    expect(workspaceSource).toContain("planRemaining");
    expect(workspaceSource).toContain("freeRewritesRemaining");
    expect(workspaceSource).toContain("showPostCopyNudge");
    expect(workspaceSource).toContain("Dismiss");
    expect(workspaceSource).toContain("workspacePacks");
    expect(workspaceSource).toContain("pack.name");
    expect(workspaceSource).toContain("pack.price");
    expect(workspaceSource).toContain("pack.allowance");
    expect(workspaceSource).toContain("pack.term");
    expect(workspaceSource).toContain('href="/pricing"');
    expect(workspaceSource).toContain("!paid");
    expect(workspaceSource).toContain("trial rewrite");
    expect(workspaceSource).not.toContain(
      "The Value Pack gives you 30 rewrites",
    );
    expect(workspaceSource).not.toContain(phrase("free", "rewrite"));

    // The upgrade nudge only appears after a copy, never mid-rewrite.
    const submitBody = workspaceSource.slice(
      workspaceSource.indexOf("async function submit"),
      workspaceSource.indexOf("async function copyReply"),
    );
    const copyBody = workspaceSource.slice(
      workspaceSource.indexOf("async function copyReply"),
      workspaceSource.indexOf("function resetWorkspace"),
    );
    expect(submitBody).not.toContain("setShowPostCopyNudge(true)");
    expect(copyBody).toContain("setShowPostCopyNudge(true)");
  });

  it("surfaces low-credit and paid monthly-quota moments in the workspace body", () => {
    expect(workspaceSource).toContain("showLowCreditBanner");
    expect(workspaceSource).toContain("remaining <= 2");
    expect(workspaceSource).toContain("remaining / quota <= 0.15");
    expect(workspaceSource).toContain("LowCreditBanner");
    expect(workspaceSource).toContain("Get more rewrites");
    expect(workspaceSource).toContain("Low rewrite balance");

    const outOfCreditsNudgeSource = workspaceSource.slice(
      workspaceSource.indexOf("function OutOfCreditsNudge"),
      workspaceSource.indexOf("export function RewriteWorkspace"),
    );

    expect(workspaceSource).toContain("openBillingPortal");
    expect(workspaceSource).toContain("Manage billing");
    expect(outOfCreditsNudgeSource).toContain("ManageBillingButton");
    expect(outOfCreditsNudgeSource).toContain("Your monthly limit has been reached.");
    expect(outOfCreditsNudgeSource).toContain("Buy rewrites");
    expect(outOfCreditsNudgeSource).toContain("Redeem code");
  });

  it("routes the quota pill by account type and explains the low state", () => {
    expect(appShellSource).toContain("paid={account.isDeveloperTier}");
    expect(quotaPillSource).toContain("paid");
    expect(quotaPillSource).toContain('paid ? "/app/usage" : "/pricing"');
    expect(quotaPillSource).toContain("lowStateTitle");
    expect(quotaPillSource).toContain("title={low ? lowStateTitle : undefined}");
    expect(quotaPillSource).toContain('aria-label={paid ? "View usage" : "See pricing"}');
  });

  it("always renders the workspace and passes promo state into it", () => {
    expect(appPageSource).toContain("selectAppExperience");
    expect(appPageSource).toContain("account.promo");
    expect(appPageSource).toContain("labelForQuotaSource");
    expect(appPageSource).toContain("promoState={promoState}");
    expect(appPageSource).toContain("outOfCredits={outOfCredits}");
    expect(appPageSource).toContain("canRedeem={canRedeem}");
    expect(appPageSource).toContain("appExperience={appExperience}");
    expect(appPageSource).not.toContain("return null");
    expect(appPageSource).not.toContain("<PaywallCard");
    expect(appPageSource).not.toContain("<RedeemCodeCard");
    expect(appPageSource).not.toContain("ReplyAsHuman2026");
  });

  it("wires the promo redeem modal without exposing the code value", () => {
    const outOfCreditsNudgeSource = workspaceSource.slice(
      workspaceSource.indexOf("function outOfCreditsHint"),
      workspaceSource.indexOf("export function RewriteWorkspace"),
    );

    expect(workspaceSource).toContain("RedeemCodeCard");
    expect(workspaceSource).toContain("redeemModalOpen");
    expect(outOfCreditsNudgeSource).toContain("onRedeemClick");
    expect(outOfCreditsNudgeSource).toContain("<Button");
    expect(outOfCreditsNudgeSource).toContain("<LinkButton");

    expect(redeemCardSource).toContain("NEXT_PUBLIC_TURNSTILE_SITE_KEY");
    expect(redeemCardSource).toContain(
      "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit",
    );
    expect(redeemCardSource).toContain('role="dialog"');
    expect(redeemCardSource).toContain("aria-modal");
    expect(redeemCardSource).toContain("onClose");
    expect(redeemCardSource).toContain('fetch("/api/promo/redeem"');
    expect(redeemCardSource).toContain('fetch("/api/me"');
    expect(redeemCardSource).toContain("router.refresh()");
    expect(redeemCardSource).toContain("3 rewrites unlocked");
    expect(redeemCardSource).toContain("Trial rewrites");
    expect(redeemCardSource).toContain("invalid_code");
    expect(redeemCardSource).toContain("code_expired");
    expect(redeemCardSource).toContain("already_redeemed");
    expect(redeemCardSource).toContain("code_exhausted");
    expect(redeemCardSource).toContain("ip_velocity");
    expect(redeemCardSource).not.toContain("ReplyAsHuman2026");
  });

  it("shows an honest first-run checklist without promising automatic credits", () => {
    expect(workspaceSource).toContain("FirstRunChecklist");
    expect(workspaceSource).toContain("rewriteSucceededThisSession");
    expect(workspaceSource).toContain("setRewriteSucceededThisSession(true)");
    expect(workspaceSource).toContain("rewriteBalance={");
    expect(workspaceSource).toContain("onRedeemClick={openRedeemModal}");

    expect(firstRunChecklistSource).toContain("rimv.firstrun.dismissed.v1");
    expect(firstRunChecklistSource).toContain(
      "redeem a trial code (or buy a pack)",
    );
    expect(firstRunChecklistSource).toContain("Get rewrites");
    expect(firstRunChecklistSource).toContain("First rewrite");
    expect(firstRunChecklistSource).toContain("For developers");
    expect(firstRunChecklistSource).toContain('href="/app/keys"');
    expect(firstRunChecklistSource).toContain('href="/app/connect"');
    expect(firstRunChecklistSource).toContain("readLocalRewriteHistory");
    expect(firstRunChecklistSource).toContain("allRequiredStepsDone");
    expect(firstRunChecklistSource).not.toContain(
      phrase("free", "rewrites"),
    );
  });

  it("restores only the saved draft from localStorage and can clear it", async () => {
    const testWindow = installDom();
    const savedDraft =
      "Please send a warmer version that keeps the Friday pickup details.";
    testWindow.localStorage.setItem("rimv.workspace.draft.v1", savedDraft);
    testWindow.localStorage.setItem("rimv.workspace.unrelated.v1", "kept");
    vi.stubGlobal("fetch", vi.fn());

    const { act, createElement, createRoot, RewriteWorkspace } =
      await loadWorkspaceModules();

    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(RewriteWorkspace, workspaceProps));
    });

    const textarea =
      container.querySelector<HTMLTextAreaElement>("#roughDraftReply");

    expect(textarea?.value).toBe(savedDraft);
    expect(container.textContent).toContain("Restored your draft.");
    expect(testWindow.localStorage.getItem("rimv.workspace.unrelated.v1")).toBe(
      "kept",
    );

    await act(async () => {
      const clearButton = buttonByText(container, "Clear saved draft");
      expect(clearButton).toBeTruthy();
      clearButton?.dispatchEvent(
        new testWindow.MouseEvent("click", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });

    expect(textarea?.value).toBe("");
    expect(testWindow.localStorage.getItem("rimv.workspace.draft.v1")).toBeNull();
    expect(container.textContent).not.toContain("Restored your draft.");

    await act(async () => {
      root.unmount();
    });
  });

  it("submits a valid draft with Cmd+Enter", async () => {
    const testWindow = installDom();
    const rewriteResponse = {
      rewrittenText: "This is the rewritten reply.",
      changeSummary: [],
      riskNotes: [],
      naturalness: {
        draftAiLikePercent: 70,
        rewriteAiLikePercent: 20,
        changePoints: 50,
        label: "lower",
      },
      optimization: {
        internalStrategiesTried: 1,
        userUsageCharged: 1,
      },
    };
    const fetchSpy = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(rewriteResponse), {
        headers: { "Content-Type": "application/json" },
        status: 200,
      }),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const {
      act,
      createElement,
      createRoot,
      RewriteWorkspace,
      homepageSampleCases,
    } = await loadWorkspaceModules();

    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(RewriteWorkspace, workspaceProps));
    });

    await act(async () => {
      const tryExampleButton = buttonByText(container, "Try an example");
      expect(tryExampleButton).toBeTruthy();
      tryExampleButton?.dispatchEvent(
        new testWindow.MouseEvent("click", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });

    const textarea =
      container.querySelector<HTMLTextAreaElement>("#roughDraftReply");
    expect(textarea?.value).toBe(homepageSampleCases[0].draft);

    await act(async () => {
      textarea?.dispatchEvent(
        new testWindow.KeyboardEvent("keydown", {
          bubbles: true,
          cancelable: true,
          key: "Enter",
          metaKey: true,
        }) as unknown as Event,
      );
    });

    expect(fetchSpy).toHaveBeenCalledTimes(1);
    expect(fetchSpy).toHaveBeenCalledWith(
      "/api/rewrite",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({
          roughDraftReply: homepageSampleCases[0].draft,
          tone: "warm",
        }),
      }),
    );

    await act(async () => {
      root.unmount();
    });
  });
});
