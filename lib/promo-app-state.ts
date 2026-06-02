export type PromoAccountState = {
  hasRedeemed: boolean;
  trialExpiresAt: string | null;
  trialRemaining: number;
};

export type AppExperience =
  | "workspace"
  | "redeem"
  | "free-paywall"
  | "paid-paywall";

type SelectAppExperienceInput = {
  paid: boolean;
  promo?: PromoAccountState | null;
  usageExhausted: boolean;
  usageRemaining: number;
};

export function selectAppExperience({
  paid,
  promo,
  usageExhausted,
  usageRemaining,
}: SelectAppExperienceInput): AppExperience {
  if (usageRemaining > 0) {
    return "workspace";
  }

  if (paid && usageExhausted) {
    return "paid-paywall";
  }

  if (promo && !promo.hasRedeemed && usageRemaining === 0) {
    return "redeem";
  }

  if (usageExhausted) {
    return "free-paywall";
  }

  return "workspace";
}

export function labelForQuotaSource(source: string, fallback: string) {
  return source.trim().toUpperCase() === "PROMO" ? "Trial rewrites" : fallback;
}

export function trialExpiryLabel(expiresAt: string | null, now = new Date()) {
  if (!expiresAt) {
    return null;
  }

  const expiry = new Date(expiresAt);
  const expiryTime = expiry.getTime();
  if (Number.isNaN(expiryTime)) {
    return null;
  }

  const msPerDay = 24 * 60 * 60 * 1000;
  const days = Math.max(Math.ceil((expiryTime - now.getTime()) / msPerDay), 0);
  return `expire in ${days} ${days === 1 ? "day" : "days"}`;
}
