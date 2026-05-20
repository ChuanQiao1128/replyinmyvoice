import { NextResponse } from "next/server";

import { completeEntraCallback } from "../../../lib/entra-auth";
import { getAppUrl } from "../../../lib/env";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  try {
    const result = await completeEntraCallback(request.url);
    return NextResponse.redirect(`${getAppUrl()}${result.redirectTo}`);
  } catch (error) {
    console.error("entra_callback_failed", {
      message: error instanceof Error ? error.message : "Unknown callback failure",
    });
    return NextResponse.redirect(`${getAppUrl()}/sign-in?error=callback`);
  }
}
