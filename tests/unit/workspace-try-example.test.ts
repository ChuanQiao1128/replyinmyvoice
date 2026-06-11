import { Window } from "happy-dom";
import { afterEach, describe, expect, it, vi } from "vitest";

type Act = typeof import("react").act;
type CreateElement = typeof import("react").createElement;
type CreateRoot = typeof import("react-dom/client").createRoot;

function installDom() {
  const testWindow = new Window({ url: "http://localhost/app" });

  vi.stubGlobal("window", testWindow);
  vi.stubGlobal("self", testWindow);
  vi.stubGlobal("document", testWindow.document);
  vi.stubGlobal("navigator", testWindow.navigator);
  vi.stubGlobal("HTMLElement", testWindow.HTMLElement);
  vi.stubGlobal("HTMLTextAreaElement", testWindow.HTMLTextAreaElement);
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

async function loadWorkspaceModules() {
  const react = await import("react");
  vi.stubGlobal("React", react);
  vi.doMock("next/navigation", () => ({
    useRouter: () => ({
      refresh: vi.fn(),
    }),
  }));

  const [reactDom, workspace, samples] = await Promise.all([
    import("react-dom/client"),
    import("../../components/app/rewrite-workspace"),
    import("../../components/landing/sample-cases"),
  ]);

  return {
    act: react.act as Act,
    createElement: react.createElement as CreateElement,
    createRoot: reactDom.createRoot as CreateRoot,
    RewriteWorkspace: workspace.RewriteWorkspace,
    homepageSampleCases: samples.homepageSampleCases,
  };
}

const workspaceProps = {
  appExperience: "ok" as const,
  canRedeem: false,
  checkoutStatus: null,
  outOfCredits: false,
  usageLabel: "3 rewrites remaining",
  subscriptionStatus: "Active",
  paymentGraceEndsAt: null,
  paid: false,
  quota: 3,
  planRemaining: 3,
  promoState: {
    hasRedeemed: true,
    trialExpiresAt: null,
    trialRemaining: 3,
  },
  rewriteHistoryUserKey: "test-user",
  remaining: 3,
  usageExhausted: false,
};

function buttonByText(container: HTMLElement, text: string) {
  return Array.from(container.querySelectorAll("button")).find((button) =>
    button.textContent?.includes(text),
  );
}

describe("workspace Try an example", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.resetModules();
  });

  it("populates the draft from a curated sample without starting a rewrite", async () => {
    const testWindow = installDom();
    const fetchSpy = vi.fn();
    vi.stubGlobal("fetch", fetchSpy);

    const {
      act,
      createElement,
      createRoot,
      RewriteWorkspace,
      homepageSampleCases,
    } = await loadWorkspaceModules();

    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(RewriteWorkspace, workspaceProps));
    });

    const tryExampleButton = buttonByText(container, "Try an example");
    expect(tryExampleButton).toBeTruthy();

    await act(async () => {
      const clickEvent = new testWindow.MouseEvent("click", {
        bubbles: true,
        cancelable: true,
      }) as unknown as Event;
      tryExampleButton?.dispatchEvent(clickEvent);
    });

    const textarea =
      container.querySelector<HTMLTextAreaElement>("#roughDraftReply");

    expect(textarea?.value).toBe(homepageSampleCases[0].draft);
    expect(fetchSpy).not.toHaveBeenCalled();

    await act(async () => {
      root.unmount();
    });
  });
});
