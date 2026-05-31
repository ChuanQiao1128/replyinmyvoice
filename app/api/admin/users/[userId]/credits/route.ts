import { forwardAdminPost } from "../../../../../../lib/admin-api-proxy";

export const dynamic = "force-dynamic";

type RouteContext = {
  params: Promise<{ userId: string }>;
};

export async function POST(request: Request, context: RouteContext) {
  const { userId } = await context.params;
  return forwardAdminPost(
    request,
    `/api/admin/users/${encodeURIComponent(userId)}/credits`,
  );
}
