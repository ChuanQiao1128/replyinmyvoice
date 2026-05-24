import { NextResponse } from "next/server";

import { getCurrentAccessToken } from "../../../../lib/entra-auth";

export const dynamic = "force-dynamic";

export async function GET() {
  const accessToken = await getCurrentAccessToken();
  if (!accessToken) {
    return NextResponse.json({ error: "unauthorized" }, { status: 401 });
  }

  return NextResponse.json({ accessToken });
}
