import type { User as ClerkUser } from "@clerk/nextjs/server";
import { currentUser } from "@clerk/nextjs/server";
import type { User } from "@prisma/client";

function primaryEmailFromClerk(user: ClerkUser): string | null {
  return (
    user.primaryEmailAddress?.emailAddress ??
    user.emailAddresses.at(0)?.emailAddress ??
    null
  );
}

export async function upsertUserFromClerk(user: ClerkUser): Promise<User> {
  const { prisma } = await import("./db");

  return prisma.user.upsert({
    where: {
      clerkUserId: user.id,
    },
    create: {
      clerkUserId: user.id,
      email: primaryEmailFromClerk(user),
    },
    update: {
      email: primaryEmailFromClerk(user),
    },
  });
}

export async function getCurrentAppUser(): Promise<User | null> {
  const user = await currentUser();
  if (!user) {
    return null;
  }

  return upsertUserFromClerk(user);
}

export async function findUserByClerkId(clerkUserId: string) {
  const { prisma } = await import("./db");

  return prisma.user.findUnique({
    where: {
      clerkUserId,
    },
  });
}
