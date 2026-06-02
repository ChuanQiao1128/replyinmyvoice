export type AdminPromoStatus =
  | "active"
  | "disabled"
  | "exhausted"
  | "expired"
  | "pending";

export type AdminPromoCode = {
  code: string;
  createdAt: string;
  creditsGranted: number;
  description: string | null;
  displayCode: string | null;
  grantTtlDays: number;
  id: string;
  isActive: boolean;
  kind: string;
  maxRedemptionsGlobal: number | null;
  maxRedemptionsPerUser: number;
  redemptionCount: number;
  status: AdminPromoStatus;
  updatedAt: string;
  validFrom: string;
  validUntil: string;
};

export type AdminPromoDailyRedemptions = {
  date: string;
  redemptions: number;
};

export type AdminPromoIpHashCluster = {
  distinctUsers: number;
  firstRedeemedAt: string;
  ipHash: string;
  lastRedeemedAt: string;
  redemptions: number;
};

export type AdminPromoStats = {
  activationRate: number;
  dailyCurve: AdminPromoDailyRedemptions[];
  distinctUsers: number;
  ipHashClusters: AdminPromoIpHashCluster[];
  totalRedemptions: number;
};

export type AdminPromoDetail = {
  promoCode: AdminPromoCode;
  stats: AdminPromoStats;
};

export type AdminPromoCreateFormValues = {
  code: string;
  credits: string;
  displayCode: string;
  globalCap: string;
  perUserCap: string;
  ttlDays: string;
  validFrom: string;
  validUntil: string;
};

export type AdminPromoCreatePayload = {
  code: string;
  description: string | null;
  creditsGranted: number;
  grantTtlDays: number;
  validFrom: string;
  validUntil: string;
  maxRedemptionsGlobal: number | null;
  maxRedemptionsPerUser: number;
};

export type AdminPromoCreateFieldErrors = Partial<
  Record<keyof AdminPromoCreateFormValues, string>
>;

export type AdminPromoCreateValidationResult =
  | { fieldErrors: AdminPromoCreateFieldErrors; ok: false }
  | { ok: true; payload: AdminPromoCreatePayload };

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function valueFor(record: Record<string, unknown>, camelKey: string, pascalKey: string) {
  return record[camelKey] ?? record[pascalKey];
}

function stringValue(
  record: Record<string, unknown>,
  camelKey: string,
  pascalKey = camelKey[0]?.toUpperCase() + camelKey.slice(1),
) {
  const value = valueFor(record, camelKey, pascalKey);
  return typeof value === "string" ? value : null;
}

function nullableStringValue(record: Record<string, unknown>, camelKey: string) {
  const value = valueFor(record, camelKey, camelKey[0]?.toUpperCase() + camelKey.slice(1));
  return typeof value === "string" ? value : null;
}

function numberValue(record: Record<string, unknown>, camelKey: string) {
  const value = valueFor(record, camelKey, camelKey[0]?.toUpperCase() + camelKey.slice(1));
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function nullableNumberValue(record: Record<string, unknown>, camelKey: string) {
  const value = valueFor(record, camelKey, camelKey[0]?.toUpperCase() + camelKey.slice(1));
  if (value === null || value === undefined) {
    return null;
  }
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function booleanValue(record: Record<string, unknown>, camelKey: string) {
  const value = valueFor(record, camelKey, camelKey[0]?.toUpperCase() + camelKey.slice(1));
  return typeof value === "boolean" ? value : null;
}

export function normalizePromoCode(rawCode: string) {
  if (!rawCode.trim()) {
    return null;
  }

  let normalized = "";
  for (const character of rawCode.trim()) {
    if (character === "-" || /\s/.test(character)) {
      continue;
    }

    const upper = character.toUpperCase();
    if (!/^[A-Z0-9]$/.test(upper)) {
      return null;
    }

    normalized += upper;
    if (normalized.length > 40) {
      return null;
    }
  }

  return normalized.length > 0 ? normalized : null;
}

function promoCodeFromPayload(payload: unknown): AdminPromoCode | null {
  if (!isRecord(payload)) {
    return null;
  }

  const id = stringValue(payload, "id");
  const code = stringValue(payload, "code");
  const kind = stringValue(payload, "kind") ?? "TrialCredits";
  const creditsGranted = numberValue(payload, "creditsGranted");
  const grantTtlDays = numberValue(payload, "grantTtlDays");
  const validFrom = stringValue(payload, "validFrom");
  const validUntil = stringValue(payload, "validUntil");
  const maxRedemptionsPerUser = numberValue(payload, "maxRedemptionsPerUser");
  const redemptionCount = numberValue(payload, "redemptionCount");
  const isActive = booleanValue(payload, "isActive");
  const createdAt = stringValue(payload, "createdAt");
  const updatedAt = stringValue(payload, "updatedAt");

  if (
    !id ||
    !code ||
    creditsGranted === null ||
    grantTtlDays === null ||
    !validFrom ||
    !validUntil ||
    maxRedemptionsPerUser === null ||
    redemptionCount === null ||
    isActive === null ||
    !createdAt ||
    !updatedAt
  ) {
    return null;
  }

  const codeResponse = {
    code,
    createdAt,
    creditsGranted,
    description: nullableStringValue(payload, "description"),
    displayCode: nullableStringValue(payload, "displayCode"),
    grantTtlDays,
    id,
    isActive,
    kind,
    maxRedemptionsGlobal: nullableNumberValue(payload, "maxRedemptionsGlobal"),
    maxRedemptionsPerUser,
    redemptionCount,
    status: "active" as AdminPromoStatus,
    updatedAt,
    validFrom,
    validUntil,
  };

  return {
    ...codeResponse,
    status: derivePromoCodeStatus(codeResponse),
  };
}

export function adminPromoCodesFromPayload(payload: unknown) {
  if (!isRecord(payload)) {
    return [];
  }

  const rawCodes = valueFor(payload, "promoCodes", "PromoCodes");
  if (!Array.isArray(rawCodes)) {
    return [];
  }

  return rawCodes
    .map((candidate) => promoCodeFromPayload(candidate))
    .filter((candidate): candidate is AdminPromoCode => candidate !== null);
}

function dailyCurveFromPayload(payload: unknown) {
  if (!Array.isArray(payload)) {
    return [];
  }

  return payload.flatMap((candidate): AdminPromoDailyRedemptions[] => {
    if (!isRecord(candidate)) {
      return [];
    }

    const date = stringValue(candidate, "date");
    const redemptions = numberValue(candidate, "redemptions");
    return date && redemptions !== null ? [{ date, redemptions }] : [];
  });
}

function ipHashClustersFromPayload(payload: unknown) {
  if (!Array.isArray(payload)) {
    return [];
  }

  return payload.flatMap((candidate): AdminPromoIpHashCluster[] => {
    if (!isRecord(candidate)) {
      return [];
    }

    const ipHash = stringValue(candidate, "ipHash");
    const redemptions = numberValue(candidate, "redemptions");
    const distinctUsers = numberValue(candidate, "distinctUsers");
    const firstRedeemedAt = stringValue(candidate, "firstRedeemedAt");
    const lastRedeemedAt = stringValue(candidate, "lastRedeemedAt");
    if (
      !ipHash ||
      redemptions === null ||
      distinctUsers === null ||
      !firstRedeemedAt ||
      !lastRedeemedAt
    ) {
      return [];
    }

    return [{
      distinctUsers,
      firstRedeemedAt,
      ipHash,
      lastRedeemedAt,
      redemptions,
    }];
  });
}

function statsFromPayload(payload: unknown): AdminPromoStats | null {
  if (!isRecord(payload)) {
    return null;
  }

  const totalRedemptions = numberValue(payload, "totalRedemptions");
  const distinctUsers = numberValue(payload, "distinctUsers");
  const activationRate = numberValue(payload, "activationRate");
  if (
    totalRedemptions === null ||
    distinctUsers === null ||
    activationRate === null
  ) {
    return null;
  }

  return {
    activationRate,
    dailyCurve: dailyCurveFromPayload(valueFor(payload, "dailyCurve", "DailyCurve")),
    distinctUsers,
    ipHashClusters: ipHashClustersFromPayload(
      valueFor(payload, "ipHashClusters", "IpHashClusters"),
    ),
    totalRedemptions,
  };
}

export function adminPromoDetailFromPayload(payload: unknown): AdminPromoDetail | null {
  if (!isRecord(payload)) {
    return null;
  }

  const promoCode = promoCodeFromPayload(valueFor(payload, "promoCode", "PromoCode"));
  const stats = statsFromPayload(valueFor(payload, "stats", "Stats"));
  return promoCode && stats ? { promoCode, stats } : null;
}

export function derivePromoCodeStatus(
  code: Pick<
    AdminPromoCode,
    | "isActive"
    | "maxRedemptionsGlobal"
    | "redemptionCount"
    | "validFrom"
    | "validUntil"
  >,
  now = new Date(),
): AdminPromoStatus {
  if (!code.isActive) {
    return "disabled";
  }

  const validFrom = new Date(code.validFrom);
  if (!Number.isNaN(validFrom.getTime()) && now < validFrom) {
    return "pending";
  }

  const validUntil = new Date(code.validUntil);
  if (!Number.isNaN(validUntil.getTime()) && now > validUntil) {
    return "expired";
  }

  if (
    code.maxRedemptionsGlobal !== null &&
    code.redemptionCount >= code.maxRedemptionsGlobal
  ) {
    return "exhausted";
  }

  return "active";
}

function positiveInteger(value: string, label: string) {
  if (!value.trim() || !/^-?\d+$/.test(value.trim())) {
    return { error: `${label} must be a whole number.`, value: null };
  }

  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed)) {
    return { error: `${label} is too large.`, value: null };
  }

  if (parsed <= 0) {
    return { error: `${label} must be greater than zero.`, value: parsed };
  }

  return { error: null, value: parsed };
}

function dateFromLocalInput(value: string) {
  if (!value.trim()) {
    return null;
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function validatePromoCreateForm(
  values: AdminPromoCreateFormValues,
): AdminPromoCreateValidationResult {
  const fieldErrors: AdminPromoCreateFieldErrors = {};
  const normalizedCode = normalizePromoCode(values.code);
  const normalizedDisplayCode = values.displayCode.trim()
    ? normalizePromoCode(values.displayCode)
    : normalizedCode;

  if (!normalizedCode) {
    fieldErrors.code = "Use letters and numbers only, up to 40 characters.";
  }

  if (!normalizedDisplayCode) {
    fieldErrors.displayCode = "Display code can use letters, numbers, spaces, or hyphens.";
  }

  if (
    normalizedCode &&
    normalizedDisplayCode &&
    normalizedCode !== normalizedDisplayCode
  ) {
    fieldErrors.displayCode = "Display code must match the normalized code.";
  }

  const credits = positiveInteger(values.credits, "Credits");
  if (credits.error) {
    fieldErrors.credits = credits.error;
  }

  const ttlDays = positiveInteger(values.ttlDays, "TTL days");
  if (ttlDays.error) {
    fieldErrors.ttlDays = ttlDays.error;
  }

  const globalCap = values.globalCap.trim()
    ? positiveInteger(values.globalCap, "Global cap")
    : { error: null, value: null };
  if (globalCap.error) {
    fieldErrors.globalCap = globalCap.error;
  }

  const perUserCap = positiveInteger(values.perUserCap, "Per-user cap");
  if (perUserCap.error) {
    fieldErrors.perUserCap = perUserCap.value !== null && perUserCap.value <= 0
      ? "Per-user cap must be at least one."
      : perUserCap.error;
  } else if (perUserCap.value !== null && perUserCap.value < 1) {
    fieldErrors.perUserCap = "Per-user cap must be at least one.";
  }

  const validFrom = dateFromLocalInput(values.validFrom);
  const validUntil = dateFromLocalInput(values.validUntil);
  if (!validFrom) {
    fieldErrors.validFrom = "Valid from is required.";
  }
  if (!validUntil) {
    fieldErrors.validUntil = "Valid until is required.";
  }
  if (validFrom && validUntil && validUntil <= validFrom) {
    fieldErrors.validUntil = "Valid until must be after valid from.";
  }

  if (Object.keys(fieldErrors).length > 0) {
    return { fieldErrors, ok: false };
  }

  return {
    ok: true,
    payload: {
      code: values.displayCode.trim() || values.code.trim(),
      description: null,
      creditsGranted: credits.value ?? 0,
      grantTtlDays: ttlDays.value ?? 0,
      maxRedemptionsGlobal: globalCap.value,
      maxRedemptionsPerUser: perUserCap.value ?? 1,
      validFrom: validFrom?.toISOString() ?? "",
      validUntil: validUntil?.toISOString() ?? "",
    },
  };
}

function payloadMessage(payload: unknown) {
  if (!isRecord(payload)) {
    return null;
  }

  return stringValue(payload, "detail") ??
    stringValue(payload, "error") ??
    stringValue(payload, "message") ??
    stringValue(payload, "title");
}

export function fieldErrorsFromAdminError(status: number, payload: unknown) {
  const message = payloadMessage(payload) ?? "Could not create this promo code.";
  if (
    status === 400 &&
    /duplicate promo code|already exists/i.test(JSON.stringify(payload))
  ) {
    return { code: message } satisfies AdminPromoCreateFieldErrors;
  }

  if (/credits granted/i.test(message)) {
    return { credits: message } satisfies AdminPromoCreateFieldErrors;
  }

  if (/grant ttl/i.test(message)) {
    return { ttlDays: message } satisfies AdminPromoCreateFieldErrors;
  }

  if (/max redemptions global/i.test(message)) {
    return { globalCap: message } satisfies AdminPromoCreateFieldErrors;
  }

  if (/max redemptions per user/i.test(message)) {
    return { perUserCap: message } satisfies AdminPromoCreateFieldErrors;
  }

  if (/valid until/i.test(message) || /valid from/i.test(message)) {
    return { validUntil: message } satisfies AdminPromoCreateFieldErrors;
  }

  return {};
}
