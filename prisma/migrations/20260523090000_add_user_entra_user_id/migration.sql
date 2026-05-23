-- Add nullable Entra External ID user mapping for the Clerk-to-Entra migration.
ALTER TABLE "User" ADD COLUMN IF NOT EXISTS "entraUserId" TEXT;

CREATE UNIQUE INDEX IF NOT EXISTS "User_entraUserId_key" ON "User"("entraUserId");
