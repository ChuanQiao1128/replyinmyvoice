import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../lib/azure-api";
import { getCurrentAccessToken } from "../../../lib/entra-auth";
import { jsonError, requireSameOrigin } from "../../../lib/http";
import { normalizeRewriteResponse } from "../../../lib/rewrite-response";
import { copyV1ResponseHeaders } from "../../../lib/v1-response-headers";

export const dynamic = "force-dynamic";

type AzureRewriteAttemptResponse = {
  attemptId?: string;
  AttemptId?: string;
  status?: string;
  Status?: string;
  resultJson?: string | null;
  ResultJson?: string | null;
  errorCode?: string | null;
  ErrorCode?: string | null;
};

const qualityFailureCodes = new Set([
  "quality_signal_unavailable",
  "structure_gate_failed",
  "naturalness_gate_failed",
  "fact_gate_failed",
  "policy_intent_gate_failed",
]);

function sleep(milliseconds: number) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function jsonFromResultJson(resultJson: string) {
  try {
    const payload = normalizeRewriteResponse(JSON.parse(resultJson));
    if (!payload) {
      return jsonError("Rewrite result was invalid.", 502);
    }

    return NextResponse.json(payload);
  } catch {
    return jsonError("Rewrite result was not valid JSON.", 502);
  }
}

function qualityFailureResponse(errorCode?: string | null) {
  return NextResponse.json(
    {
      code: "quality_gate_failed",
      charged: false,
      reason: errorCode ?? "quality_gate_failed",
      error:
        "We couldn't produce a rewrite that met our internal quality bar. This attempt was not charged.",
    },
    { status: 422 },
  );
}

function failedAttemptResponse(errorCode?: string | null) {
  if (errorCode && qualityFailureCodes.has(errorCode)) {
    return qualityFailureResponse(errorCode);
  }

  return jsonError("Could not rewrite this draft right now.", 500);
}

async function readJsonPayload(response: Response) {
  return (await response.json().catch(() => null)) as unknown;
}

function parseAttemptPayload(payload: unknown) {
  if (payload === null || typeof payload !== "object" || Array.isArray(payload)) {
    return null;
  }

  const attemptPayload = payload as AzureRewriteAttemptResponse;
  const parsed = {
    attemptId: attemptPayload.attemptId ?? attemptPayload.AttemptId,
    status: attemptPayload.status ?? attemptPayload.Status,
    resultJson: attemptPayload.resultJson ?? attemptPayload.ResultJson,
    errorCode: attemptPayload.errorCode ?? attemptPayload.ErrorCode,
  };

  if (
    !parsed.attemptId &&
    !parsed.status &&
    !parsed.resultJson &&
    !parsed.errorCode
  ) {
    return null;
  }

  return parsed;
}

async function parseAttemptResponse(response: Response) {
  const payload = parseAttemptPayload(await readJsonPayload(response));
  if (!payload) {
    return null;
  }

  return payload;
}

async function pollAttempt({
  accessToken,
  attemptId,
}: {
  accessToken: string;
  attemptId: string;
}) {
  for (let attempt = 0; attempt < 20; attempt += 1) {
    await sleep(750);

    const response = await fetch(
      `${getAzureApiBaseUrl()}/api/rewrite-attempts/${attemptId}`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
        cache: "no-store",
      },
    );

    if (!response.ok) {
      return new NextResponse(await response.text(), {
        status: response.status,
        headers: {
          "Content-Type": response.headers.get("content-type") ?? "application/json",
        },
      });
    }

    const payload = await parseAttemptResponse(response);
    if (!payload?.status) {
      return jsonError("Rewrite attempt response was invalid.", 502);
    }

    if (payload.status === "Succeeded" && payload.resultJson) {
      return jsonFromResultJson(payload.resultJson);
    }

    if (payload.status === "Failed" || payload.status === "Expired") {
      return failedAttemptResponse(payload.errorCode);
    }
  }

  return NextResponse.json(
    {
      code: "rewrite_pending",
      attemptId,
      error: "Rewrite is still processing. Try again in a moment.",
    },
    { status: 202 },
  );
}

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    return jsonError("Authentication required.", 401);
  }

  const body = await request.text();
  if (!body.trim()) {
    return jsonError("Invalid JSON request body.", 400);
  }

  const idempotencyKey =
    request.headers.get("X-Idempotency-Key") ??
    (typeof crypto.randomUUID === "function"
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(16).slice(2)}`);

  const response = await fetch(`${getAzureApiBaseUrl()}/api/rewrite`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": request.headers.get("content-type") ?? "application/json",
      "X-Idempotency-Key": idempotencyKey,
    },
    body,
    cache: "no-store",
  });

  if (response.status === 429) {
    const headers = new Headers({ "Content-Type": "application/json" });
    copyV1ResponseHeaders(response.headers, headers);
    return new NextResponse(
      JSON.stringify({
        code: "rate_limited",
        error: "You're sending rewrites too quickly. Please wait a moment and try again.",
      }),
      { status: 429, headers },
    );
  }

  if (response.status === 202) {
    const payload = await parseAttemptResponse(response);
    if (!payload?.attemptId) {
      return jsonError("Rewrite attempt response was invalid.", 502);
    }

    return pollAttempt({ accessToken, attemptId: payload.attemptId });
  }

  if (response.ok) {
    const rawPayload = await readJsonPayload(response);
    const directPayload = normalizeRewriteResponse(rawPayload);
    if (directPayload) {
      return NextResponse.json(directPayload);
    }

    const attemptPayload = parseAttemptPayload(rawPayload);
    if (attemptPayload?.status === "Succeeded" && attemptPayload.resultJson) {
      return jsonFromResultJson(attemptPayload.resultJson);
    }

    if (!attemptPayload) {
      return jsonError("Rewrite response was invalid.", 502);
    }

    return new NextResponse(JSON.stringify(attemptPayload), {
      status: response.status,
      headers: {
        "Content-Type": "application/json",
      },
    });
  }

  return new NextResponse(await response.text(), {
    status: response.status,
    headers: {
      "Content-Type": response.headers.get("content-type") ?? "application/json",
    },
  });
}
