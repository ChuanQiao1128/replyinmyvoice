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
const appPageSource = readFileSync(
  new URL("../../app/app/page.tsx", import.meta.url),
  "utf8",
);

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

  it("keeps local rewrite history without persisting drafts to the database", () => {
    expect(workspaceSource).toContain("rimv.rewrite.history.v1");
    expect(workspaceSource).toContain("Recent rewrites");
    expect(workspaceSource).toContain(
      "Rewrites stay in this browser only and are not saved to the",
    );
  });

  it("keeps the slim quota bar and the paywall aligned with the app shell", () => {
    expect(subscriptionStatusSource).toContain("bg-sky");
    expect(subscriptionStatusSource).toContain("Manage billing");
    expect(subscriptionStatusSource).toContain("Upgrade");
    expect(paywallSource).toContain("Starter");
    expect(paywallSource).toContain("NZ$9.90/month");
    expect(paywallSource).toContain("55 rewrites per month");
    expect(paywallSource).toContain("Exam Week Pass");
    expect(paywallSource).toContain("Top-ups appear when quota runs low");
    expect(paywallSource).not.toContain("NZD $9/month");
    expect(paywallSource).not.toContain("40 rewrites");
  });

  it("wires Azure-backed quota and a post-copy upgrade nudge", () => {
    expect(appPageSource).toContain("remaining={usage.remaining}");
    expect(appPageSource).toContain("quota={usage.quota}");
    expect(appPageSource).toContain("planRemaining={usage.remaining}");
    expect(appPageSource).toContain("quotaSources={[]}");
    expect(workspaceSource).toContain("quota: number");
    expect(workspaceSource).toContain("planRemaining");
    expect(workspaceSource).toContain("freeRewritesRemaining");
    expect(workspaceSource).toContain("showPostCopyNudge");
    expect(workspaceSource).toContain("Dismiss");
    expect(workspaceSource).toContain('href="/pricing"');
    expect(workspaceSource).toContain("!paid");

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
});
