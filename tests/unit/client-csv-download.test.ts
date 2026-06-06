import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { downloadCsvFile } from "../../lib/client-csv-download";

const objectUrl = "blob:https://replyinmyvoice.com/unit-csv";

function fetchMock() {
  return vi.mocked(globalThis.fetch);
}

function urlMock() {
  return vi.mocked(globalThis.URL);
}

function documentMock() {
  return globalThis.document as unknown as {
    body: {
      appendChild: ReturnType<typeof vi.fn>;
    };
    createElement: ReturnType<typeof vi.fn>;
  };
}

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
  vi.stubGlobal("URL", {
    createObjectURL: vi.fn(() => objectUrl),
    revokeObjectURL: vi.fn(),
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("client csv download helper", () => {
  it("fetches the route as a blob and triggers a temporary download link", async () => {
    const click = vi.fn();
    const remove = vi.fn();
    const link = {
      click,
      download: "",
      href: "",
      remove,
      style: {
        display: "",
      },
    };
    vi.stubGlobal("document", {
      body: {
        appendChild: vi.fn(),
      },
      createElement: vi.fn(() => link),
    });
    fetchMock().mockResolvedValueOnce(
      new Response("createdAt,statusCode\n2026-06-05,200", {
        headers: {
          "content-type": "text/csv",
        },
        status: 200,
      }),
    );

    await downloadCsvFile({
      filename: "api-usage.csv",
      path: "/api/me/api-usage/export?limit=1000",
    });

    expect(fetchMock()).toHaveBeenCalledWith("/api/me/api-usage/export?limit=1000", {
      cache: "no-store",
    });
    expect(documentMock().createElement).toHaveBeenCalledWith("a");
    expect(documentMock().body.appendChild).toHaveBeenCalledWith(link);
    expect(urlMock().createObjectURL).toHaveBeenCalledWith(expect.any(Blob));
    expect(link.href).toBe(objectUrl);
    expect(link.download).toBe("api-usage.csv");
    expect(link.style.display).toBe("none");
    expect(click).toHaveBeenCalledTimes(1);
    expect(remove).toHaveBeenCalledTimes(1);
    expect(urlMock().revokeObjectURL).toHaveBeenCalledWith(objectUrl);
  });

  it("throws the route error without creating a blob download", async () => {
    vi.stubGlobal("document", {
      body: {
        appendChild: vi.fn(),
      },
      createElement: vi.fn(),
    });
    fetchMock().mockResolvedValueOnce(
      Response.json({ error: "Cross-origin request rejected." }, { status: 403 }),
    );

    await expect(
      downloadCsvFile({
        filename: "billing-history.csv",
        path: "/api/me/billing/export",
      }),
    ).rejects.toThrow("Cross-origin request rejected.");
    expect(urlMock().createObjectURL).not.toHaveBeenCalled();
    expect(documentMock().createElement).not.toHaveBeenCalled();
  });
});
