import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

import { failureCopy } from "../../lib/failure-copy";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

function phrase(...parts: string[]) {
  return parts.join(" ");
}

describe("pricing and auth visual system", () => {
  it("keeps the pricing route aligned with the rewrite-packs model and graceful gating", () => {
    const pricingPage = source("app/pricing/page.tsx");

    expect(pricingPage).toContain('<main className="rimv">');
    expect(pricingPage).toContain('className="page"');
    expect(pricingPage).toContain('className="pricing-wrap"');
    expect(pricingPage).toContain(
      "Redeem a trial code. Buy rewrites when you need them.",
    );
    expect(pricingPage).toContain(
      "Trial codes unlock 3 rewrites with no card.",
    );
    expect(pricingPage).toContain("Trial code access");
    expect(pricingPage).toContain("Redeem a trial code");
    expect(pricingPage).toContain("Rewrite packs");
    expect(pricingPage).toContain("Quick Pack");
    expect(pricingPage).toContain("NZ$2.50");
    expect(pricingPage).toContain("10 rewrites");
    expect(pricingPage).toContain("Value Pack");
    expect(pricingPage).toContain("NZ$6.90");
    expect(pricingPage).toContain("30 rewrites");
    expect(pricingPage).toContain("Most popular");
    expect(pricingPage).toContain("Pro/API");
    expect(pricingPage).toContain("NZ$19.90/mo");
    expect(pricingPage).toContain("90 rewrites");
    expect(pricingPage).toContain("Available soon");
    expect(pricingPage).toContain("isPriceConfigured");
    // Old subscription-era copy must be gone.
    expect(pricingPage).not.toContain("Starter");
    expect(pricingPage).not.toContain("Exam Week Pass");
    expect(pricingPage).not.toContain("NZ$9.90");
    expect(pricingPage).not.toContain("55 rewrites");
    expect(pricingPage).not.toContain("110 rewrites");
    expect(pricingPage).not.toContain("unlimited rewrites");
    expect(pricingPage).not.toContain("NZD $9");
    expect(pricingPage).not.toContain("40 rewrites");
    expect(pricingPage).not.toContain(phrase("Start", "free"));
    expect(pricingPage).not.toContain(phrase("Free", "tier"));
    expect(pricingPage).not.toContain(phrase("3", "free"));
  });

  it("keeps auth cards within the refreshed shape and keyboard-focus system", () => {
    const authCard = source("components/auth/google-oauth-card.tsx");

    expect(authCard).toContain('className="rimv"');
    expect(authCard).toContain('className="brand"');
    expect(authCard).toContain('className="btn btn-primary btn-lg"');
    expect(authCard).toContain("var(--card)");
    expect(authCard).toContain("var(--rule)");
    expect(authCard).toContain("boxShadow");
    expect(authCard).toContain("Entra OAuth sign-in");
    expect(authCard).toContain("Continue with email");
    expect(authCard).toContain("Continue with Google");
    expect(authCard).toContain("Continue to sign-in");
    expect(authCard).toContain("Redeem a trial code for 3 rewrites");
    expect(authCard).toContain('import { Eye, EyeOff } from "lucide-react";');
    expect(authCard).toContain('type="tel"');
    expect(authCard).toContain('label="New password"');
    expect(authCard).toContain('label="Confirm password"');
    expect(authCard).toContain("failureCopy.auth.credentials");
    expect(authCard).toContain("failureCopy.auth.signInUnavailable");
    expect(failureCopy.auth.credentials).toBe("Email or password is incorrect.");
    expect(failureCopy.auth.signInUnavailable).toContain("temporarily unavailable");
    expect(failureCopy.auth.signInUnavailable).toContain("try again in a few minutes");
    expect(authCard).toContain("scrollIntoView");
    expect(authCard).toContain(
      'heading="Redeem a trial code after you create your account."',
    );
    expect(authCard).toContain(
      'lead="Create the account in-app, verify your email, then redeem a trial code (or buy a pack) to unlock 3 rewrites."',
    );
    expect(authCard).toContain(
      'title="Create your account for trial-code access"',
    );
    expect(authCard).toContain(
      'body="Verify your email, then redeem a trial code (or buy a pack) to unlock 3 rewrites."',
    );
    expect(authCard).toContain(
      '<ReturnHint action="verifying" destination={authRedirect.redirectTo} />',
    );
    expect(authCard).toContain(
      "After {action} you&apos;ll return to {destination}.",
    );
    expect(authCard).not.toContain(
      "Start with email, a sign-in value, and a quick verification.",
    );
    expect(authCard).not.toContain("sign-in value");
    expect(authCard.indexOf('id="sign-in-entry-hint"')).toBeLessThan(
      authCard.indexOf('hintId="sign-in-entry-hint"'),
    );
    expect(authCard.indexOf('id="sign-up-entry-hint"')).toBeLessThan(
      authCard.indexOf('hintId="sign-up-entry-hint"'),
    );
    expect(authCard.indexOf('id="reset-entry-hint"')).toBeLessThan(
      authCard.indexOf('hintId="reset-entry-hint"'),
    );
    expect(authCard).not.toContain(
      phrase("Start with three", "free", "rewrites"),
    );
  });

  it("keeps landing CTAs focused on trial-code redemption and developer CTA on keys", () => {
    const hero = source("components/landing/hero.tsx");
    const closingCta = source("components/landing/closing-cta.tsx");
    const pricingBlock = source("components/landing/pricing-v2.tsx");
    const footer = source("components/site-footer.tsx");
    const developersPage = source("app/developers/page.tsx");
    const promoSurfaces = [hero, closingCta, pricingBlock, footer].join("\n");

    expect(hero).toContain('{ v: "Trial code", l: "redeem for 3 rewrites" }');
    expect(hero).toContain(
      "Redeem a trial code · Buy rewrites from NZ$2.50 · Pro/API for developers",
    );
    expect(closingCta).toContain(
      "Redeem a trial code · 3 trial rewrites",
    );
    expect(pricingBlock).toContain(
      "Redeem a trial code, then buy rewrites as you need them.",
    );
    expect(pricingBlock).toContain("Trial code access");
    expect(pricingBlock).toContain("≈ NZ$0.25 / rewrite");
    expect(pricingBlock).toContain("≈ NZ$0.23 / rewrite");
    expect(pricingBlock).toContain("≈ NZ$0.22 / rewrite");
    expect(footer).toContain(
      "Redeem trial codes · Rewrite packs from NZ$2.50 · Pro/API NZ$19.90/mo",
    );
    expect(developersPage).toContain('href="/developers/keys"');
    expect(developersPage).toContain("Get your API key");
    expect(developersPage).not.toContain(
      "Trial codes unlock 3 rewrites to try the engine",
    );

    expect(promoSurfaces).not.toContain(phrase("3", "free"));
    expect(promoSurfaces).not.toContain(phrase("Free", "tier"));
    expect(promoSurfaces).not.toContain(phrase("Start", "free"));
    expect(promoSurfaces).not.toContain(phrase("free", "account"));
    expect(promoSurfaces).not.toContain(phrase("free", "rewrites"));
  });

  it("keeps shared button controls visibly focusable", () => {
    const button = source("components/ui/button.tsx");

    expect(button).toContain("focus-visible:outline-none");
    expect(button).toContain("focus-visible:ring-2");
    expect(button).toContain("focus-visible:ring-clay");
  });
});
