import { describe, expect, it } from "vitest";

import { createAuthRateLimiter, type AuthRateLimitPolicy } from "../../lib/auth-rate-limit";

const minuteWindowPolicy: AuthRateLimitPolicy = {
  emailLimit: 2,
  ipLimit: 3,
  name: "unit",
  windowMs: 60_000,
};

function requestFromIp(ip: string) {
  return new Request("https://replyinmyvoice.com/api/auth/signin", {
    headers: {
      "x-forwarded-for": ip,
    },
    method: "POST",
  });
}

describe("auth rate limiter", () => {
  it("returns 429 once the email threshold is exceeded and allows after the window", async () => {
    let now = 1_000;
    const limiter = createAuthRateLimiter({ now: () => now });
    const request = requestFromIp("203.0.113.10");

    await expect(limiter.check({
      email: "Casey@Example.com",
      policy: minuteWindowPolicy,
      request,
    })).resolves.toMatchObject({ allowed: true });
    await expect(limiter.check({
      email: "casey@example.com",
      policy: minuteWindowPolicy,
      request,
    })).resolves.toMatchObject({ allowed: true });

    await expect(limiter.check({
      email: "CASEY@example.com",
      policy: minuteWindowPolicy,
      request,
    })).resolves.toMatchObject({
      allowed: false,
      retryAfterSeconds: 60,
      scope: "email",
      status: 429,
    });

    now += minuteWindowPolicy.windowMs;

    await expect(limiter.check({
      email: "casey@example.com",
      policy: minuteWindowPolicy,
      request,
    })).resolves.toMatchObject({ allowed: true });
  });

  it("returns 429 once the IP threshold is exceeded", async () => {
    let now = 2_000;
    const limiter = createAuthRateLimiter({ now: () => now });
    const request = requestFromIp("198.51.100.25");

    await expect(limiter.check({
      email: "one@example.com",
      policy: minuteWindowPolicy,
      request,
    })).resolves.toMatchObject({ allowed: true });
    await expect(limiter.check({
      email: "two@example.com",
      policy: minuteWindowPolicy,
      request,
    })).resolves.toMatchObject({ allowed: true });
    await expect(limiter.check({
      email: "three@example.com",
      policy: minuteWindowPolicy,
      request,
    })).resolves.toMatchObject({ allowed: true });

    await expect(limiter.check({
      email: "four@example.com",
      policy: minuteWindowPolicy,
      request,
    })).resolves.toMatchObject({
      allowed: false,
      scope: "ip",
      status: 429,
    });

    now += minuteWindowPolicy.windowMs;

    await expect(limiter.check({
      email: "four@example.com",
      policy: minuteWindowPolicy,
      request,
    })).resolves.toMatchObject({ allowed: true });
  });
});
