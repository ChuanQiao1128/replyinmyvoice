import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const workspaceSource = readFileSync(
  new URL("../../components/app/rewrite-workspace.tsx", import.meta.url),
  "utf8",
);
const rewriteHistorySource = readFileSync(
  new URL("../../lib/rewrite-history.ts", import.meta.url),
  "utf8",
);
const subscriptionStatusSource = readFileSync(
  new URL("../../components/app/subscription-status.tsx", import.meta.url),
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

function phrase(...parts: string[]) {
  return parts.join(" ");
}

describe("rewrite workspace surface copy", () => {
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
    expect(workspaceSource).toContain(
      "We could not produce a better version yet",
    );
    expect(workspaceSource).toContain("reference signal");
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
    expect(workspaceSource).toContain("min-h-[28rem]");
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

  it("wires Azure-backed quota and a post-copy upgrade nudge", () => {
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
    expect(workspaceSource).toContain('href="/pricing"');
    expect(workspaceSource).toContain("!paid");
    expect(workspaceSource).toContain("trial rewrite");
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
    expect(outOfCreditsNudgeSource).toContain("bar above");
    expect(outOfCreditsNudgeSource).not.toContain("onRedeemClick");
    expect(outOfCreditsNudgeSource).not.toContain("onManageBillingClick");
    expect(outOfCreditsNudgeSource).not.toContain("<Button");
    expect(outOfCreditsNudgeSource).not.toContain("<Link");

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
});
