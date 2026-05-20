"use client";

import { AuthenticateWithRedirectCallback } from "@clerk/nextjs";

export default function SsoCallbackPage() {
  return (
    <>
      <AuthenticateWithRedirectCallback
        continueSignUpUrl="/sign-in"
        signInForceRedirectUrl="/app"
        signUpForceRedirectUrl="/app"
        signInFallbackRedirectUrl="/app"
        signUpFallbackRedirectUrl="/app"
        signInUrl="/sign-in"
        signUpUrl="/sign-in"
        transferable
      />
      <div id="clerk-captcha" />
    </>
  );
}
