import { NextResponse } from "next/server";

import { dbHealthCheck } from "../../../../lib/db";
import { MissingEnvError } from "../../../../lib/env";

export const dynamic = "force-dynamic";

function redact(value: string, secret: string | undefined, label: string) {
  return secret ? value.replace(secret, label) : value;
}

export async function GET() {
  try {
    await dbHealthCheck();

    return NextResponse.json({ ok: true });
  } catch (error) {
    const message =
      error instanceof Error ? error.message : "Unknown database smoke error";
    const scrubbed = redact(
      redact(message, process.env.DATABASE_URL, "[DATABASE_URL]"),
      process.env.DIRECT_URL,
      "[DIRECT_URL]",
    );

    console.error("DB smoke failed", scrubbed);
    const code =
      typeof error === "object" &&
      error !== null &&
      "code" in error &&
      typeof error.code === "string"
        ? error.code
        : undefined;

    return NextResponse.json(
      {
        ok: false,
        reason:
          error instanceof MissingEnvError
            ? `missing:${error.message.replace("Missing required environment variable: ", "")}`
            : error instanceof Error
              ? error.name
              : "UnknownError",
        code,
      },
      { status: 500 },
    );
  }
}
