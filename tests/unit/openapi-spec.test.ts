import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function loadOpenApiSpec() {
  return JSON.parse(readFileSync(join(root, "public/openapi.json"), "utf8"));
}

function loadOpenApiText() {
  return readFileSync(join(root, "public/openapi.json"), "utf8");
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
    expect(spec.components.schemas.RewriteResult.discriminator).toEqual({
      propertyName: "status",
      mapping: {
        failed: "#/components/schemas/RewriteFailed",
        processing: "#/components/schemas/RewriteProcessing",
        succeeded: "#/components/schemas/RewriteSucceeded",
      },
    });
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
      for (const [status, response] of Object.entries(method.responses)) {
        if (status === "401") {
          expect(response).not.toHaveProperty("headers.X-RateLimit-Limit");
          expect(response).not.toHaveProperty("headers.X-RateLimit-Remaining");
          expect(response).not.toHaveProperty("headers.X-RateLimit-Reset");
          continue;
        }

        expect(response).toHaveProperty("headers.X-RateLimit-Limit");
        expect(response).toHaveProperty("headers.X-RateLimit-Remaining");
        expect(response).toHaveProperty("headers.X-RateLimit-Reset");
      }
    }

    expect(spec.paths["/api/v1/rewrite"].post.responses["429"].headers).toHaveProperty(
      "Retry-After",
    );
  });

  it("documents UUID rewrite ids and omits stale rewrite examples", () => {
    const spec = loadOpenApiSpec();
    const specText = loadOpenApiText();
    const uuidSchema = {
      type: "string",
      format: "uuid",
    };

    expect(spec.components.parameters.RewriteId.schema).toMatchObject(uuidSchema);
    expect(spec.components.schemas.RewriteSubmitAccepted.properties.id).toMatchObject(uuidSchema);
    expect(spec.components.schemas.RewriteProcessing.properties.id).toMatchObject(uuidSchema);
    expect(spec.components.schemas.RewriteSucceeded.properties.id).toMatchObject(uuidSchema);
    expect(spec.components.schemas.RewriteFailed.properties.id).toMatchObject(uuidSchema);
    expect(specText).not.toContain("rw_123");
    expect(specText).not.toContain("rewrite_failed");
    expect(spec.paths).not.toHaveProperty("/api/v1/openapi");
  });

  it("matches request trimming and nullable usage period behavior", () => {
    const spec = loadOpenApiSpec();
    const submitRequest = spec.components.schemas.RewriteSubmitRequest;

    expect(submitRequest).not.toHaveProperty("additionalProperties", false);
    expect(submitRequest.properties.draft).not.toHaveProperty("minLength");
    expect(submitRequest.properties.draft).not.toHaveProperty("maxLength");
    expect(submitRequest.properties.draft.description.toLowerCase()).toContain("after trimming");
    expect(spec.components.schemas.Usage.properties.periodEnd).toEqual({
      type: ["string", "null"],
      format: "date-time",
    });
  });
});
