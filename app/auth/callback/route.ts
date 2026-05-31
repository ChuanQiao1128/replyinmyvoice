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
    const errorCode = callbackErrorCode(request.url);
    console.error("entra_callback_failed", {
      error: errorCode,
      kind: error instanceof Error ? error.name : "UnknownCallbackFailure",
    });
    return NextResponse.redirect(`${getAppUrl()}/sign-in?error=${errorCode}`);
  }
}

function callbackErrorCode(requestUrl: string) {
  try {
    const providerError = new URL(requestUrl).searchParams.get("error");
    return providerError === "access_denied" ? "access_denied" : "callback";
  } catch {
    return "callback";
  }
}
