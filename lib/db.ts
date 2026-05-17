import { neonConfig } from "@neondatabase/serverless";
import { PrismaNeon } from "@prisma/adapter-neon";
import { PrismaClient } from "@prisma/client";

import { requireEnv } from "./env";

type PrismaGlobal = typeof globalThis & {
  replyInMyVoicePrisma?: PrismaClient;
};

function createPrismaClient(): PrismaClient {
  const connectionString = requireEnv("DATABASE_URL");

  if (typeof WebSocket !== "undefined") {
    neonConfig.webSocketConstructor = WebSocket;
  }

  const adapter = new PrismaNeon({ connectionString });
  return new PrismaClient({ adapter });
}

const globalForPrisma = globalThis as PrismaGlobal;

export const prisma =
  globalForPrisma.replyInMyVoicePrisma ?? createPrismaClient();

if (process.env.NODE_ENV !== "production") {
  globalForPrisma.replyInMyVoicePrisma = prisma;
}
