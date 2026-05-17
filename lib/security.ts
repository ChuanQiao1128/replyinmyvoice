type OriginOptions = {
  appUrl?: string;
  nodeEnv?: string;
  requestUrl?: string;
};

function toOrigin(value?: string | null): string | null {
  if (!value) {
    return null;
  }

  try {
    return new URL(value).origin;
  } catch {
    return null;
  }
}

function isLocalhostOrigin(origin: string): boolean {
  const parsed = toOrigin(origin);
  if (!parsed) {
    return false;
  }

  const { hostname } = new URL(parsed);
  return hostname === "localhost" || hostname === "127.0.0.1" || hostname === "::1";
}

export function isAllowedOrigin(
  origin: string | null,
  options: OriginOptions = {},
): boolean {
  const nodeEnv = options.nodeEnv ?? process.env.NODE_ENV ?? "development";
  const appOrigin = toOrigin(options.appUrl ?? process.env.NEXT_PUBLIC_APP_URL);
  const currentOrigin = toOrigin(options.requestUrl);

  if (!origin) {
    return nodeEnv !== "production";
  }

  const requestOrigin = toOrigin(origin);
  if (!requestOrigin) {
    return false;
  }

  if (appOrigin && requestOrigin === appOrigin) {
    return true;
  }

  if (currentOrigin && requestOrigin === currentOrigin) {
    return true;
  }

  if (nodeEnv !== "production" && isLocalhostOrigin(requestOrigin)) {
    return true;
  }

  return false;
}

export function hasAllowedOrigin(request: Request): boolean {
  return isAllowedOrigin(request.headers.get("origin"), {
    requestUrl: request.url,
  });
}
