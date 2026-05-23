-- Add user pricing/referral fields.
ALTER TABLE "User" ADD COLUMN IF NOT EXISTS "planTier" TEXT NOT NULL DEFAULT 'free';
ALTER TABLE "User" ADD COLUMN IF NOT EXISTS "referralCode" TEXT;
ALTER TABLE "User" ADD COLUMN IF NOT EXISTS "referredByUserId" TEXT;

CREATE UNIQUE INDEX IF NOT EXISTS "User_referralCode_key" ON "User"("referralCode");

-- Create non-periodic rewrite credit ledger.
CREATE TABLE IF NOT EXISTS "RewriteCredit" (
  "id" TEXT NOT NULL,
  "userId" TEXT NOT NULL,
  "source" TEXT NOT NULL,
  "amountGranted" INTEGER NOT NULL,
  "amountConsumed" INTEGER NOT NULL DEFAULT 0,
  "grantedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "expiresAt" TIMESTAMP(3),
  "stripeEventId" TEXT,
  CONSTRAINT "RewriteCredit_pkey" PRIMARY KEY ("id")
);

CREATE INDEX IF NOT EXISTS "RewriteCredit_userId_expiresAt_idx" ON "RewriteCredit"("userId", "expiresAt");
CREATE INDEX IF NOT EXISTS "RewriteCredit_stripeEventId_idx" ON "RewriteCredit"("stripeEventId");

ALTER TABLE "RewriteCredit" ADD CONSTRAINT "RewriteCredit_userId_fkey"
  FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- Create referral attribution and audit table.
CREATE TABLE IF NOT EXISTS "Referral" (
  "id" TEXT NOT NULL,
  "referrerId" TEXT NOT NULL,
  "refereeId" TEXT NOT NULL,
  "status" TEXT NOT NULL DEFAULT 'pending',
  "creditedAt" TIMESTAMP(3),
  "signupIpHash" TEXT,
  CONSTRAINT "Referral_pkey" PRIMARY KEY ("id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "Referral_refereeId_key" ON "Referral"("refereeId");
CREATE INDEX IF NOT EXISTS "Referral_referrerId_creditedAt_idx" ON "Referral"("referrerId", "creditedAt");

ALTER TABLE "Referral" ADD CONSTRAINT "Referral_referrerId_fkey"
  FOREIGN KEY ("referrerId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;
