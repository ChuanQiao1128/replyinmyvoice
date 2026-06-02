import {
  forwardAdminGet,
  forwardAdminMutation,
} from "../../../../lib/admin-promo-proxy";

export const dynamic = "force-dynamic";

const promoCodesPath = "/api/admin/promo-codes";

export async function GET() {
  return forwardAdminGet(promoCodesPath);
}

export async function POST(request: Request) {
  return forwardAdminMutation(request, promoCodesPath, "POST");
}
