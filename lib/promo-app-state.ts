export type PromoAccountState = {
  hasRedeemed: boolean;
  trialExpiresAt: string | null;
  trialRemaining: number;
};

export type AppExperience = "ok" | "needsRedeem" | "needsBuy";

type SelectAppExperienceInput = {
  paid: boolean;
  promo?: PromoAccountState | null;
  usageExhausted: boolean;
  usageRemaining: number;
};

type TrialQuotaSource = {
  source: string;
  label: string;
  remaining?: number | null;
  limit?: number | null;
  quota?: number | null;
  total?: number | null;
};

export function selectAppExperience({
  paid,
  promo,
  usageExhausted,
  usageRemaining,
}: SelectAppExperienceInput): AppExperience {
  if (usageRemaining > 0) {
    return "ok";
  }

  if (paid && usageExhausted) {
    return "needsBuy";
  }

  if (!paid && !promo?.hasRedeemed && usageRemaining === 0) {
    return "needsRedeem";
  }

  if (usageExhausted) {
    return "needsBuy";
  }

  return "ok";
}

export function labelForQuotaSource(source: string, fallback: string) {
  return source.trim().toUpperCase() === "PROMO" ? "Trial rewrites" : fallback;
}

function isTrialQuotaSource(source: TrialQuotaSource) {
  const normalizedSource = source.source.trim().toUpperCase();
  const normalizedLabel = source.label.trim().toLowerCase();
  return (
    normalizedSource === "PROMO" ||
    normalizedLabel.includes("promo") ||
    normalizedLabel.includes("trial")
  );
}

function safeCount(value: number | null | undefined) {
  return typeof value === "number" && Number.isFinite(value)
    ? Math.max(Math.floor(value), 0)
    : null;
}

function sourceGrant(source: TrialQuotaSource) {
  return safeCount(source.limit) ?? safeCount(source.quota) ?? safeCount(source.total);
}

export function trialCreditSummary(
  sources: readonly TrialQuotaSource[] | null | undefined,
  fallbackRemaining: number,
) {
  const fallback = safeCount(fallbackRemaining) ?? 0;
  const trialSources = (sources ?? []).filter(isTrialQuotaSource);

  if (trialSources.length === 0) {
    return { granted: fallback, remaining: fallback };
  }

  const sourceCounts = trialSources.map((source) => {
    const remaining = safeCount(source.remaining);
    return {
      granted: sourceGrant(source) ?? remaining,
      remaining,
    };
  });
  const remainingValues = sourceCounts
    .map((source) => source.remaining)
    .filter((value): value is number => value !== null);
  const remaining =
    remainingValues.length > 0
      ? remainingValues.reduce((sum, value) => sum + value, 0)
      : fallback;

  const grantValues = sourceCounts
    .map((source) => source.granted)
    .filter((value): value is number => value !== null);
  const granted =
    grantValues.length > 0
      ? grantValues.reduce((sum, value) => sum + value, 0)
      : remaining;

  return { granted: Math.max(granted, remaining), remaining };
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
  const exactExpiry = new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(expiry);
  return `expires in ${days} ${days === 1 ? "day" : "days"} (expires ${exactExpiry})`;
}
