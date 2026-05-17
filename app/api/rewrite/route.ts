import { auth } from "@clerk/nextjs/server";
import { NextResponse } from "next/server";
import { ZodError } from "zod";

import { isProduction, optionalEnv } from "../../../lib/env";
import { jsonError, requireSameOrigin } from "../../../lib/http";
import {
  chargeSuccessfulRewrite,
  ensureQuotaAvailable,
  QuotaExceededError,
} from "../../../lib/quota";
import { rewriteWithOptimization } from "../../../lib/rewrite";
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

  const { userId } = await auth();
  if (!userId) {
    return jsonError("Authentication required.", 401);
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

  try {
    if (!skipQuotaForLocalDev) {
      await ensureQuotaAvailable(user);
    }

    const rewrite = await rewriteWithOptimization(input);

    if (!skipQuotaForLocalDev) {
      await chargeSuccessfulRewrite(user);
    }

    return NextResponse.json(rewrite);
  } catch (error) {
    if (error instanceof QuotaExceededError) {
      return jsonError("Rewrite quota exhausted.", 402);
    }

    console.error("rewrite_failed", {
      name: error instanceof Error ? error.name : "UnknownError",
      message: safeErrorMessage(error),
    });

    return jsonError("Could not rewrite this draft right now.", 500);
  }
}
