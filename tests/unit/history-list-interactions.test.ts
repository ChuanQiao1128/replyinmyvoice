import { Window } from "happy-dom";
import type { ComponentType, ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";

type Act = typeof import("react").act;
type CreateElement = typeof import("react").createElement;
type CreateRoot = typeof import("react-dom/client").createRoot;
type HistoryListProps = {
  demoItems?: {
    attemptId: string;
    status: string;
    preview: string;
    createdAt: string | null;
  }[];
  demoDetail?: {
    draft: string;
    rewrite: string;
    draftSignal: number | null;
    rewriteSignal: number | null;
  };
};
type HistoryListComponent = ComponentType<HistoryListProps>;

function installDom() {
  const testWindow = new Window({ url: "http://localhost/app/history" });

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
  vi.stubGlobal(
    "requestAnimationFrame",
    testWindow.requestAnimationFrame.bind(testWindow),
  );
  vi.stubGlobal(
    "cancelAnimationFrame",
    testWindow.cancelAnimationFrame.bind(testWindow),
  );

  (
    globalThis as typeof globalThis & {
      IS_REACT_ACT_ENVIRONMENT: boolean;
    }
  ).IS_REACT_ACT_ENVIRONMENT = true;

  return testWindow;
}

async function loadHistoryModules() {
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

  const [reactDom, history] = await Promise.all([
    import("react-dom/client"),
    import("../../components/app/history-list"),
  ]);

  return {
    act: react.act as Act,
    createElement: react.createElement as CreateElement,
    createRoot: reactDom.createRoot as CreateRoot,
    HistoryList: history.HistoryList as HistoryListComponent,
  };
}

function historyItem(index: number) {
  return {
    attemptId: `attempt-${index}`,
    status: "Succeeded",
    resultJson: JSON.stringify({ rewrittenText: `Rewrite ${index}` }),
    createdAt: `2026-06-${String(index).padStart(2, "0")}T12:00:00Z`,
  };
}

function jsonResponse(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { "Content-Type": "application/json" },
  });
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

describe("history list interactions", () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("loads the next rewrite history page after the first 20 items", async () => {
    installDom();
    const fetchSpy = vi
      .fn()
      .mockResolvedValueOnce(
        jsonResponse({
          page: 1,
          pageSize: 20,
          totalCount: 21,
          items: Array.from({ length: 20 }, (_, index) =>
            historyItem(index + 1),
          ),
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          page: 2,
          pageSize: 20,
          totalCount: 21,
          items: [historyItem(21)],
        }),
      );
    vi.stubGlobal("fetch", fetchSpy);

    const { act, createElement, createRoot, HistoryList } =
      await loadHistoryModules();

    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(HistoryList));
    });
    await flushReact(act);

    expect(fetchSpy.mock.calls[0]?.[0]).toBe(
      "/api/me/rewrites?page=1&pageSize=20",
    );
    expect(container.textContent).toContain("Rewrite 20");

    const loadMoreButton = buttonByText(container, "Load more");
    expect(loadMoreButton).toBeTruthy();

    await act(async () => {
      loadMoreButton?.dispatchEvent(
        new MouseEvent("click", { bubbles: true, cancelable: true }),
      );
    });
    await flushReact(act);

    expect(fetchSpy.mock.calls[1]?.[0]).toBe(
      "/api/me/rewrites?page=2&pageSize=20",
    );
    expect(container.textContent).toContain("Rewrite 21");

    await act(async () => {
      root.unmount();
    });
  });

  it("restores a pending delete when Undo is clicked before finalizing", async () => {
    vi.useFakeTimers();
    const testWindow = installDom();
    const fetchSpy = vi.fn();
    vi.stubGlobal("fetch", fetchSpy);

    const { act, createElement, createRoot, HistoryList } =
      await loadHistoryModules();

    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    const historyProps: HistoryListProps = {
      demoItems: [
        {
          attemptId: "attempt-undo",
          status: "Succeeded",
          preview: "Keep this rewrite",
          createdAt: "2026-06-11T12:00:00Z",
        },
      ],
      demoDetail: {
        draft: "Original draft",
        rewrite: "Keep this rewrite",
        draftSignal: null,
        rewriteSignal: null,
      },
    };

    await act(async () => {
      root.render(createElement(HistoryList, historyProps));
    });

    const rowButton = buttonByText(container, "Keep this rewrite");
    expect(rowButton).toBeTruthy();

    await act(async () => {
      rowButton?.dispatchEvent(
        new testWindow.MouseEvent("click", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });

    const deleteButton = buttonByText(container, "Delete");
    expect(deleteButton).toBeTruthy();

    await act(async () => {
      deleteButton?.dispatchEvent(
        new testWindow.MouseEvent("click", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });

    expect(container.textContent).not.toContain("Keep this rewrite");
    expect(container.textContent).toContain("Rewrite removed.");

    const undoButton = buttonByText(container, "Undo");
    expect(undoButton).toBeTruthy();

    await act(async () => {
      undoButton?.dispatchEvent(
        new testWindow.MouseEvent("click", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });

    expect(container.textContent).toContain("Keep this rewrite");

    await act(async () => {
      await vi.advanceTimersByTimeAsync(5000);
    });

    expect(fetchSpy).not.toHaveBeenCalled();

    await act(async () => {
      root.unmount();
    });
  });
});
