import type { User } from "./generated/prisma/client";

import { createId, getSql, nullableDate, requiredDate } from "./db";
import { getCurrentSession, type AuthSession } from "./entra-auth";

type UserRow = Omit<User, "currentPeriodEnd" | "createdAt" | "updatedAt"> & {
  currentPeriodEnd: unknown;
  createdAt: unknown;
  updatedAt: unknown;
};

export function mapUser(row: UserRow): User {
  return {
    ...row,
    currentPeriodEnd: nullableDate(row.currentPeriodEnd),
    createdAt: requiredDate(row.createdAt),
    updatedAt: requiredDate(row.updatedAt),
  };
}

export async function upsertUserFromAuthSession(session: AuthSession): Promise<User> {
  const sql = getSql();
  const rows = (await sql`
    INSERT INTO "User" (
      "id",
      "clerkUserId",
      "email",
      "createdAt",
      "updatedAt"
    )
    VALUES (
      ${createId()},
      ${session.sub},
      ${session.email},
      now(),
      now()
    )
    ON CONFLICT ("clerkUserId")
    DO UPDATE SET
      "email" = EXCLUDED."email",
      "updatedAt" = now()
    RETURNING *
  `) as UserRow[];

  return mapUser(rows[0]);
}

export async function getCurrentAppUser(): Promise<User | null> {
  const session = await getCurrentSession();
  if (!session) {
    return null;
  }

  return upsertUserFromAuthSession(session);
}

export async function findUserByClerkId(clerkUserId: string) {
  const sql = getSql();
  const rows = (await sql`
    SELECT *
    FROM "User"
    WHERE "clerkUserId" = ${clerkUserId}
    LIMIT 1
  `) as UserRow[];

  return rows[0] ? mapUser(rows[0]) : null;
}

export async function findUserByStripeCustomerId(stripeCustomerId: string) {
  const sql = getSql();
  const rows = (await sql`
    SELECT *
    FROM "User"
    WHERE "stripeCustomerId" = ${stripeCustomerId}
    LIMIT 1
  `) as UserRow[];

  return rows[0] ? mapUser(rows[0]) : null;
}

export async function updateUserStripeCustomer(
  userId: string,
  stripeCustomerId: string,
): Promise<User> {
  const sql = getSql();
  const rows = (await sql`
    UPDATE "User"
    SET
      "stripeCustomerId" = ${stripeCustomerId},
      "updatedAt" = now()
    WHERE "id" = ${userId}
    RETURNING *
  `) as UserRow[];

  return mapUser(rows[0]);
}

export async function updateStripeCustomerByClerkId(
  clerkUserId: string,
  stripeCustomerId: string,
) {
  const sql = getSql();
  await sql`
    UPDATE "User"
    SET
      "stripeCustomerId" = ${stripeCustomerId},
      "updatedAt" = now()
    WHERE "clerkUserId" = ${clerkUserId}
  `;
}
