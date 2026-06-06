import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function loadOpenApiSpec() {
  return JSON.parse(readFileSync(join(root, "public/openapi.json"), "utf8"));
}

describe("public/openapi.json", () => {
  it("is an OpenAPI 3.1 document for the async v1 API", () => {
    const spec = loadOpenApiSpec();

    expect(spec.openapi).toMatch(/^3\.1/);
    expect(spec.info).toMatchObject({
      title: "Reply In My Voice API",
      version: "1.0.0",
    });
    expect(spec.servers).toEqual([{ url: "https://replyinmyvoice.com" }]);
    expect(Object.keys(spec.paths).sort()).toEqual([
      "/api/v1/rewrite",
      "/api/v1/rewrite/{id}",
      "/api/v1/usage",
    ]);
  });

  it("defines shared auth, error, and result schemas", () => {
    const spec = loadOpenApiSpec();

    expect(spec.components.securitySchemes.ApiKeyBearer).toMatchObject({
      type: "http",
      scheme: "bearer",
      bearerFormat: "rmv_live API key",
    });
    expect(spec.components.schemas.Error.required).toEqual(["error"]);
    expect(spec.components.schemas.Error.properties.error.required).toEqual([
      "code",
      "message",
    ]);
    expect(spec.components.schemas.RewriteResult.oneOf).toEqual([
      { $ref: "#/components/schemas/RewriteProcessing" },
      { $ref: "#/components/schemas/RewriteSucceeded" },
      { $ref: "#/components/schemas/RewriteFailed" },
    ]);
  });

  it("documents idempotency and rate-limit headers", () => {
    const spec = loadOpenApiSpec();

    expect(spec.paths["/api/v1/rewrite"].post.parameters).toContainEqual({
      $ref: "#/components/parameters/IdempotencyKey",
    });

    for (const method of [
      spec.paths["/api/v1/rewrite"].post,
      spec.paths["/api/v1/rewrite/{id}"].get,
      spec.paths["/api/v1/usage"].get,
    ]) {
      expect(method.security).toEqual([{ ApiKeyBearer: [] }]);
      for (const response of Object.values(method.responses)) {
        expect(response).toHaveProperty("headers.X-RateLimit-Limit");
        expect(response).toHaveProperty("headers.X-RateLimit-Remaining");
        expect(response).toHaveProperty("headers.X-RateLimit-Reset");
      }
    }

    expect(spec.paths["/api/v1/rewrite"].post.responses["429"].headers).toHaveProperty(
      "Retry-After",
    );
  });
});
