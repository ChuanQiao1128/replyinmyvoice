import type { User as ClerkUser } from "@clerk/nextjs/server";
import { currentUser } from "@clerk/nextjs/server";
import type { User } from "./generated/prisma/client";

import { createId, getSql, nullableDate, requiredDate } from "./db";

type UserRow = Omit<User, "currentPeriodEnd" | "createdAt" | "updatedAt"> & {
  currentPeriodEnd: unknown;
  createdAt: unknown;
  updatedAt: unknown;
};

function primaryEmailFromClerk(user: ClerkUser): string | null {
  return (
    user.primaryEmailAddress?.emailAddress ??
    user.emailAddresses.at(0)?.emailAddress ??
    null
  );
}

export function mapUser(row: UserRow): User {
  return {
    ...row,
    currentPeriodEnd: nullableDate(row.currentPeriodEnd),
    createdAt: requiredDate(row.createdAt),
    updatedAt: requiredDate(row.updatedAt),
  };
}

export async function upsertUserFromClerk(user: ClerkUser): Promise<User> {
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
      ${user.id},
      ${primaryEmailFromClerk(user)},
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
  const user = await currentUser();
  if (!user) {
    return null;
  }

  return upsertUserFromClerk(user);
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
