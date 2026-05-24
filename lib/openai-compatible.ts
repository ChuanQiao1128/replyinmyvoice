import { optionalEnv, requireEnv } from "./env";

const DEFAULT_OPENAI_BASE_URL = "https://api.openai.com/v1";

function trimTrailingSlashes(value: string) {
  return value.replace(/\/+$/g, "");
}

export function isDeepSeekCompatibleBaseUrl(value = getOpenAiCompatibleBaseUrl()) {
  try {
    const hostname = new URL(value).hostname.toLowerCase();
    return hostname === "api.deepseek.com" || hostname.endsWith(".deepseek.com");
  } catch {
    return false;
  }
}

export function getOpenAiCompatibleBaseUrl() {
  return trimTrailingSlashes(
    optionalEnv("OPENAI_BASE_URL", DEFAULT_OPENAI_BASE_URL),
  );
}

export function getOpenAiCompatibleChatCompletionsUrl() {
  const baseUrl = getOpenAiCompatibleBaseUrl();

  if (baseUrl.endsWith("/chat/completions")) {
    return baseUrl;
  }

  if (baseUrl.endsWith("/v1")) {
    return `${baseUrl}/chat/completions`;
  }

  return `${baseUrl}/v1/chat/completions`;
}

export function getOpenAiCompatibleApiKey() {
  const baseUrl = getOpenAiCompatibleBaseUrl();
  const deepSeekApiKey = optionalEnv("DEEPSEEK_API_KEY");

  if (deepSeekApiKey && isDeepSeekCompatibleBaseUrl(baseUrl)) {
    return deepSeekApiKey;
  }

  return requireEnv("OPENAI_API_KEY");
}
