import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const workspaceSource = readFileSync(
  new URL("../../components/app/rewrite-workspace.tsx", import.meta.url),
  "utf8",
);
const subscriptionStatusSource = readFileSync(
  new URL("../../components/app/subscription-status.tsx", import.meta.url),
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
    expect(workspaceSource).toContain('from "../landing/nat-bar"');
    expect(workspaceSource).toContain("NatBar");
    expect(workspaceSource).toContain("AI Signal");
    expect(workspaceSource).toContain("draftAiLikePercent");
    expect(workspaceSource).toContain("rewriteAiLikePercent");
  });

  it("describes rewrite history and retention accurately", () => {
    expect(workspaceSource).toContain("rimv.rewrite.history.v1");
    expect(workspaceSource).toContain("Recent rewrites");
    expect(workspaceSource).toContain("By choosing Rewrite");
    expect(workspaceSource).toContain("pasted messages and rewrites");
    expect(workspaceSource).toContain("processed for this request and retained");
    expect(workspaceSource).toContain("up to 90 days");
    expect(workspaceSource).toContain("default. Raw content is then removed");
    expect(workspaceSource).toContain("delete history");
    expect(workspaceSource).toContain("items from the workspace");
    expect(privacySource).toContain("up to the configured retention window");
    expect(privacySource).toContain("default 90 days");
    expect(termsSource).toContain("default 90 days");
  });

  it("keeps the slim quota bar and the paywall aligned with the rewrite-packs model", () => {
    expect(subscriptionStatusSource).toContain("bg-sky");
    expect(subscriptionStatusSource).toContain("Manage billing");
    expect(subscriptionStatusSource).toContain("Upgrade");
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

  it("wires Azure-backed quota and a post-copy upgrade nudge", () => {
    expect(appPageSource).toContain("remaining={usage.remaining}");
    expect(appPageSource).toContain("quota={workspaceQuota}");
    expect(appPageSource).toContain("planRemaining={workspacePlanRemaining}");
    expect(appPageSource).toContain("quotaSources={quotaSources}");
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
      workspaceSource.indexOf("function clearHistory"),
    );
    expect(submitBody).not.toContain("setShowPostCopyNudge(true)");
    expect(copyBody).toContain("setShowPostCopyNudge(true)");
  });

  it("wires the promo redeem card without exposing the code value", () => {
    expect(appPageSource).toContain("selectAppExperience");
    expect(appPageSource).toContain("RedeemCodeCard");
    expect(appPageSource).toContain("account.promo");
    expect(appPageSource).toContain("labelForQuotaSource");
    expect(appPageSource).not.toContain("ReplyAsHuman2026");

    expect(redeemCardSource).toContain("NEXT_PUBLIC_TURNSTILE_SITE_KEY");
    expect(redeemCardSource).toContain(
      "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit",
    );
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
