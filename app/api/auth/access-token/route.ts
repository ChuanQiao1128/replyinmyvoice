import { NextResponse } from "next/server";

import {
  getCurrentAccessToken,
  getCurrentSession,
} from "../../../../lib/entra-auth";
import { requireSameOrigin } from "../../../../lib/http";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  const originError = requireSameOrigin(request);
  if (originError) {
    return originError;
  }

  const session = await getCurrentSession();
  if (!session) {
    return NextResponse.json({ error: "unauthorized" }, { status: 401 });
  }

  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    return NextResponse.json({ error: "unauthorized" }, { status: 401 });
  }

  return NextResponse.json({ accessToken });
}
