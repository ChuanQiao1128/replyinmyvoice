import type { User } from "./generated/prisma/client";

import { isPaidSubscriptionStatus } from "./quota";

export type SubscriptionState = {
  isActive: boolean;
  status: string;
  currentPeriodEnd?: Date | null;
};

export function getSubscriptionState(user: Pick<User, "subscriptionStatus" | "currentPeriodEnd">): SubscriptionState {
  return {
    isActive: isPaidSubscriptionStatus(user.subscriptionStatus),
    status: user.subscriptionStatus,
    currentPeriodEnd: user.currentPeriodEnd,
  };
}
