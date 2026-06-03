import {
  forwardAdminMutation,
  isGuid,
} from "../../../../../../lib/admin-promo-proxy";
import { jsonError } from "../../../../../../lib/http";

export const dynamic = "force-dynamic";

type RouteContext = {
  params: Promise<{ promoCodeId: string }>;
};

export async function POST(request: Request, context: RouteContext) {
  const { promoCodeId } = await context.params;
  if (!isGuid(promoCodeId)) {
    return jsonError("Invalid promo code id.", 400);
  }

  return forwardAdminMutation(
    request,
    `/api/console/promo-codes/${promoCodeId}/enable`,
    "POST",
  );
}
