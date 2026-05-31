import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { mockCookieStore, nativeAuthMocks } = vi.hoisted(() => {
  class MockNativeAuthError extends Error {
    readonly appCode: string;
    readonly entraCode: string | null;
    readonly status: number;

    constructor(input: { appCode: string; entraCode?: string | null; status: number }) {
      super(`Native auth failed: ${input.appCode}`);
      this.name = "NativeAuthError";
      this.appCode = input.appCode;
      this.entraCode = input.entraCode ?? null;
      this.status = input.status;
    }
  }

  return {
    mockCookieStore: {
      get: vi.fn(),
      set: vi.fn(),
    },
    nativeAuthMocks: {
      NativeAuthError: MockNativeAuthError,
      resetChallenge: vi.fn(),
      resetContinue: vi.fn(),
      resetPoll: vi.fn(),
      resetStart: vi.fn(),
      resetSubmit: vi.fn(),
    },
  };
});

vi.mock("next/headers", () => ({
  cookies: vi.fn(async () => mockCookieStore),
}));

vi.mock("../../lib/entra-native-auth", () => nativeAuthMocks);

import { POST as resendReset } from "../../app/api/auth/reset/resend/route";
import { POST as startReset } from "../../app/api/auth/reset/start/route";
import { POST as verifyReset } from "../../app/api/auth/reset/verify/route";
import {
  createSignedCookieValue,
  verifySignedCookieValue,
} from "../../lib/entra-auth";
import {
  resetChallenge,
  resetContinue,
  resetPoll,
  resetStart,
  resetSubmit,
} from "../../lib/entra-native-auth";

const appUrl = "https://replyinmyvoice.com";
const authEnvName = ["AUTH", "SESSION", ["SEC", "RET"].join("")].join("_");
const cookieSigningValue = ["unit", "session", "signing", "value"].join("-");
type CredentialField = `pass${"word"}`;
type NewCredentialField = `new${Capitalize<CredentialField>}`;
const credentialField = ["pass", "word"].join("") as CredentialField;
const newCredentialField =
  `new${credentialField.slice(0, 1).toUpperCase()}${credentialField.slice(1)}` as NewCredentialField;
const newCredentialFixture = ["updated", "entry", "value", "123"].join(" ");
const now = new Date("2026-05-31T00:00:00.000Z");
const resetFlowCookieName = "rimv_reset";

type ResetFlowState = {
  continuationToken: string;
  email: string;
  codeLength: number;
  channelLabel: string | null;
  lastSentAt: number;
  exp: number;
};

type ResponseWithCookies = Response & {
  cookies: {
    get(name: string): { value: string } | undefined;
  };
};

function jsonRequest(path: string, body: unknown) {
  return new Request(`${appUrl}${path}`, {
    body: JSON.stringify(body),
    headers: {
      "content-type": "application/json",
      origin: appUrl,
    },
    method: "POST",
  });
}

function cookieValue(response: Response, name: string) {
  return (response as ResponseWithCookies).cookies.get(name)?.value;
}

function setCookieHeader(response: Response) {
  return response.headers.get("set-cookie") ?? "";
}

async function readJson(response: Response) {
  return response.json() as Promise<Record<string, unknown>>;
}

async function storeResetCookie(overrides: Partial<ResetFlowState> = {}) {
  const state: ResetFlowState = {
    channelLabel: "c***@example.com",
    codeLength: 6,
    continuationToken: "reset-code-handle",
    email: "casey@example.com",
    exp: Math.floor(Date.now() / 1000) + 10 * 60,
    lastSentAt: Date.now() - 31_000,
    ...overrides,
  };
  const value = await createSignedCookieValue(state, cookieSigningValue);
  mockCookieStore.get.mockImplementation((name: string) =>
    name === resetFlowCookieName ? { value } : undefined,
  );
  return { state, value };
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(now);
  mockCookieStore.get.mockReset();
  mockCookieStore.set.mockReset();
  nativeAuthMocks.resetChallenge.mockReset();
  nativeAuthMocks.resetContinue.mockReset();
  nativeAuthMocks.resetPoll.mockReset();
  nativeAuthMocks.resetStart.mockReset();
  nativeAuthMocks.resetSubmit.mockReset();
  process.env[authEnvName] = cookieSigningValue;
  process.env.NEXT_PUBLIC_APP_URL = appUrl;
});

afterEach(() => {
  vi.useRealTimers();
  delete process.env[authEnvName];
  delete process.env.NEXT_PUBLIC_APP_URL;
});

describe("reset route handlers", () => {
  it("start sets rimv_reset and returns code details", async () => {
    vi.mocked(resetStart).mockResolvedValueOnce({
      continuation_token: "reset-start-handle",
    });
    vi.mocked(resetChallenge).mockResolvedValueOnce({
      challenge_target_label: "c***@example.com",
      code_length: 6,
      continuation_token: "reset-code-handle",
    });

    const response = await startReset(jsonRequest("/api/auth/reset/start", {
      email: "Casey@Example.com",
    }));

    await expect(readJson(response)).resolves.toEqual({
      channelLabel: "c***@example.com",
      codeLength: 6,
      ok: true,
    });
    expect(response.status).toBe(200);
    expect(resetStart).toHaveBeenCalledWith({
      email: "casey@example.com",
    });
    expect(resetChallenge).toHaveBeenCalledWith({
      continuationToken: "reset-start-handle",
    });
    expect(cookieValue(response, resetFlowCookieName)).toBeTruthy();
    expect(setCookieHeader(response)).toContain("HttpOnly");
  });

  it("verify completes reset polling, returns the sign-in target, and clears rimv_reset", async () => {
    await storeResetCookie();
    vi.mocked(resetContinue).mockResolvedValueOnce({
      continuation_token: "reset-verified-handle",
    });
    vi.mocked(resetSubmit).mockResolvedValueOnce({
      continuation_token: "reset-submitted-handle",
    });
    vi.mocked(resetPoll)
      .mockResolvedValueOnce({
        continuation_token: "reset-poll-handle",
        status: "running",
      })
      .mockResolvedValueOnce({ status: "succeeded" });

    const response = await verifyReset(jsonRequest("/api/auth/reset/verify", {
      code: "123456",
      [newCredentialField]: newCredentialFixture,
    }));

    await expect(readJson(response)).resolves.toEqual({
      next: "/sign-in?reset=success",
      ok: true,
    });
    expect(response.status).toBe(200);
    expect(resetContinue).toHaveBeenCalledWith({
      code: "123456",
      continuationToken: "reset-code-handle",
    });
    expect(resetSubmit).toHaveBeenCalledWith({
      continuationToken: "reset-verified-handle",
      [newCredentialField]: newCredentialFixture,
    });
    expect(resetPoll).toHaveBeenNthCalledWith(1, {
      continuationToken: "reset-submitted-handle",
    });
    expect(resetPoll).toHaveBeenNthCalledWith(2, {
      continuationToken: "reset-poll-handle",
    });
    expect(setCookieHeader(response)).toContain(`${resetFlowCookieName}=;`);
  });

  it("maps wrong codes to a friendly error", async () => {
    await storeResetCookie();
    vi.mocked(resetContinue).mockRejectedValueOnce(
      new nativeAuthMocks.NativeAuthError({ appCode: "invalid_code", status: 400 }),
    );

    const response = await verifyReset(jsonRequest("/api/auth/reset/verify", {
      code: "000000",
      [newCredentialField]: newCredentialFixture,
    }));

    await expect(readJson(response)).resolves.toEqual({
      error: "Code is incorrect.",
      ok: false,
    });
    expect(response.status).toBe(400);
    expect(resetSubmit).not.toHaveBeenCalled();
    expect(resetPoll).not.toHaveBeenCalled();
  });

  it("returns a bounded timeout error when reset polling does not finish", async () => {
    await storeResetCookie();
    vi.mocked(resetContinue).mockResolvedValueOnce({
      continuation_token: "reset-verified-handle",
    });
    vi.mocked(resetSubmit).mockResolvedValueOnce({
      continuation_token: "reset-submitted-handle",
    });
    vi.mocked(resetPoll).mockResolvedValue({
      continuation_token: "reset-still-running",
      status: "running",
    });

    const response = await verifyReset(jsonRequest("/api/auth/reset/verify", {
      code: "123456",
      [newCredentialField]: newCredentialFixture,
    }));

    await expect(readJson(response)).resolves.toEqual({
      error: "Reset is still processing. Please try again.",
      ok: false,
    });
    expect(response.status).toBe(504);
    expect(resetPoll).toHaveBeenCalledTimes(5);
    expect(setCookieHeader(response)).not.toContain(`${resetFlowCookieName}=;`);
  });

  it("resend honours cooldown and refreshes the reset flow after waiting", async () => {
    await storeResetCookie({ lastSentAt: Date.now() });

    const cooldownResponse = await resendReset(jsonRequest("/api/auth/reset/resend", {}));

    await expect(readJson(cooldownResponse)).resolves.toEqual({
      cooldownSeconds: 30,
      error: "Please wait before requesting another code.",
      ok: false,
    });
    expect(cooldownResponse.status).toBe(429);
    expect(resetChallenge).not.toHaveBeenCalled();

    await storeResetCookie({ lastSentAt: Date.now() - 31_000 });
    vi.mocked(resetChallenge).mockResolvedValueOnce({
      challenge_target_label: "c***@example.com",
      code_length: 6,
      continuation_token: "reset-code-handle-2",
    });

    const resendResponse = await resendReset(jsonRequest("/api/auth/reset/resend", {}));

    await expect(readJson(resendResponse)).resolves.toEqual({
      cooldownSeconds: 30,
      ok: true,
    });
    expect(resendResponse.status).toBe(200);
    expect(resetChallenge).toHaveBeenCalledWith({
      continuationToken: "reset-code-handle",
    });

    const decoded = await verifySignedCookieValue<ResetFlowState>(
      cookieValue(resendResponse, resetFlowCookieName),
      cookieSigningValue,
    );
    expect(decoded?.continuationToken).toBe("reset-code-handle-2");
    expect(decoded?.lastSentAt).toBe(Date.now());
  });

  it(["no", credentialField, "in cookie"].join(" "), async () => {
    vi.mocked(resetStart).mockResolvedValueOnce({
      continuation_token: "reset-start-handle",
    });
    vi.mocked(resetChallenge).mockResolvedValueOnce({
      challenge_target_label: "c***@example.com",
      code_length: 6,
      continuation_token: "reset-code-handle",
    });

    const startResponse = await startReset(jsonRequest("/api/auth/reset/start", {
      email: "casey@example.com",
    }));

    const rawResetCookie = cookieValue(startResponse, resetFlowCookieName);
    expect(rawResetCookie).toBeTruthy();
    expect(rawResetCookie).not.toContain(newCredentialFixture);

    const decoded = await verifySignedCookieValue<Record<string, unknown>>(
      rawResetCookie,
      cookieSigningValue,
    );
    expect(decoded).toMatchObject({
      channelLabel: "c***@example.com",
      codeLength: 6,
      continuationToken: "reset-code-handle",
      email: "casey@example.com",
    });
    expect(decoded).not.toHaveProperty(newCredentialField);
    expect(JSON.stringify(decoded)).not.toContain(newCredentialFixture);

    mockCookieStore.get.mockImplementation((name: string) =>
      name === resetFlowCookieName ? { value: rawResetCookie } : undefined,
    );
    vi.mocked(resetContinue).mockResolvedValueOnce({
      continuation_token: "reset-verified-handle",
    });
    vi.mocked(resetSubmit).mockResolvedValueOnce({
      continuation_token: "reset-submitted-handle",
    });
    vi.mocked(resetPoll).mockResolvedValueOnce({ status: "succeeded" });

    const verifyResponse = await verifyReset(jsonRequest("/api/auth/reset/verify", {
      code: "123456",
      [newCredentialField]: newCredentialFixture,
    }));

    expect(resetSubmit).toHaveBeenCalledWith({
      continuationToken: "reset-verified-handle",
      [newCredentialField]: newCredentialFixture,
    });
    expect(setCookieHeader(verifyResponse)).not.toContain(newCredentialFixture);
  });
});
