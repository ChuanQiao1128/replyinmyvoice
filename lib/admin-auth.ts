import type { AuthSession } from "./entra-auth";
import { getCurrentSession } from "./entra-auth";
import { optionalEnv } from "./env";

function configuredAdminIds() {
  return optionalEnv("ADMIN_EMAILS")
    .split(",")
    .map((value) => value.trim().toLowerCase())
    .filter(Boolean);
}

export function isAdminSession(session: AuthSession | null) {
  if (!session) {
    return false;
  }

  const allowed = new Set(configuredAdminIds());
  const identifiers = [session.sub, session.email]
    .filter((value): value is string => Boolean(value?.trim()))
    .map((value) => value.trim().toLowerCase());

  return identifiers.some((value) => allowed.has(value));
}

export async function getCurrentAdminSession() {
  const session = await getCurrentSession();
  return isAdminSession(session) ? session : null;
}
