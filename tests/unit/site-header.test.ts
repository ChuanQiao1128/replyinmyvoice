import * as React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { AuthSession } from "../../lib/entra-auth";

const { entraAuthMock } = vi.hoisted(() => ({
  entraAuthMock: {
    getCurrentSession: vi.fn(),
  },
}));

vi.mock("../../lib/entra-auth", () => entraAuthMock);

import { SiteHeader } from "../../components/site-header";
import { getCurrentSession } from "../../lib/entra-auth";

const adminSession: AuthSession = {
  email: "admin@example.test",
  exp: 1_800_000_000,
  name: "Admin User",
  sub: "admin-sub",
};

const userSession: AuthSession = {
  email: "user@example.test",
  exp: 1_800_000_000,
  name: "Regular User",
  sub: "user-sub",
};

async function renderHeader() {
  return renderToStaticMarkup(await SiteHeader());
}

beforeEach(() => {
  process.env.ADMIN_EMAILS = "admin@example.test,admin-sub";
  vi.stubGlobal("React", React);
  vi.mocked(getCurrentSession).mockReset();
});

afterEach(() => {
  delete process.env.ADMIN_EMAILS;
  vi.unstubAllGlobals();
});

describe("SiteHeader", () => {
  it("shows an admin nav link only for signed-in admins", async () => {
    vi.mocked(getCurrentSession).mockResolvedValue(adminSession);

    const html = await renderHeader();

    expect(html).toContain('href="/admin"');
    expect(html).toContain(">Admin</a>");
  });

  it("hides the admin nav link from signed-in non-admins", async () => {
    vi.mocked(getCurrentSession).mockResolvedValue(userSession);

    const html = await renderHeader();

    expect(html).not.toContain('href="/admin"');
    expect(html).not.toContain(">Admin</a>");
  });

  it("hides the admin nav link from signed-out visitors", async () => {
    vi.mocked(getCurrentSession).mockResolvedValue(null);

    const html = await renderHeader();

    expect(html).not.toContain('href="/admin"');
    expect(html).not.toContain(">Admin</a>");
  });
});
