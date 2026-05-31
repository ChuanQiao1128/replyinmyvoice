export type AdminUsersListResponse = {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  users: AdminUserListItem[];
};

export type AdminUserListItem = {
  id: string;
  externalAuthUserId: string;
  email: string | null;
  subscriptionStatus: string;
  createdAt: string;
  updatedAt: string;
  usedRewrites: number;
  reservedRewrites: number;
  creditRemaining: number;
  costToDateUsd: number;
};

export type AdminUserDetailResponse = {
  id: string;
  externalAuthUserId: string;
  email: string | null;
  createdAt: string;
  updatedAt: string;
  subscription: AdminSubscriptionSummary;
  usage: AdminUsagePeriod[];
  credits: AdminCredit[];
  payments: AdminPayment[];
  costToDateUsd: number;
};

export type AdminSubscriptionSummary = {
  status: string;
  stripeCustomerId: string | null;
  stripeSubscriptionId: string | null;
  currentPeriodEnd: string | null;
};

export type AdminUsagePeriod = {
  id: string;
  periodKey: string;
  quota: number;
  used: number;
  reserved: number;
  periodStart: string | null;
  periodEnd: string | null;
  createdAt: string;
  updatedAt: string;
};

export type AdminCredit = {
  id: string;
  source: string;
  amountGranted: number;
  amountConsumed: number;
  remaining: number;
  grantedAt: string;
  expiresAt: string | null;
  stripeEventId: string | null;
  paymentIntentId: string | null;
  sku: string | null;
  amountTotal: number | null;
  currency: string | null;
};

export type AdminPayment = {
  creditId: string;
  source: string;
  eventId: string | null;
  paymentIntentId: string | null;
  sku: string | null;
  amountTotal: number | null;
  currency: string | null;
  grantedAt: string;
  expiresAt: string | null;
  creditsGranted: number;
  creditsConsumed: number;
  creditsRemaining: number;
  receiptUrl?: string | null;
};

export type AdminStatsResponse = {
  totalUsers: number;
  paidUsers: number;
  freeUsers: number;
  usageUsed: number;
  usageReserved: number;
  creditRemaining: number;
  paymentCount: number;
  paymentAmountTotal: number;
  costToDateUsd: number;
};
