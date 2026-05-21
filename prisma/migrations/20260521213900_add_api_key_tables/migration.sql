-- Add ApiKey + ApiKeyUsage tables.
CREATE TABLE IF NOT EXISTS "ApiKey" (
  "id" TEXT NOT NULL,
  "userId" TEXT NOT NULL,
  "keyHash" TEXT NOT NULL,
  "name" TEXT NOT NULL,
  "planTier" TEXT NOT NULL DEFAULT 'free',
  "scope" TEXT NOT NULL DEFAULT '[]',
  "rateLimitPerMinute" INTEGER NOT NULL DEFAULT 60,
  "monthlyQuota" INTEGER NOT NULL DEFAULT 1000,
  "currentPeriodUsage" INTEGER NOT NULL DEFAULT 0,
  "currentPeriodStartedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "lastUsedAt" TIMESTAMP(3),
  "expiresAt" TIMESTAMP(3),
  "revokedAt" TIMESTAMP(3),
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "updatedAt" TIMESTAMP(3) NOT NULL,
  CONSTRAINT "ApiKey_pkey" PRIMARY KEY ("id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "ApiKey_keyHash_key" ON "ApiKey"("keyHash");
CREATE INDEX IF NOT EXISTS "ApiKey_userId_createdAt_idx" ON "ApiKey"("userId","createdAt");
CREATE INDEX IF NOT EXISTS "ApiKey_planTier_idx" ON "ApiKey"("planTier");
ALTER TABLE "ApiKey" ADD CONSTRAINT "ApiKey_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

CREATE TABLE IF NOT EXISTS "ApiKeyUsage" (
  "id" TEXT NOT NULL,
  "apiKeyId" TEXT NOT NULL,
  "requestId" TEXT NOT NULL,
  "endpoint" TEXT NOT NULL,
  "statusCode" INTEGER NOT NULL,
  "latencyMs" INTEGER,
  "costUsdEstimate" DECIMAL(65,30) NOT NULL DEFAULT 0,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "ApiKeyUsage_pkey" PRIMARY KEY ("id")
);
CREATE INDEX IF NOT EXISTS "ApiKeyUsage_apiKeyId_createdAt_idx" ON "ApiKeyUsage"("apiKeyId","createdAt");
CREATE INDEX IF NOT EXISTS "ApiKeyUsage_endpoint_idx" ON "ApiKeyUsage"("endpoint");
CREATE INDEX IF NOT EXISTS "ApiKeyUsage_createdAt_idx" ON "ApiKeyUsage"("createdAt");
ALTER TABLE "ApiKeyUsage" ADD CONSTRAINT "ApiKeyUsage_apiKeyId_fkey" FOREIGN KEY ("apiKeyId") REFERENCES "ApiKey"("id") ON DELETE CASCADE ON UPDATE CASCADE;
