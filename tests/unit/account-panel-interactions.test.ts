import { Window } from "happy-dom";
import type { ComponentType, ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";

type Act = typeof import("react").act;
type CreateElement = typeof import("react").createElement;
type CreateRoot = typeof import("react-dom/client").createRoot;
type AccountPanelProps =
  NonNullable<Parameters<typeof import("../../components/account/account-panel").AccountPanel>[0]>;

function installDom() {
  const testWindow = new Window({ url: "http://localhost/app/account" });

  vi.stubGlobal("window", testWindow);
  vi.stubGlobal("self", testWindow);
  vi.stubGlobal("document", testWindow.document);
  vi.stubGlobal("navigator", testWindow.navigator);
  vi.stubGlobal("HTMLElement", testWindow.HTMLElement);
  vi.stubGlobal("HTMLTextAreaElement", testWindow.HTMLTextAreaElement);
  vi.stubGlobal("HTMLAnchorElement", testWindow.HTMLAnchorElement);
  vi.stubGlobal("Element", testWindow.Element);
  vi.stubGlobal("Node", testWindow.Node);
  vi.stubGlobal("Text", testWindow.Text);
  vi.stubGlobal("Event", testWindow.Event);
  vi.stubGlobal("MouseEvent", testWindow.MouseEvent);
  vi.stubGlobal(
    "requestAnimationFrame",
    testWindow.requestAnimationFrame.bind(testWindow),
  );
  vi.stubGlobal(
    "cancelAnimationFrame",
    testWindow.cancelAnimationFrame.bind(testWindow),
  );
  vi.stubGlobal("URL", testWindow.URL);

  (
    globalThis as typeof globalThis & {
      IS_REACT_ACT_ENVIRONMENT: boolean;
    }
  ).IS_REACT_ACT_ENVIRONMENT = true;

  return testWindow;
}

async function loadAccountModules() {
  const react = await import("react");
  vi.stubGlobal("React", react);
  vi.doMock("next/link", () => ({
    default: ({
      href,
      children,
      ...props
    }: {
      href: string;
      children: ReactNode;
    }) => react.createElement("a", { href, ...props }, children),
  }));

  const [reactDom, accountPanel] = await Promise.all([
    import("react-dom/client"),
    import("../../components/account/account-panel"),
  ]);

  return {
    act: react.act as Act,
    createElement: react.createElement as CreateElement,
    createRoot: reactDom.createRoot as CreateRoot,
    AccountPanel: accountPanel.AccountPanel as ComponentType<AccountPanelProps>,
  };
}

function demoBundle(): AccountPanelProps["demoBundle"] {
  return {
    account: {
      currentPeriodEnd: null,
      email: "buyer@example.test",
      externalAuthUserId: "external-buyer-1",
      paymentGraceEndsAt: null,
      subscriptionStatus: "inactive",
      usage: {
        exhausted: false,
        periodEnd: null,
        periodKey: "free:lifetime",
        quota: 3,
        remaining: 3,
        reserved: 0,
        scope: "free",
        used: 0,
      },
      userId: "user_buyer",
    },
    payments: [],
    supportRequests: [],
  };
}

function buttonByText(container: HTMLElement, text: string) {
  return Array.from(container.querySelectorAll("button")).find((button) =>
    button.textContent?.includes(text),
  );
}

async function flushReact(act: Act) {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

function jsonResponse(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), {
    headers: { "Content-Type": "application/json" },
    status,
  });
}

describe("account panel interactions", () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("holds billing support submit briefly after one request", async () => {
    vi.useFakeTimers();
    const testWindow = installDom();
    const fetchSpy = vi.fn().mockResolvedValueOnce(
      jsonResponse({
        createdAt: "2026-06-02T00:00:00Z",
        id: "support-created",
        message: "Please check this purchase.",
        relatedPaymentIntentId: null,
        resolvedAt: null,
        status: "open",
        type: "billing-question",
        updatedAt: "2026-06-02T00:00:00Z",
        userId: "user_buyer",
      }),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { act, createElement, createRoot, AccountPanel } =
      await loadAccountModules();
    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(AccountPanel, { demoBundle: demoBundle() }));
    });

    const messageField = container.querySelector("textarea");
    expect(messageField).toBeTruthy();
    await act(async () => {
      const valueSetter = Object.getOwnPropertyDescriptor(
        testWindow.HTMLTextAreaElement.prototype,
        "value",
      )?.set;
      valueSetter?.call(messageField, "Please check this purchase.");
      messageField!.dispatchEvent(
        new testWindow.Event("input", { bubbles: true }) as unknown as Event,
      );
    });

    const form = container.querySelector("form");
    await act(async () => {
      form?.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
    });
    await flushReact(act);

    expect(fetchSpy).toHaveBeenCalledTimes(1);
    expect(buttonByText(container, "Send request")?.disabled).toBe(true);
    expect(container.textContent).toContain(
      "One request at a time. You can send another request in a moment.",
    );

    await act(async () => {
      await vi.advanceTimersByTimeAsync(2499);
    });
    expect(buttonByText(container, "Send request")?.disabled).toBe(true);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
    });
    expect(buttonByText(container, "Send request")?.disabled).toBe(false);

    await act(async () => {
      root.unmount();
    });
  });

  it("exports rewrite history from the delete confirmation dialog", async () => {
    const testWindow = installDom();
    const anchorClick = vi.fn();
    Object.defineProperty(testWindow.HTMLAnchorElement.prototype, "click", {
      configurable: true,
      value: anchorClick,
    });
    testWindow.URL.createObjectURL = vi.fn(() => "blob:rewrite-history");
    testWindow.URL.revokeObjectURL = vi.fn();

    const fetchSpy = vi.fn().mockResolvedValueOnce(
      jsonResponse({
        items: [
          {
            attemptId: "attempt-1",
            createdAt: "2026-06-11T00:00:00Z",
            preview: "Saved rewrite",
            status: "Succeeded",
          },
        ],
        page: 1,
        pageSize: 1000,
        totalCount: 1,
      }),
    );
    vi.stubGlobal("fetch", fetchSpy);

    const { act, createElement, createRoot, AccountPanel } =
      await loadAccountModules();
    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(AccountPanel, { demoBundle: demoBundle() }));
    });

    await act(async () => {
      buttonByText(container, "Delete account")?.dispatchEvent(
        new testWindow.MouseEvent("click", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });

    const exportButton = buttonByText(container, "Export my rewrite history");
    expect(exportButton).toBeTruthy();

    await act(async () => {
      exportButton?.dispatchEvent(
        new testWindow.MouseEvent("click", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });
    await flushReact(act);

    expect(fetchSpy).toHaveBeenCalledWith(
      "/api/me/rewrites?page=1&pageSize=1000",
      { cache: "no-store" },
    );
    expect(testWindow.URL.createObjectURL).toHaveBeenCalledWith(expect.any(Blob));
    expect(anchorClick).toHaveBeenCalledTimes(1);
    expect(testWindow.URL.revokeObjectURL).toHaveBeenCalledWith(
      "blob:rewrite-history",
    );

    await act(async () => {
      root.unmount();
    });
  });
});
