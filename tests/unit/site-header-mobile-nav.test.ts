import { readFileSync } from "node:fs";
import { join } from "node:path";

import { Window } from "happy-dom";
import * as React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

type Act = typeof import("react").act;

const { entraAuthMock } = vi.hoisted(() => ({
  entraAuthMock: {
    getCurrentSession: vi.fn(),
  },
}));

vi.mock("../../lib/entra-auth", () => entraAuthMock);

import { SiteHeader } from "../../components/site-header";
import { getCurrentSession } from "../../lib/entra-auth";

const root = process.cwd();

function source(path: string) {
  return readFileSync(join(root, path), "utf8");
}

async function renderHeader() {
  return renderToStaticMarkup(await SiteHeader());
}

function installDom() {
  const testWindow = new Window({ url: "http://localhost/" });

  vi.stubGlobal("window", testWindow);
  vi.stubGlobal("self", testWindow);
  vi.stubGlobal("document", testWindow.document);
  vi.stubGlobal("navigator", testWindow.navigator);
  vi.stubGlobal("HTMLElement", testWindow.HTMLElement);
  vi.stubGlobal("Element", testWindow.Element);
  vi.stubGlobal("Node", testWindow.Node);
  vi.stubGlobal("Text", testWindow.Text);
  vi.stubGlobal("Event", testWindow.Event);
  vi.stubGlobal("MouseEvent", testWindow.MouseEvent);
  vi.stubGlobal("KeyboardEvent", testWindow.KeyboardEvent);

  (
    globalThis as typeof globalThis & {
      IS_REACT_ACT_ENVIRONMENT: boolean;
    }
  ).IS_REACT_ACT_ENVIRONMENT = true;

  return testWindow;
}

async function renderInteractiveHeader() {
  const react = await import("react");
  vi.stubGlobal("React", react);
  const reactDom = await import("react-dom/client");

  const container = document.createElement("div");
  document.body.append(container);
  const rootInstance = reactDom.createRoot(container);

  await (react.act as Act)(async () => {
    rootInstance.render(await SiteHeader());
  });

  return {
    act: react.act as Act,
    container,
    rootInstance,
  };
}

async function flushReact(act: Act) {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

beforeEach(() => {
  vi.stubGlobal("React", React);
  vi.mocked(getCurrentSession).mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.resetModules();
});

describe("SiteHeader mobile navigation", () => {
  it("renders a button-driven mobile menu in the shared header", async () => {
    const headerSource = source("components/site-header.tsx");
    vi.mocked(getCurrentSession).mockResolvedValue(null);

    const html = await renderHeader();

    expect(headerSource).toContain("SiteHeaderMobileMenu");
    expect(headerSource).not.toContain("<details");
    expect(headerSource).not.toContain("<summary");
    expect(html).toContain('class="mobile-nav-menu"');
    expect(html).toContain("<button");
    expect(html).toContain('aria-expanded="false"');
    expect(html).toContain('aria-controls="site-mobile-nav-panel"');
    expect(html).not.toContain(">+<");
  });

  it("keeps Sign in reachable for signed-out visitors", async () => {
    vi.mocked(getCurrentSession).mockResolvedValue(null);

    const html = await renderHeader();

    expect(html).toContain('href="/sign-in"');
    expect(html).toContain(">Sign in</a>");
  });

  it("opens with focus in the panel and closes with Escape", async () => {
    const testWindow = installDom();
    vi.mocked(getCurrentSession).mockResolvedValue(null);

    const { act, container, rootInstance } = await renderInteractiveHeader();
    const trigger = container.querySelector<HTMLButtonElement>(
      ".mobile-nav-trigger",
    );
    const panel = container.querySelector<HTMLElement>("#site-mobile-nav-panel");

    expect(trigger).toBeTruthy();
    expect(panel).toBeTruthy();
    expect(trigger?.getAttribute("aria-expanded")).toBe("false");
    expect(panel?.hasAttribute("hidden")).toBe(true);

    await act(async () => {
      trigger?.dispatchEvent(
        new testWindow.MouseEvent("click", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });
    await flushReact(act);

    const firstLink = panel?.querySelector<HTMLAnchorElement>('a[href="/pricing"]');
    expect(trigger?.getAttribute("aria-expanded")).toBe("true");
    expect(panel?.hasAttribute("hidden")).toBe(false);
    expect(document.activeElement).toBe(firstLink);

    await act(async () => {
      document.dispatchEvent(
        new testWindow.KeyboardEvent("keydown", {
          bubbles: true,
          cancelable: true,
          key: "Escape",
        }) as unknown as Event,
      );
    });
    await flushReact(act);

    expect(trigger?.getAttribute("aria-expanded")).toBe("false");
    expect(panel?.hasAttribute("hidden")).toBe(true);
    expect(document.activeElement).toBe(trigger);

    await act(async () => {
      rootInstance.unmount();
    });
  });

  it("replaces the narrow-screen blanket link hide with mobile menu CSS", () => {
    const globals = source("app/globals.css");

    expect(globals).toContain(".mobile-nav-menu");
    expect(globals).toContain(".mobile-nav-panel");
    expect(globals).toContain(".mobile-nav-panel[hidden]");
    expect(globals).not.toMatch(/\.mobile-nav-trigger::-[\w-]*marker/);
    expect(globals).not.toMatch(/\.mobile-nav-trigger::marker/);
    expect(globals).not.toMatch(
      /\.nav-links\s+a:not\(\.btn\)\s*\{[\s\S]*?display\s*:\s*none[\s\S]*?\}/,
    );
    expect(globals).toMatch(
      /@media\s*\(max-width:\s*680px\)\s*\{[\s\S]*?\.nav-inline-links\s*\{[\s\S]*?display\s*:\s*none/,
    );
    expect(globals).toMatch(
      /@media\s*\(max-width:\s*680px\)\s*\{[\s\S]*?\.mobile-nav-menu\s*\{[\s\S]*?display\s*:\s*block/,
    );
  });
});
