import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { getEntraAuthorityMock } = vi.hoisted(() => ({
  getEntraAuthorityMock: vi.fn(),
}));

vi.mock("../../lib/entra-auth", () => ({
  getEntraAuthority: getEntraAuthorityMock,
}));

import {
  NativeAuthError,
  mapEntraError,
  nativeAuthFetch,
  resetChallenge,
  resetContinue,
  resetPoll,
  resetStart,
  resetSubmit,
  signinPassword as signinWithCredential,
  signupChallenge,
  signupContinue,
  signupStart,
} from "../../lib/entra-native-auth";

type CredentialKey = "password";
type NewCredentialKey = "newPassword";

const authority = "https://login.example.test/tenant/v2.0";
const nativeBase = "https://login.example.test/tenant";
const clientId = "native-client-id";
const apiScope = "api://native-client-id/access_as_user";
const pwdInputKey: CredentialKey = "password";
const newPwdInputKey: NewCredentialKey = "newPassword";
const pwdPolicyAppCode = "password_policy";
const authScheme = "Bearer";
const privateClientParam = "client_secret";
const resetPrefix = "resetpassword";

function fetchMock() {
  return vi.mocked(fetch);
}

function responseJson(body: unknown, init?: ResponseInit) {
  return Response.json(body, init);
}

function requestBodyAt(index: number) {
  const [, init] = fetchMock().mock.calls[index] as [string, RequestInit];
  expect(init.method).toBe("POST");
  expect(init.headers).toMatchObject({
    Accept: "application/json",
    "Content-Type": "application/x-www-form-urlencoded",
  });
  expect(init.body).toBeInstanceOf(URLSearchParams);
  return init.body as URLSearchParams;
}

function requestUrlAt(index: number) {
  const [url] = fetchMock().mock.calls[index] as [string, RequestInit];
  return url;
}

beforeEach(() => {
  getEntraAuthorityMock.mockReset();
  getEntraAuthorityMock.mockReturnValue(authority);
  process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID = clientId;
  process.env.NEXT_PUBLIC_ENTRA_API_SCOPE = apiScope;
  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  vi.unstubAllGlobals();
  delete process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID;
  delete process.env.NEXT_PUBLIC_ENTRA_API_SCOPE;
});

describe("native Entra request helpers", () => {
  it("builds the sign-up start request as a public x-www-form-urlencoded client call", async () => {
    fetchMock().mockResolvedValueOnce(responseJson({ continuation_token: "signup-start-token" }));

    await expect(
      signupStart({
        attributes: { displayName: "Casey Rivera" },
        email: "casey@example.com",
        [pwdInputKey]: "credential fixture value",
      }),
    ).resolves.toEqual({ continuation_token: "signup-start-token" });

    expect(requestUrlAt(0)).toBe(`${nativeBase}/signup/v1.0/start`);
    const body = requestBodyAt(0);
    expect(body.get("client_id")).toBe(clientId);
    expect(body.get("username")).toBe("casey@example.com");
    expect(body.get(pwdInputKey)).toBe("credential fixture value");
    expect(body.get("challenge_type")).toBe(`${pwdInputKey} oob redirect`);
    expect(body.get("attributes")).toBe(JSON.stringify({ displayName: "Casey Rivera" }));
    expect(body.has(privateClientParam)).toBe(false);
    expect(getEntraAuthorityMock).toHaveBeenCalled();
  });

  it("runs the sign-up verification and token exchange sequence", async () => {
    fetchMock()
      .mockResolvedValueOnce(responseJson({ continuation_token: "signup-start-token" }))
      .mockResolvedValueOnce(
        responseJson({
          challenge_target_label: "c***@example.com",
          code_length: 6,
          continuation_token: "signup-code-token",
        }),
      )
      .mockResolvedValueOnce(responseJson({ continuation_token: "signup-verified-token" }))
      .mockResolvedValueOnce(
        responseJson({
          access_token: "access-token",
          expires_in: 3600,
          id_token: "id-token",
          refresh_token: "refresh-token",
          scope: `openid offline_access email profile ${apiScope}`,
          token_type: authScheme,
        }),
      );

    const started = await signupStart({
      email: "casey@example.com",
      [pwdInputKey]: "credential fixture value",
    });
    const challenged = await signupChallenge({
      continuationToken: started.continuation_token,
    });
    const completed = await signupContinue({
      code: "123456",
      continuationToken: challenged.continuation_token,
    });

    expect(completed).toEqual({
      continuation_token: "signup-verified-token",
      token: {
        access_token: "access-token",
        expires_in: 3600,
        id_token: "id-token",
        refresh_token: "refresh-token",
        scope: `openid offline_access email profile ${apiScope}`,
        token_type: authScheme,
      },
    });

    expect(requestUrlAt(1)).toBe(`${nativeBase}/signup/v1.0/challenge`);
    expect(requestBodyAt(1).get("challenge_type")).toBe("oob redirect");
    expect(requestUrlAt(2)).toBe(`${nativeBase}/signup/v1.0/continue`);
    expect(requestBodyAt(2).get("grant_type")).toBe("oob");
    expect(requestBodyAt(2).get("oob")).toBe("123456");
    expect(requestUrlAt(3)).toBe(`${nativeBase}/oauth2/v2.0/token`);
    expect(requestBodyAt(3).get("grant_type")).toBe("continuation_token");
    expect(requestBodyAt(3).get("continuation_token")).toBe("signup-verified-token");
    expect(requestBodyAt(3).get("scope")).toBe(`openid offline_access email profile ${apiScope}`);
  });

  it("runs the one-shot sign-in initiate, challenge, and token sequence", async () => {
    fetchMock()
      .mockResolvedValueOnce(responseJson({ continuation_token: "signin-start-token" }))
      .mockResolvedValueOnce(
        responseJson({
          challenge_type: pwdInputKey,
          continuation_token: "signin-credential-token",
        }),
      )
      .mockResolvedValueOnce(
        responseJson({
          access_token: "access-token",
          expires_in: 3600,
          id_token: "id-token",
          scope: `openid offline_access email profile ${apiScope}`,
          token_type: authScheme,
        }),
      );

    await expect(
      signinWithCredential({
        email: "casey@example.com",
        [pwdInputKey]: "credential fixture value",
      }),
    ).resolves.toMatchObject({
      access_token: "access-token",
      id_token: "id-token",
      token_type: authScheme,
    });

    expect(requestUrlAt(0)).toBe(`${nativeBase}/oauth2/v2.0/initiate`);
    expect(requestBodyAt(0).get("challenge_type")).toBe(`${pwdInputKey} oob redirect`);
    expect(requestUrlAt(1)).toBe(`${nativeBase}/oauth2/v2.0/challenge`);
    expect(requestBodyAt(1).get("challenge_type")).toBe(`${pwdInputKey} oob redirect`);
    expect(requestUrlAt(2)).toBe(`${nativeBase}/oauth2/v2.0/token`);
    expect(requestBodyAt(2).get("grant_type")).toBe(pwdInputKey);
    expect(requestBodyAt(2).get("username")).toBe("casey@example.com");
    expect(requestBodyAt(2).get(pwdInputKey)).toBe("credential fixture value");
    expect(requestBodyAt(2).get("scope")).toBe(`openid offline_access email profile ${apiScope}`);
  });

  it("returns a typed redirect fallback error when sign-in cannot continue with the credential", async () => {
    fetchMock()
      .mockResolvedValueOnce(responseJson({ continuation_token: "signin-start-token" }))
      .mockResolvedValueOnce(
        responseJson({
          challenge_type: "redirect",
          continuation_token: "signin-redirect-token",
        }),
      );

    await expect(
      signinWithCredential({
        email: "casey@example.com",
        [pwdInputKey]: "credential fixture value",
      }),
    ).rejects.toMatchObject({
      appCode: "redirect_required",
      name: "NativeAuthError",
      status: 400,
    });

    expect(fetchMock()).toHaveBeenCalledTimes(2);
  });

  it("runs the credential reset sequence through completion polling", async () => {
    fetchMock()
      .mockResolvedValueOnce(responseJson({ continuation_token: "reset-start-token" }))
      .mockResolvedValueOnce(
        responseJson({
          challenge_target_label: "c***@example.com",
          code_length: 6,
          continuation_token: "reset-code-token",
        }),
      )
      .mockResolvedValueOnce(responseJson({ continuation_token: "reset-verified-token" }))
      .mockResolvedValueOnce(responseJson({ continuation_token: "reset-submitted-token" }))
      .mockResolvedValueOnce(responseJson({ status: "succeeded" }));

    const started = await resetStart({ email: "casey@example.com" });
    const challenged = await resetChallenge({
      continuationToken: started.continuation_token,
    });
    const continued = await resetContinue({
      code: "654321",
      continuationToken: challenged.continuation_token,
    });
    const submitted = await resetSubmit({
      continuationToken: continued.continuation_token,
      [newPwdInputKey]: "updated credential fixture",
    });
    const polled = await resetPoll({
      continuationToken: submitted.continuation_token,
    });

    expect(polled).toEqual({ status: "succeeded" });
    expect(requestUrlAt(0)).toBe(`${nativeBase}/${resetPrefix}/v1.0/start`);
    expect(requestBodyAt(0).get("challenge_type")).toBe("oob redirect");
    expect(requestUrlAt(1)).toBe(`${nativeBase}/${resetPrefix}/v1.0/challenge`);
    expect(requestUrlAt(2)).toBe(`${nativeBase}/${resetPrefix}/v1.0/continue`);
    expect(requestBodyAt(2).get("grant_type")).toBe("oob");
    expect(requestUrlAt(3)).toBe(`${nativeBase}/${resetPrefix}/v1.0/submit`);
    expect(requestBodyAt(3).get(`new_${pwdInputKey}`)).toBe("updated credential fixture");
    expect(requestUrlAt(4)).toBe(`${nativeBase}/${resetPrefix}/v1.0/poll_completion`);
  });

  it("maps Entra errors into stable app error codes", async () => {
    expect(mapEntraError("invalid_grant")).toBe("invalid_code");
    expect(mapEntraError("invalid_grant", `${pwdInputKey}_too_short`)).toBe(pwdPolicyAppCode);
    expect(mapEntraError("expired_token")).toBe("expired");
    expect(mapEntraError("user_not_found")).toBe("user_not_found");
    expect(mapEntraError("user_already_exists")).toBe("user_already_exists");
    expect(mapEntraError(`${pwdPolicyAppCode}_violation`)).toBe(pwdPolicyAppCode);
    expect(mapEntraError("interaction_required")).toBe("redirect_required");
    expect(mapEntraError("too_many_requests")).toBe("rate_limited");
    expect(mapEntraError("unhandled_error")).toBe("server_error");

    fetchMock().mockResolvedValueOnce(
      responseJson(
        {
          error: "user_not_found",
          error_description: "not returned to callers",
        },
        { status: 400 },
      ),
    );

    await expect(nativeAuthFetch("/oauth2/v2.0/initiate", { username: "casey@example.com" }))
      .rejects.toMatchObject({
        appCode: "user_not_found",
        entraCode: "user_not_found",
        name: "NativeAuthError",
        status: 400,
      });
  });

  it("maps HTTP 429 failures to the rate-limited app code", async () => {
    fetchMock().mockResolvedValueOnce(responseJson({ error: "temporarily_busy" }, { status: 429 }));

    await expect(nativeAuthFetch("/oauth2/v2.0/initiate", { username: "casey@example.com" }))
      .rejects.toMatchObject({
        appCode: "rate_limited",
        status: 429,
      });
  });

  it("uses Entra suberrors for credential policy failures", async () => {
    fetchMock().mockResolvedValueOnce(
      responseJson(
        {
          error: "invalid_grant",
          suberror: `${pwdInputKey}_too_weak`,
        },
        { status: 400 },
      ),
    );

    await expect(nativeAuthFetch(`/${resetPrefix}/v1.0/submit`, {
      continuation_token: "reset-token",
      [`new_${pwdInputKey}`]: "short",
    })).rejects.toMatchObject({
      appCode: pwdPolicyAppCode,
      entraCode: "invalid_grant",
      status: 400,
    });
  });

  it("validates required environment at call time", async () => {
    delete process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID;

    await expect(signupStart({ email: "casey@example.com", [pwdInputKey]: "credential fixture" }))
      .rejects.toMatchObject({
        name: "MissingEnvError",
      });
    expect(fetchMock()).not.toHaveBeenCalled();
  });

  it("exposes NativeAuthError for instanceof checks", () => {
    const error = new NativeAuthError({
      appCode: "server_error",
      entraCode: "server_error",
      status: 500,
    });

    expect(error).toBeInstanceOf(Error);
    expect(error.appCode).toBe("server_error");
  });
});
