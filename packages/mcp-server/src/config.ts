export type ConfigEnv = Record<string, string | undefined>;

export interface McpServerConfig {
  apiKey: string;
  baseUrl: string;
}

export const DEFAULT_BASE_URL = "https://replyinmyvoice.com";
export const MISSING_API_KEY_MESSAGE =
  "Set REPLY_IN_MY_VOICE_API_KEY env var. Get one at https://replyinmyvoice.com/app/keys";

export function readServerConfig(env: ConfigEnv = process.env): McpServerConfig {
  const apiKey = env.REPLY_IN_MY_VOICE_API_KEY?.trim();

  if (!apiKey) {
    throw new Error(MISSING_API_KEY_MESSAGE);
  }

  return {
    apiKey,
    baseUrl: normalizeBaseUrl(env.REPLY_IN_MY_VOICE_BASE_URL),
  };
}

function normalizeBaseUrl(value: string | undefined): string {
  const trimmed = value?.trim();

  if (!trimmed) {
    return DEFAULT_BASE_URL;
  }

  return trimmed.replace(/\/+$/, "");
}
