import { neon, neonConfig } from "@neondatabase/serverless";

import { requireEnv } from "./env";

type SqlClient = ReturnType<typeof neon>;

let sqlClient: SqlClient | null = null;

export function getSql(): SqlClient {
  if (!sqlClient) {
    if (typeof WebSocket !== "undefined") {
      neonConfig.webSocketConstructor = WebSocket;
    }

    sqlClient = neon(requireEnv("DATABASE_URL"));
  }

  return sqlClient;
}

export function createId(): string {
  return crypto.randomUUID();
}

export function nullableDate(value: unknown): Date | null {
  if (!value) {
    return null;
  }

  if (value instanceof Date) {
    return value;
  }

  const date = new Date(String(value));
  return Number.isNaN(date.getTime()) ? null : date;
}

export function requiredDate(value: unknown): Date {
  return nullableDate(value) ?? new Date();
}

export async function dbHealthCheck() {
  await getSql()`SELECT 1`;
}
