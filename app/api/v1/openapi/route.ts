import { NextResponse } from "next/server";

import openApiSpec from "../../../../public/openapi.json";

export const revalidate = 3600;

export function GET() {
  return NextResponse.json(openApiSpec, {
    headers: {
      "Cache-Control": "public, max-age=3600, stale-while-revalidate=86400",
    },
  });
}
