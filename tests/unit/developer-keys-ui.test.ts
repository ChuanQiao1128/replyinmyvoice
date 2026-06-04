import { readFileSync } from "node:fs";
import { join } from "node:path";

import { describe, expect, it } from "vitest";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

describe("developer key management UI source", () => {
  const pageSource = source("app/developers/keys/page.tsx");
  const panelSource = source("components/developers/api-keys-panel.tsx");
  const headerSource = source("components/site-header.tsx");

  it("adds a signed-in portal page using the shared header", () => {
    expect(pageSource).toContain(
      'from "../../../components/developers/api-keys-panel"',
    );
    expect(pageSource).toContain('from "../../../components/site-header"');
    expect(pageSource).toContain('export const dynamic = "force-dynamic"');
    expect(pageSource).toContain("<SiteHeader");
    expect(pageSource).toContain("<ApiKeysPanel");
    expect(pageSource).not.toContain("getAzureApiBaseUrl");
    expect(pageSource).not.toContain("getCurrentAccessToken");
  });

  it("loads, creates, and revokes keys through the existing UI routes", () => {
    expect(panelSource).toContain('fetch("/api/keys"');
    expect(panelSource).toContain('method: "POST"');
    expect(panelSource).toContain('method: "DELETE"');
    expect(panelSource).toContain("encodeURIComponent(key.id)");
    expect(panelSource).toContain("maskedKey");
    expect(panelSource).toContain("lastUsedAt");
    expect(panelSource).toContain("revokedAt");
    expect(panelSource).not.toContain("localStorage");
    expect(panelSource).not.toContain("sessionStorage");
  });

  it("keeps the one-time key reveal and revoke confirmation explicit", () => {
    expect(panelSource).toContain("Create key");
    expect(panelSource).toContain("Copy key");
    expect(panelSource).toContain("you won't see this again");
    expect(panelSource).toContain("Revoke");
    expect(panelSource).toContain("Confirm revoke");
    expect(panelSource).toContain("setRevealedKey(null)");
  });

  it("wires signed-in navigation to the key manager", () => {
    expect(headerSource).toContain('href="/developers/keys"');
    expect(headerSource).toContain("API keys");
  });
});
