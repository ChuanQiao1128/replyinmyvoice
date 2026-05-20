import { notFound } from "next/navigation";

import { getCurrentSession } from "./entra-auth";
import { optionalEnv } from "./env";

function parseList(value: string | undefined) {
  return (value ?? "")
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

export function isAdminIdentityAllowed({
  adminClerkUserIds,
  adminEmails,
  clerkUserId,
  email,
}: {
  clerkUserId?: string | null;
  email?: string | null;
  adminEmails?: string;
  adminClerkUserIds?: string;
}) {
  const emails = parseList(adminEmails).map((item) => item.toLowerCase());
  const userIds = parseList(adminClerkUserIds);
  const normalizedEmail = email?.trim().toLowerCase() ?? "";
  const normalizedUserId = clerkUserId?.trim() ?? "";

  return (
    (normalizedEmail.length > 0 && emails.includes(normalizedEmail)) ||
    (normalizedUserId.length > 0 && userIds.includes(normalizedUserId))
  );
}

export async function getCurrentAdminIdentity() {
  const session = await getCurrentSession();
  if (!session) {
    return null;
  }

  const allowed = isAdminIdentityAllowed({
    clerkUserId: session.sub,
    email: session.email,
    adminEmails: optionalEnv("ADMIN_EMAILS", ""),
    adminClerkUserIds: optionalEnv("ADMIN_CLERK_USER_IDS", ""),
  });

  return allowed ? { clerkUserId: session.sub, email: session.email } : null;
}

export async function requireAdminUser() {
  const admin = await getCurrentAdminIdentity();
  if (!admin) {
    notFound();
  }

  return admin;
}
