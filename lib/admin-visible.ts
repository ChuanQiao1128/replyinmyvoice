import { optionalEnv } from "./env";
import { isAdminIdentityAllowed } from "./admin-auth";

export function shouldShowAdminEntry({
  clerkUserId,
  email,
}: {
  clerkUserId?: string | null;
  email?: string | null;
}) {
  return isAdminIdentityAllowed({
    clerkUserId,
    email,
    adminEmails: optionalEnv("ADMIN_EMAILS", ""),
    adminClerkUserIds: optionalEnv("ADMIN_CLERK_USER_IDS", ""),
  });
}
