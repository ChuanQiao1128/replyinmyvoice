import { Window } from "happy-dom";
import type { ComponentType, ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";

type Act = typeof import("react").act;
type CreateElement = typeof import("react").createElement;
type CreateRoot = typeof import("react-dom/client").createRoot;
type AuthModule = typeof import("../../components/auth/google-oauth-card");
type SignInAuthPageProps = Parameters<AuthModule["SignInAuthPage"]>[0];
type SignUpAuthPageProps = Parameters<AuthModule["SignUpAuthPage"]>[0];
type SignInAuthPageComponent = ComponentType<SignInAuthPageProps>;
type SignUpAuthPageComponent = ComponentType<SignUpAuthPageProps>;
type TurnstileCallback = (token: string) => void;
type TestWindow = Window & {
  turnstile?: {
    remove: ReturnType<typeof vi.fn>;
    render: ReturnType<typeof vi.fn>;
    reset: ReturnType<typeof vi.fn>;
  };
};

function installDom(pathname: string) {
  const testWindow = new Window({ url: `http://localhost${pathname}` }) as TestWindow;

  vi.stubGlobal("window", testWindow);
  vi.stubGlobal("self", testWindow);
  vi.stubGlobal("document", testWindow.document);
  vi.stubGlobal("navigator", testWindow.navigator);
  vi.stubGlobal("HTMLElement", testWindow.HTMLElement);
  vi.stubGlobal("HTMLInputElement", testWindow.HTMLInputElement);
  vi.stubGlobal("HTMLAnchorElement", testWindow.HTMLAnchorElement);
  vi.stubGlobal("Element", testWindow.Element);
  vi.stubGlobal("Node", testWindow.Node);
  vi.stubGlobal("Text", testWindow.Text);
  vi.stubGlobal("Event", testWindow.Event);
  vi.stubGlobal("MouseEvent", testWindow.MouseEvent);
  vi.stubGlobal("URL", testWindow.URL);

  (
    globalThis as typeof globalThis & {
      IS_REACT_ACT_ENVIRONMENT: boolean;
    }
  ).IS_REACT_ACT_ENVIRONMENT = true;

  return testWindow;
}

async function loadAuthModules() {
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

  const [reactDom, auth] = await Promise.all([
    import("react-dom/client"),
    import("../../components/auth/google-oauth-card"),
  ]);

  return {
    act: react.act as Act,
    createElement: react.createElement as CreateElement,
    createRoot: reactDom.createRoot as CreateRoot,
    SignInAuthPage: auth.SignInAuthPage as SignInAuthPageComponent,
    SignUpAuthPage: auth.SignUpAuthPage as SignUpAuthPageComponent,
  };
}

function installTurnstile(testWindow: TestWindow, token: string | null) {
  testWindow.turnstile = {
    remove: vi.fn(),
    render: vi.fn((_container: HTMLElement, options: { callback: TurnstileCallback }) => {
      if (token) {
        options.callback(token);
      }
      return "turnstile-widget-id";
    }),
    reset: vi.fn(),
  };
}

function jsonResponse(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), {
    headers: { "Content-Type": "application/json" },
    status,
  });
}

function inputByName(container: HTMLElement, name: string) {
  const input = container.querySelector<HTMLInputElement>(`input[name="${name}"]`);
  expect(input).toBeTruthy();
  return input!;
}

function setInputValue(testWindow: Window, input: HTMLInputElement, value: string) {
  const valueSetter = Object.getOwnPropertyDescriptor(
    testWindow.HTMLInputElement.prototype,
    "value",
  )?.set;
  valueSetter?.call(input, value);
  input.dispatchEvent(
    new testWindow.Event("input", { bubbles: true }) as unknown as Event,
  );
}

async function flushReact(act: Act) {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

describe("auth card interactions", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("uses generic password copy for sign-in credential failures", async () => {
    const testWindow = installDom("/sign-in");
    const fetchSpy = vi.fn().mockResolvedValueOnce(
      jsonResponse({
        error: "user_not_found",
        ok: false,
      }, 404),
    );
    vi.stubGlobal("fetch", fetchSpy);
    const { act, createElement, createRoot, SignInAuthPage } =
      await loadAuthModules();
    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(SignInAuthPage, {}));
    });

    await act(async () => {
      setInputValue(testWindow, inputByName(container, "email"), "casey@example.com");
      setInputValue(testWindow, inputByName(container, "password"), "correct password");
    });
    await act(async () => {
      container.querySelector("form")?.dispatchEvent(
        new testWindow.Event("submit", { bubbles: true, cancelable: true }) as unknown as Event,
      );
    });
    await flushReact(act);

    expect(container.textContent).toContain("Email or password is incorrect.");
    expect(container.textContent).not.toContain("No account found");

    await act(async () => {
      root.unmount();
    });
  });

  it("shows a reset-success sign-in CTA with password terminology", async () => {
    installDom("/sign-in?reset=success");
    const { act, createElement, createRoot, SignInAuthPage } =
      await loadAuthModules();
    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(SignInAuthPage, { resetSuccess: true }));
    });

    const cta = Array.from(container.querySelectorAll<HTMLAnchorElement>("a")).find(
      (link) => link.textContent === "Continue to sign-in",
    );
    expect(container.textContent).toContain("Your password has been reset.");
    expect(container.textContent).not.toContain("sign-in value");
    expect(cta?.getAttribute("href")).toBe("/sign-in");

    await act(async () => {
      root.unmount();
    });
  });

  it("renders the account-exists sign-in link inside the error message", async () => {
    const testWindow = installDom("/sign-up");
    installTurnstile(testWindow, "signup-token");
    const fetchSpy = vi.fn().mockResolvedValueOnce(
      jsonResponse({
        error: "Account exists. Please sign in.",
        fallbackRedirect: "/sign-in?email=casey%40example.com",
        ok: false,
      }, 409),
    );
    vi.stubGlobal("fetch", fetchSpy);
    const { act, createElement, createRoot, SignUpAuthPage } =
      await loadAuthModules();
    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(SignUpAuthPage, {}));
    });
    await flushReact(act);

    await act(async () => {
      setInputValue(testWindow, inputByName(container, "email"), "casey@example.com");
      setInputValue(testWindow, inputByName(container, "password"), "correct password");
    });
    await act(async () => {
      container.querySelector("form")?.dispatchEvent(
        new testWindow.Event("submit", { bubbles: true, cancelable: true }) as unknown as Event,
      );
    });
    await flushReact(act);

    const alert = container.querySelector<HTMLElement>('[role="alert"]');
    const signInLink = alert?.querySelector<HTMLAnchorElement>('a[href^="/sign-in"]');
    expect(alert?.textContent).toContain("An account already exists for this email.");
    expect(signInLink?.textContent).toBe("Sign in");

    await act(async () => {
      root.unmount();
    });
  });

  it("scrolls Turnstile into view when verification is missing on submit", async () => {
    const testWindow = installDom("/sign-up");
    const scrollIntoView = vi.fn();
    Object.defineProperty(testWindow.HTMLElement.prototype, "scrollIntoView", {
      configurable: true,
      value: scrollIntoView,
    });
    installTurnstile(testWindow, null);
    vi.stubGlobal("fetch", vi.fn());
    const { act, createElement, createRoot, SignUpAuthPage } =
      await loadAuthModules();
    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(SignUpAuthPage, {}));
    });
    await flushReact(act);

    await act(async () => {
      setInputValue(testWindow, inputByName(container, "email"), "casey@example.com");
      setInputValue(testWindow, inputByName(container, "password"), "correct password");
    });
    await act(async () => {
      container.querySelector("form")?.dispatchEvent(
        new testWindow.Event("submit", { bubbles: true, cancelable: true }) as unknown as Event,
      );
    });
    await flushReact(act);

    expect(container.textContent).toContain("Complete the verification and try again.");
    expect(scrollIntoView).toHaveBeenCalledWith({
      behavior: "smooth",
      block: "center",
    });

    await act(async () => {
      root.unmount();
    });
  });
});
