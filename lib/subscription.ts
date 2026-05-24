import { isPaidSubscriptionStatus } from "./quota";

export type SubscriptionState = {
  isActive: boolean;
  status: string;
  currentPeriodEnd?: Date | null;
};

export function getSubscriptionState(user: {
  subscriptionStatus: string;
  currentPeriodEnd?: Date | null;
}): SubscriptionState {
  return {
    isActive: isPaidSubscriptionStatus(user.subscriptionStatus),
    status: user.subscriptionStatus,
    currentPeriodEnd: user.currentPeriodEnd,
  };
}
