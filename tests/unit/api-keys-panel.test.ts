import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

import {
  CreatedKeyReveal,
  MaskedApiKeyValue,
  createdKeyListItem,
} from "../../components/developers/api-keys-panel";

describe("API keys panel rendering", () => {
  it("renders a created key once, then renders the list value masked", () => {
    const created = {
      createdAt: "2026-06-08T00:00:00.000Z",
      id: "key_created",
      isTest: false,
      key: "rmv_live_abcdefghijklmnopqrstuvwxyz1234567890ABCD",
      name: "Production server",
    };

    const revealHtml = renderToStaticMarkup(
      createElement(CreatedKeyReveal, {
        copyNotice: null,
        onCopy: vi.fn(),
        onDone: vi.fn(),
        revealedKey: created,
      }),
    );

    expect(revealHtml).toContain(created.key);
    expect(revealHtml).toContain("Copy key");

    const maskedKey = createdKeyListItem(created);
    const maskedHtml = renderToStaticMarkup(
      createElement(MaskedApiKeyValue, { apiKey: maskedKey }),
    );

    expect(maskedKey.maskedKey).toBe(`rmv_live_${"\u2022".repeat(4)}ABCD`);
    expect(maskedHtml).toContain("rmv_live_");
    expect(maskedHtml).toContain("ABCD");
    expect(maskedHtml).not.toContain(created.key);
  });
});
