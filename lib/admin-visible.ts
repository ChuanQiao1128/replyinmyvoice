import { optionalEnv } from "./env";
import { isAdminIdentityAllowed } from "./admin-auth";

export function shouldShowAdminEntry({
  userId,
  email,
}: {
  userId?: string | null;
  email?: string | null;
}) {
  return isAdminIdentityAllowed({
    userId,
    email,
    adminEmails: optionalEnv("ADMIN_EMAILS", ""),
    adminUserIds: optionalEnv("ADMIN_USER_IDS", ""),
  });
}
