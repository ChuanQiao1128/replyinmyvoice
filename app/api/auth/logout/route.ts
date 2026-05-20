import { NextResponse } from "next/server";

import { clearCurrentSession } from "../../../../lib/entra-auth";
import { getAppUrl } from "../../../../lib/env";

export const dynamic = "force-dynamic";

export async function GET() {
  await clearCurrentSession();
  return NextResponse.redirect(`${getAppUrl()}/`);
}

export async function POST() {
  await clearCurrentSession();
  return NextResponse.redirect(`${getAppUrl()}/`);
}
