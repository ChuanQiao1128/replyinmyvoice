export type AuthRateLimitPolicy = {
  name: string;
  windowMs: number;
  emailLimit: number;
  ipLimit: number;
};

export type AuthRateLimitResult =
  | { allowed: true }
  | {
      allowed: false;
      retryAfterSeconds: number;
      scope: "email" | "ip";
      status: 429;
    };

type AuthRateLimiterOptions = {
  now?: () => number;
};

type AuthRateLimitCheck = {
  email: string;
  policy: AuthRateLimitPolicy;
  request: Request;
};

type AuthRateLimitBucket = Map<string, number[]>;

const fallbackClientIp = "unknown";
const textEncoder = new TextEncoder();
const oneMinuteMs = 60_000;

export const authRateLimitPolicies = {
  resetResend: {
    emailLimit: 3,
    ipLimit: 10,
    name: "reset-resend",
    windowMs: oneMinuteMs,
  },
  resetStart: {
    emailLimit: 3,
    ipLimit: 10,
    name: "reset-start",
    windowMs: oneMinuteMs,
  },
  signin: {
    emailLimit: 5,
    ipLimit: 20,
    name: "signin",
    windowMs: oneMinuteMs,
  },
  signupResend: {
    emailLimit: 3,
    ipLimit: 10,
    name: "signup-resend",
    windowMs: oneMinuteMs,
  },
  signupStart: {
    emailLimit: 3,
    ipLimit: 10,
    name: "signup-start",
    windowMs: oneMinuteMs,
  },
} satisfies Record<string, AuthRateLimitPolicy>;

export function createAuthRateLimiter(options: AuthRateLimiterOptions = {}) {
  const emailBuckets: AuthRateLimitBucket = new Map();
  const ipBuckets: AuthRateLimitBucket = new Map();

  return {
    async check(input: AuthRateLimitCheck): Promise<AuthRateLimitResult> {
      const now = options.now?.() ?? Date.now();
      const emailKey = `${input.policy.name}:email:${await hashEmail(input.email)}`;
      const ipKey = `${input.policy.name}:ip:${clientIpFromRequest(input.request)}`;
      const emailBucket = bucketFor(emailBuckets, emailKey);
      const ipBucket = bucketFor(ipBuckets, ipKey);

      pruneWindow(emailBucket, now, input.policy.windowMs);
      pruneWindow(ipBucket, now, input.policy.windowMs);

      if (emailBucket.length >= input.policy.emailLimit) {
        return {
          allowed: false,
          retryAfterSeconds: retryAfterSeconds(emailBucket, now, input.policy.windowMs),
          scope: "email",
          status: 429,
        };
      }

      if (ipBucket.length >= input.policy.ipLimit) {
        return {
          allowed: false,
          retryAfterSeconds: retryAfterSeconds(ipBucket, now, input.policy.windowMs),
          scope: "ip",
          status: 429,
        };
      }

      emailBucket.push(now);
      ipBucket.push(now);
      return { allowed: true };
    },
  };
}

const authRateLimiter = createAuthRateLimiter();

export async function checkAuthRateLimit(input: AuthRateLimitCheck) {
  return authRateLimiter.check(input);
}

export function clientIpFromRequest(request: Request) {
  return cloudflareClientIpFromRequest(request) ??
    firstHeaderValue(request.headers.get("x-forwarded-for")) ??
    fallbackClientIp;
}

export function cloudflareClientIpFromRequest(request: Request) {
  return firstHeaderValue(request.headers.get("cf-connecting-ip"));
}

async function hashEmail(email: string) {
  const normalized = email.trim().toLowerCase();
  if (!globalThis.crypto?.subtle) {
    return fallbackHash(normalized);
  }
  const digest = await globalThis.crypto.subtle.digest(
    "SHA-256",
    textEncoder.encode(normalized),
  );
  return Array.from(new Uint8Array(digest))
    .map((byte) => byte.toString(16).padStart(2, "0"))
    .join("");
}

function fallbackHash(value: string) {
  let hash = 0x811c9dc5;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return hash.toString(16).padStart(8, "0");
}

function firstHeaderValue(value: string | null) {
  const firstValue = value?.split(",")[0]?.trim();
  return firstValue || null;
}

function bucketFor(buckets: AuthRateLimitBucket, key: string) {
  const existing = buckets.get(key);
  if (existing) {
    return existing;
  }
  const bucket: number[] = [];
  buckets.set(key, bucket);
  return bucket;
}

function pruneWindow(bucket: number[], now: number, windowMs: number) {
  const cutoff = now - windowMs;
  while (bucket.length > 0 && bucket[0] <= cutoff) {
    bucket.shift();
  }
}

function retryAfterSeconds(bucket: number[], now: number, windowMs: number) {
  const oldest = bucket[0] ?? now;
  return Math.max(1, Math.ceil((oldest + windowMs - now) / 1000));
}
