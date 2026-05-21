import { afterEach, describe, expect, it } from "vitest";

import {
  getOpenAiCompatibleApiKey,
  getOpenAiCompatibleChatCompletionsUrl,
} from "../../lib/openai-compatible";

const originalEnv = {
  OPENAI_API_KEY: process.env.OPENAI_API_KEY,
  OPENAI_BASE_URL: process.env.OPENAI_BASE_URL,
  DEEPSEEK_API_KEY: process.env.DEEPSEEK_API_KEY,
};

afterEach(() => {
  for (const [key, value] of Object.entries(originalEnv)) {
    if (value === undefined) {
      delete process.env[key];
    } else {
      process.env[key] = value;
    }
  }
});

describe("OpenAI-compatible provider configuration", () => {
  it("defaults to the OpenAI chat completions URL", () => {
    delete process.env.OPENAI_BASE_URL;

    expect(getOpenAiCompatibleChatCompletionsUrl()).toBe(
      "https://api.openai.com/v1/chat/completions",
    );
  });

  it("normalizes a DeepSeek base URL to the chat completions endpoint", () => {
    process.env.OPENAI_BASE_URL = "https://api.deepseek.com";

    expect(getOpenAiCompatibleChatCompletionsUrl()).toBe(
      "https://api.deepseek.com/v1/chat/completions",
    );
  });

  it("accepts a base URL that already includes /v1", () => {
    process.env.OPENAI_BASE_URL = "https://api.deepseek.com/v1/";

    expect(getOpenAiCompatibleChatCompletionsUrl()).toBe(
      "https://api.deepseek.com/v1/chat/completions",
    );
  });

  it("uses DEEPSEEK_API_KEY for DeepSeek routing without requiring OPENAI_API_KEY", () => {
    delete process.env.OPENAI_API_KEY;
    process.env.OPENAI_BASE_URL = "https://api.deepseek.com";
    process.env.DEEPSEEK_API_KEY = "test-deepseek-key";

    expect(getOpenAiCompatibleApiKey()).toBe("test-deepseek-key");
  });

  it("uses OPENAI_API_KEY for default OpenAI routing", () => {
    delete process.env.OPENAI_BASE_URL;
    process.env.OPENAI_API_KEY = "test-openai-key";
    process.env.DEEPSEEK_API_KEY = "test-deepseek-key";

    expect(getOpenAiCompatibleApiKey()).toBe("test-openai-key");
  });
});
