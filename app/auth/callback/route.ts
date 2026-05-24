import { NextResponse } from "next/server";

import { completeEntraCallback } from "../../../lib/entra-auth";
import { getAppUrl } from "../../../lib/env";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  try {
    const response = new NextResponse(null, { status: 307 });
    const result = await completeEntraCallback(request.url, response.cookies);
    response.headers.set("Location", `${getAppUrl()}${result.redirectTo}`);
    return response;
  } catch (error) {
    console.error("entra_callback_failed", {
      message: error instanceof Error ? error.message : "Unknown callback failure",
    });
    return NextResponse.redirect(`${getAppUrl()}/sign-in?error=callback`);
  }
}
