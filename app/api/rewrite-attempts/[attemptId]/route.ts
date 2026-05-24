import { NextResponse } from "next/server";

import { getAzureApiBaseUrl } from "../../../../lib/azure-api";
import { getCurrentAccessToken } from "../../../../lib/entra-auth";
import { jsonError } from "../../../../lib/http";
import { normalizeRewriteResponse } from "../../../../lib/rewrite-response";

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

function parseAttemptPayload(payload: unknown) {
  if (payload === null || typeof payload !== "object" || Array.isArray(payload)) {
    return null;
  }

  const attemptPayload = payload as AzureRewriteAttemptResponse;
  return {
    attemptId: attemptPayload.attemptId ?? attemptPayload.AttemptId,
    status: attemptPayload.status ?? attemptPayload.Status,
    resultJson: attemptPayload.resultJson ?? attemptPayload.ResultJson,
    errorCode: attemptPayload.errorCode ?? attemptPayload.ErrorCode,
  };
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

function isGuid(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
    value,
  );
}

export async function GET(
  _request: Request,
  context: { params: Promise<{ attemptId: string }> },
) {
  const { attemptId } = await context.params;
  if (!isGuid(attemptId)) {
    return jsonError("Invalid rewrite attempt id.", 400);
  }

  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    return jsonError("Authentication required.", 401);
  }

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

  const payload = parseAttemptPayload(await response.json().catch(() => null));
  if (!payload?.status) {
    return jsonError("Rewrite attempt response was invalid.", 502);
  }

  if (payload.status === "Succeeded" && payload.resultJson) {
    return jsonFromResultJson(payload.resultJson);
  }

  if (payload.status === "Failed" || payload.status === "Expired") {
    return failedAttemptResponse(payload.errorCode);
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
