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

describe("workspace V2 surface copy", () => {
  it("keeps the reply workflow and confirmed context fields", () => {
    expect(workspaceSource).not.toContain("Quick context");
    expect(workspaceSource).not.toContain("scenarioOptions");
    expect(workspaceSource).not.toContain("Blank / custom");
    expect(workspaceSource).not.toContain("Email or message reply");
    expect(workspaceSource).not.toContain("Customer support");
    expect(workspaceSource).not.toContain("Cover letter");
    expect(workspaceSource).not.toContain("Work update");
    expect(workspaceSource).toContain("Context or message");
    expect(workspaceSource).toContain("Draft to rewrite");
    expect(workspaceSource).toContain("Audience");
    expect(workspaceSource).toContain("Purpose");
    expect(workspaceSource).toContain("What actually happened");
    expect(workspaceSource).toContain("Facts to preserve");
    expect(workspaceSource).toContain("factsToPreserve: form.factsToPreserve");
    expect(workspaceSource).toContain("{combinedLength}/{rewriteInputLimits.combined}");
  });

  it("renders tone choices from the reduced preset list", () => {
    expect(workspaceSource).toContain("tonePresetOptions.map");
    expect(workspaceSource).not.toContain("Firm but polite");
    expect(workspaceSource).not.toContain("Apologetic");
  });

  it("has a safe failure state when the signal does not improve", () => {
    expect(workspaceSource).toContain("Writing signal still high");
    expect(workspaceSource).toContain("We could not produce a better version yet");
  });

  it("keeps the workspace shell dense and stable for repeated use", () => {
    expect(workspaceSource).toContain("max-w-6xl");
    expect(workspaceSource).toContain(
      "lg:grid-cols-[minmax(0,1.04fr)_minmax(360px,0.96fr)]",
    );
    expect(workspaceSource).toContain("lg:sticky lg:top-20");
    expect(workspaceSource).toContain("min-h-[24rem]");
    expect(workspaceSource).not.toContain("rounded-xl");
    expect(workspaceSource).not.toContain("rounded-2xl");
  });

  it("keeps quota status and paywall surfaces aligned with the app shell", () => {
    expect(subscriptionStatusSource).toContain("bg-sky");
    expect(subscriptionStatusSource).toContain(
      "md:grid-cols-[minmax(0,1fr)_auto]",
    );
    expect(paywallSource).toContain("max-w-6xl");
    expect(paywallSource).toContain("lg:grid-cols-[minmax(0,1fr)_360px]");
    expect(paywallSource).toContain("Starter");
    expect(paywallSource).toContain("NZ$9.90/month");
    expect(paywallSource).toContain("55 rewrites per month");
    expect(paywallSource).toContain("Exam Week Pass");
    expect(paywallSource).toContain("Top-ups appear when quota runs low");
    expect(paywallSource).not.toContain("rounded-xl");
    expect(paywallSource).not.toContain("rounded-2xl");
    expect(paywallSource).not.toContain("NZD $9/month");
    expect(paywallSource).not.toContain("40 rewrites");
  });

  it("shows the free-tier upgrade nudge only after successful unpaid rewrites", () => {
    const appPageSource = readFileSync(
      new URL("../../app/app/page.tsx", import.meta.url),
      "utf8",
    );

    expect(appPageSource).toContain("remaining={usage.remaining}");
    expect(appPageSource).toContain("quota={usage.quota}");
    expect(workspaceSource).toContain("remaining: number");
    expect(workspaceSource).toContain("quota: number");
    expect(workspaceSource).toContain("freeRewritesRemaining");
    expect(workspaceSource).toContain("You have");
    expect(workspaceSource).toContain("free rewrite(s) left");
    expect(workspaceSource).toContain("That was your last free rewrite");
    expect(workspaceSource).toContain('href="/pricing"');
    expect(workspaceSource).toContain("!paid");
  });
});
