const checkoutStart = "Could not start checkout.";
const checkoutContinue = "Could not continue checkout.";
const checkoutRetry = "Use a pack button below to start checkout again.";

const authRateLimited = "Too many attempts. Please try again in a few minutes.";
const authCredentials = "Email or password is incorrect.";
const authSignInUnavailable =
  "Sign-in is temporarily unavailable. Please try again in a few minutes.";
const authSignUpUnavailable =
  "Sign-up is temporarily unavailable. Please try again in a few minutes.";
const authPasswordResetUnavailable =
  "Password reset is temporarily unavailable. Please try again in a few minutes.";
const authVerificationUnavailable =
  "Verification is temporarily unavailable. Please try again in a few minutes.";

export const failureCopy = {
  checkout: {
    continue: checkoutContinue,
    retry: checkoutRetry,
    start: checkoutStart,
  },
  auth: {
    browserSignInCancelled: "The browser sign-in was cancelled.",
    browserSignInFailed:
      "The browser sign-in could not be completed. Please try again.",
    credentials: authCredentials,
    passwordResetUnavailable: authPasswordResetUnavailable,
    rateLimited: authRateLimited,
    server: authSignInUnavailable,
    signInUnavailable: authSignInUnavailable,
    signUpUnavailable: authSignUpUnavailable,
    strongerPassword: "Use a stronger password.",
    verificationUnavailable: authVerificationUnavailable,
  },
  workspace: {
    notCharged: "This attempt was not charged.",
    qualityDefault:
      "We could not produce a better version yet. Try again or adjust the draft.",
    tips: {
      clearerFacts: "Use clearer facts, names, dates, and constraints.",
      differentTone:
        "Try a different tone in the draft, such as warmer or more direct.",
      longerDraft: "Add a longer draft with the main point included.",
      prompt: "What can I change?",
    },
  },
} as const;
