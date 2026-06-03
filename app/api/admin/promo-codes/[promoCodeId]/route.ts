import { jsonError } from "../../../../../lib/http";
import {
  forwardAdminDetail,
  forwardAdminMutation,
  isGuid,
} from "../../../../../lib/admin-promo-proxy";

export const dynamic = "force-dynamic";

type RouteContext = {
  params: Promise<{ promoCodeId: string }>;
};

async function promoCodePath(context: RouteContext) {
  const { promoCodeId } = await context.params;
  if (!isGuid(promoCodeId)) {
    return null;
  }

  return `/api/console/promo-codes/${promoCodeId}`;
}

export async function GET(_request: Request, context: RouteContext) {
  const path = await promoCodePath(context);
  if (!path) {
    return jsonError("Invalid promo code id.", 400);
  }

  return forwardAdminDetail(path);
}

export async function PATCH(request: Request, context: RouteContext) {
  const path = await promoCodePath(context);
  if (!path) {
    return jsonError("Invalid promo code id.", 400);
  }

  return forwardAdminMutation(request, path, "PATCH");
}
