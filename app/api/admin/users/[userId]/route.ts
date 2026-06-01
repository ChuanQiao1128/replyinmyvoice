import { forwardAdminGet } from "../../../../../lib/admin-api-proxy";

export const dynamic = "force-dynamic";

type RouteContext = {
  params: Promise<{ userId: string }>;
};

export async function GET(request: Request, context: RouteContext) {
  const { userId } = await context.params;
  return forwardAdminGet(request, `/api/admin/users/${encodeURIComponent(userId)}`);
}
