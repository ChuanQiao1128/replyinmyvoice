import { forwardAdminGet } from "../../../../lib/admin-api-proxy";

export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  return forwardAdminGet(request, "/api/admin/users");
}
