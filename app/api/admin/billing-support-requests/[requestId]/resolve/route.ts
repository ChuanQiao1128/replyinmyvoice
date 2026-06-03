import { forwardAdminPost } from "../../../../../../lib/admin-api-proxy";

export const dynamic = "force-dynamic";

type RouteContext = {
  params: Promise<{ requestId: string }>;
};

export async function POST(request: Request, context: RouteContext) {
  const { requestId } = await context.params;
  return forwardAdminPost(
    request,
    `/api/console/billing-support-requests/${encodeURIComponent(requestId)}/resolve`,
  );
}
