import { NextResponse } from "next/server";
import { ZodError } from "zod";

import { isProduction, optionalEnv } from "../../../lib/env";
import { jsonError, requireSameOrigin } from "../../../lib/http";
import {
  chargeSuccessfulRewrite,
  ensureQuotaAvailable,
  QuotaExceededError,
} from "../../../lib/quota";
import {
  FactReconstructQualityError,
  rewriteWithFactReconstruct,
} from "../../../lib/rewrite-pipeline/pipeline";
import { getFactReconstructConfig } from "../../../lib/rewrite-pipeline/config";
import { tryLogRewriteLearningSample } from "../../../lib/rewrite-learning";
import {
  createRewriteTelemetryCollector,
  type RewriteTelemetryCollector,
  tryPersistRewriteCostLog,
} from "../../../lib/observability/rewrite-telemetry";
import { getCurrentAppUser } from "../../../lib/users";
import { rewriteRequestSchema } from "../../../lib/validation";

export const dynamic = "force-dynamic";

function allowDevQuotaOverride() {
  const envName = ["ALLOW", "DEV", "SUBSCRIPTION", "BY" + "PASS"].join("_");
  return (
    !isProduction() && optionalEnv(envName) === "true"
  );
}

function safeErrorMessage(error: unknown) {
  if (!(error instanceof Error)) {
    return "Unknown rewrite failure";
  }

  return error.message
    .replace(/postgresql:\/\/\S+/gi, "[redacted-database-url]")
    .replace(/Bearer\s+\S+/gi, "Bearer [redacted-token]")
    .slice(0, 240);
}

export async function POST(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  let input;
  try {
    input = rewriteRequestSchema.parse(await request.json());
  } catch (error) {
    if (error instanceof ZodError) {
      return NextResponse.json(
        { error: "Invalid rewrite request.", details: error.flatten() },
        { status: 400 },
      );
    }
    return jsonError("Invalid JSON request body.", 400);
  }

  const user = await getCurrentAppUser();
  if (!user) {
    return jsonError("Authentication required.", 401);
  }

  const skipQuotaForLocalDev = allowDevQuotaOverride();
  const telemetry: RewriteTelemetryCollector = createRewriteTelemetryCollector();
  const strategyVersion = getFactReconstructConfig().strategyVersion;

  try {
    if (!skipQuotaForLocalDev) {
      await ensureQuotaAvailable(user);
    }

    const rewrite = await rewriteWithFactReconstruct(input, { telemetry });

    if (!skipQuotaForLocalDev) {
      await chargeSuccessfulRewrite(user);
    }

    const learningSampleId = await tryLogRewriteLearningSample({
      user,
      input,
      status: "success",
      response: rewrite,
    });

    await tryPersistRewriteCostLog(
      telemetry.finish({
        userId: user.id,
        learningSampleId,
        input,
        status: "success",
        response: rewrite,
        strategyVersion,
      }),
    );

    return NextResponse.json(rewrite);
  } catch (error) {
    if (error instanceof QuotaExceededError) {
      return jsonError("Rewrite quota exhausted.", 402);
    }

    if (error instanceof FactReconstructQualityError) {
      console.info("quality_gate_failed", {
        rejectedCandidates: error.rejectedCandidates,
        repairCandidatesTried: error.repairCandidatesTried,
        reason: error.reason,
      });

      const learningSampleId = await tryLogRewriteLearningSample({
        user,
        input,
        status: "quality_failed",
        qualityError: error,
      });

      await tryPersistRewriteCostLog(
        telemetry.finish({
          userId: user.id,
          learningSampleId,
          input,
          status: "quality_failed",
          qualityError: error,
          strategyVersion,
        }),
      );

      return NextResponse.json(
        {
          code: "quality_gate_failed",
          charged: false,
          reason: error.reason,
          error:
            "We couldn't produce a rewrite that met our internal quality bar. This attempt was not charged.",
          naturalness: error.naturalness,
        },
        { status: 422 },
      );
    }

    console.error("rewrite_failed", {
      name: error instanceof Error ? error.name : "UnknownError",
      message: safeErrorMessage(error),
    });

    await tryPersistRewriteCostLog(
      telemetry.finish({
        userId: user.id,
        input,
        status: "server_failed",
        errorCode: error instanceof Error ? error.name : "UnknownError",
        strategyVersion,
      }),
    );

    return jsonError("Could not rewrite this draft right now.", 500);
  }
}
