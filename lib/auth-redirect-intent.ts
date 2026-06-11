export const defaultAuthRedirectTo = "/app";

export const knownAuthRedirectSkus = [
  "quick_pack",
  "value_pack",
  "pro_api",
  "focus_pack",
] as const;

export type AuthRedirectSku = (typeof knownAuthRedirectSkus)[number];
export type AuthRedirectIntent = "buy";

export type AuthRedirectInput = {
  redirectTo?: unknown;
  intent?: unknown;
  sku?: unknown;
};

export type AuthRedirectParams = {
  redirectTo: string;
  intent?: AuthRedirectIntent;
  sku?: AuthRedirectSku;
};

const authRedirectBase = "https://replyinmyvoice.local";

export function safeRedirectTo(value: unknown) {
  const url = parseAllowedRedirectUrl(value);
  if (!url) {
    return defaultAuthRedirectTo;
  }

  url.searchParams.delete("intent");
  url.searchParams.delete("sku");
  return pathFromUrl(url);
}

export function normalizeAuthRedirectParams(
  input: AuthRedirectInput = {},
): AuthRedirectParams {
  const redirectUrl = parseAllowedRedirectUrl(input.redirectTo);
  const redirectIntent = redirectUrl?.searchParams.get("intent");
  const redirectSku = redirectUrl?.searchParams.get("sku");
  const intent = safeAuthRedirectIntent(input.intent) ?? safeAuthRedirectIntent(redirectIntent);
  const sku = safeAuthRedirectSku(input.sku) ?? safeAuthRedirectSku(redirectSku);
  const normalized: AuthRedirectParams = {
    redirectTo: safeRedirectTo(input.redirectTo),
  };

  if (intent) {
    normalized.intent = intent;
  }
  if (sku) {
    normalized.sku = sku;
  }

  return normalized;
}

export function buildPostAuthRedirectPath(input: AuthRedirectInput = {}) {
  const normalized = normalizeAuthRedirectParams(input);
  const url = new URL(normalized.redirectTo, authRedirectBase);
  url.searchParams.delete("intent");
  url.searchParams.delete("sku");

  if (normalized.intent) {
    url.searchParams.set("intent", normalized.intent);
  }
  if (normalized.sku) {
    url.searchParams.set("sku", normalized.sku);
  }

  return pathFromUrl(url);
}

export function buildAuthRedirectSearchParams(
  input: AuthRedirectInput = {},
  base?: URLSearchParams | Record<string, string>,
  options: { includeRedirectTo?: boolean } = {},
) {
  const params = base instanceof URLSearchParams
    ? new URLSearchParams(base)
    : new URLSearchParams(base);
  const normalized = normalizeAuthRedirectParams(input);

  if (options.includeRedirectTo ?? true) {
    params.set("redirectTo", normalized.redirectTo);
  } else {
    params.delete("redirectTo");
  }
  if (normalized.intent) {
    params.set("intent", normalized.intent);
  } else {
    params.delete("intent");
  }
  if (normalized.sku) {
    params.set("sku", normalized.sku);
  } else {
    params.delete("sku");
  }

  return params;
}

export function hasAuthRedirectInput(input: AuthRedirectInput = {}) {
  return (
    typeof input.redirectTo === "string" ||
    typeof input.intent === "string" ||
    typeof input.sku === "string"
  );
}

export function safeAuthRedirectIntent(value: unknown): AuthRedirectIntent | undefined {
  return value === "buy" ? value : undefined;
}

export function safeAuthRedirectSku(value: unknown): AuthRedirectSku | undefined {
  return typeof value === "string" && isKnownAuthRedirectSku(value)
    ? value
    : undefined;
}

export function isKnownAuthRedirectSku(value: string): value is AuthRedirectSku {
  return (knownAuthRedirectSkus as readonly string[]).includes(value);
}

function parseAllowedRedirectUrl(value: unknown) {
  if (typeof value !== "string") {
    return null;
  }

  const trimmed = value.trim();
  if (
    !trimmed.startsWith("/") ||
    trimmed.startsWith("//") ||
    trimmed.includes("\\") ||
    containsDotDot(trimmed)
  ) {
    return null;
  }

  try {
    const url = new URL(trimmed, authRedirectBase);
    if (url.origin !== authRedirectBase) {
      return null;
    }

    const pathname = url.pathname;
    if (
      pathname === "/app" ||
      pathname.startsWith("/app/") ||
      pathname === "/pricing"
    ) {
      return url;
    }
  } catch {
    return null;
  }

  return null;
}

function containsDotDot(value: string) {
  if (value.includes("..")) {
    return true;
  }

  try {
    return decodeURIComponent(value).includes("..");
  } catch {
    return true;
  }
}

function pathFromUrl(url: URL) {
  return `${url.pathname}${url.search}${url.hash}`;
}
