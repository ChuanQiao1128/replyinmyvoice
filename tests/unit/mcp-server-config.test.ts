import { describe, expect, it } from "vitest";

import {
  MISSING_API_KEY_MESSAGE,
  readServerConfig,
} from "../../packages/mcp-server/src/config";

describe("MCP server configuration", () => {
  it("throws a clear setup error when the API key env var is missing", () => {
    expect(() => readServerConfig({})).toThrow(MISSING_API_KEY_MESSAGE);
  });

  it("points users to the current app API key page", () => {
    expect(MISSING_API_KEY_MESSAGE).toContain("https://replyinmyvoice.com/app/keys");
    expect(MISSING_API_KEY_MESSAGE).not.toContain("app/api-keys");
  });

  it("reads the API key and defaults to the production API base URL", () => {
    expect(
      readServerConfig({
        REPLY_IN_MY_VOICE_API_KEY: "  rmv_test_123  ",
      }),
    ).toEqual({
      apiKey: "rmv_test_123",
      baseUrl: "https://replyinmyvoice.com",
    });
  });

  it("uses the development base URL override without trailing slashes", () => {
    expect(
      readServerConfig({
        REPLY_IN_MY_VOICE_API_KEY: "rmv_test_123",
        REPLY_IN_MY_VOICE_BASE_URL: " http://localhost:3000/// ",
      }),
    ).toEqual({
      apiKey: "rmv_test_123",
      baseUrl: "http://localhost:3000",
    });
  });
});
