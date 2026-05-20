"use client";

import { useState } from "react";
import { useSignIn } from "@clerk/nextjs/legacy";

export function GoogleOAuthCard() {
  const { isLoaded, signIn } = useSignIn();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const continueWithGoogle = async () => {
    if (!signIn || isSubmitting) {
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      await signIn.authenticateWithRedirect({
        strategy: "oauth_google",
        redirectUrl: "/sso-callback",
        redirectUrlComplete: "/app",
        continueSignIn: true,
        continueSignUp: true,
      });
    } catch {
      setError("Google sign-in could not start. Please try again.");
      setIsSubmitting(false);
    }
  };

  return (
    <main className="flex min-h-screen items-center justify-center bg-paper px-6">
      <section className="w-full max-w-md rounded-2xl border border-line bg-white p-8 shadow-sm">
        <div className="space-y-2 text-center">
          <h1 className="text-3xl font-semibold text-ink">Continue to Reply In My Voice</h1>
          <p className="text-sm text-muted">Use your Google account to open your workspace.</p>
        </div>

        {error ? (
          <div className="mt-6 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {error}
          </div>
        ) : null}

        <button
          type="button"
          onClick={continueWithGoogle}
          disabled={!isLoaded || isSubmitting}
          className="mt-8 flex w-full items-center justify-center gap-3 rounded-xl border border-line bg-white px-4 py-3 text-base font-semibold text-ink shadow-sm transition hover:bg-paper disabled:cursor-not-allowed disabled:opacity-60"
        >
          <span aria-hidden="true" className="text-xl">G</span>
          {isSubmitting ? "Opening Google..." : "Continue with Google"}
        </button>

        <div id="clerk-captcha" className="mt-4" />
      </section>
    </main>
  );
}
