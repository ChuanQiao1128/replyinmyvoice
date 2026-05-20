export type StripeEventStatus = "processing" | "processed" | "failed";

export type StripeEventState = {
  status: string;
} | null;

export function shouldProcessStripeEvent(state: StripeEventState) {
  return state === null || state.status === "failed";
}

export function safeStripeEventError(error: unknown) {
  if (!(error instanceof Error)) {
    return "Unknown Stripe webhook error";
  }

  return error.message
    .replace(/sk_(test|live)_[A-Za-z0-9_]+/g, "sk_[redacted]")
    .replace(/whsec_[A-Za-z0-9_]+/g, "whsec_[redacted]")
    .slice(0, 1000);
}

export function buildStripeEventUpdate({
  error,
  now,
  status,
}: {
  status: "processed" | "failed";
  error?: unknown;
  now: Date;
}) {
  if (status === "processed") {
    return {
      status,
      processedAt: now,
      failedAt: null,
      lastError: null,
    };
  }

  return {
    status,
    processedAt: null,
    failedAt: now,
    lastError: safeStripeEventError(error),
  };
}
