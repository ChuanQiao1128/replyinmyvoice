import { afterEach, describe, expect, it, vi } from "vitest";

const stateUpdates = {
  loading: [] as boolean[],
  error: [] as string[],
  other: [] as unknown[],
};

const reactHookRuntime = vi.hoisted(() => ({
  cursor: 0,
  refs: [] as Array<{ current: unknown }>,
  stateKinds: [] as Array<"boolean" | "string" | "other">,
  states: [] as unknown[],
}));

const azureApiMock = vi.hoisted(() => ({
  azureApiFetch: vi.fn(),
}));

vi.mock("../../lib/client-azure-api", () => azureApiMock);

vi.mock("react", () => ({
  useEffect(effect: () => void | (() => void)) {
    reactHookRuntime.cursor += 1;
    return effect();
  },
  useRef(initialValue: unknown) {
    const index = reactHookRuntime.cursor;
    reactHookRuntime.cursor += 1;

    if (!reactHookRuntime.refs[index]) {
      reactHookRuntime.refs[index] = { current: initialValue };
    }

    return reactHookRuntime.refs[index];
  },
  useState(initialValue: unknown) {
    const index = reactHookRuntime.cursor;
    reactHookRuntime.cursor += 1;
    const resolvedInitialValue =
      typeof initialValue === "function"
        ? (initialValue as () => unknown)()
        : initialValue;
    const hasStoredState = Object.prototype.hasOwnProperty.call(
      reactHookRuntime.states,
      index,
    );
    const currentValue = hasStoredState
      ? reactHookRuntime.states[index]
      : resolvedInitialValue;

    if (!hasStoredState) {
      reactHookRuntime.states[index] = resolvedInitialValue;
      reactHookRuntime.stateKinds[index] =
        typeof resolvedInitialValue === "boolean"
          ? "boolean"
          : typeof resolvedInitialValue === "string"
            ? "string"
            : "other";
    }

    function setNextState(nextValue: unknown) {
      const resolvedNextValue =
        typeof nextValue === "function"
          ? (nextValue as (previousValue: unknown) => unknown)(
              reactHookRuntime.states[index],
            )
          : nextValue;
      reactHookRuntime.states[index] = resolvedNextValue;

      if (reactHookRuntime.stateKinds[index] === "boolean") {
        stateUpdates.loading.push(resolvedNextValue as boolean);
      } else if (reactHookRuntime.stateKinds[index] === "string") {
        stateUpdates.error.push(resolvedNextValue as string);
      } else {
        stateUpdates.other.push(resolvedNextValue);
      }
    }

    if (reactHookRuntime.stateKinds[index] === "boolean") {
      return [currentValue, setNextState] as const;
    }

    if (reactHookRuntime.stateKinds[index] === "string") {
      return [currentValue, setNextState] as const;
    }

    return [currentValue, setNextState] as const;
  },
}));

type ElementLike = {
  type: unknown;
  props: Record<string, unknown>;
};

function asElement(value: unknown): ElementLike {
  expect(value).toBeTypeOf("object");
  expect(value).not.toBeNull();
  return value as ElementLike;
}

function textContent(value: unknown): string {
  if (typeof value === "string" || typeof value === "number") {
    return String(value);
  }

  if (Array.isArray(value)) {
    return value.map((item) => textContent(item)).join("");
  }

  if (value && typeof value === "object" && "props" in value) {
    return textContent((value as ElementLike).props.children);
  }

  return "";
}

function walkElements(value: unknown): ElementLike[] {
  if (Array.isArray(value)) {
    return value.flatMap((item) => walkElements(item));
  }

  if (!value || typeof value !== "object" || !("props" in value)) {
    return [];
  }

  const element = value as ElementLike;
  return [element, ...walkElements(element.props.children)];
}

function resetHookRender() {
  reactHookRuntime.cursor = 0;
}

function resetHookState() {
  reactHookRuntime.cursor = 0;
  reactHookRuntime.refs = [];
  reactHookRuntime.stateKinds = [];
  reactHookRuntime.states = [];
}

function isButtonElement(element: ElementLike) {
  return (
    element.type === "button" ||
    (typeof element.type === "function" && element.type.name === "Button")
  );
}

async function renderSubscriptionStatus(
  paid = false,
  checkoutStatus: "success" | "cancelled" | null = null,
) {
  vi.stubGlobal("React", {
    createElement(
      type: unknown,
      props: Record<string, unknown> | null,
      ...children: unknown[]
    ) {
      return {
        type,
        props: {
          ...(props ?? {}),
          children: children.length === 1 ? children[0] : children,
        },
      };
    },
  });

  const { SubscriptionStatus } = await import(
    "../../components/app/subscription-status"
  );

  const props = {
    status: paid ? "Active" : "Inactive",
    usageLabel: paid ? "80 rewrites left" : "3 rewrites left",
    paid,
    paymentGraceEndsAt: null,
    canRedeem: false,
    checkoutStatus,
    onRedeemClick: vi.fn(),
  } satisfies Parameters<typeof SubscriptionStatus>[0] & {
    checkoutStatus: "success" | "cancelled" | null;
  };

  return asElement(SubscriptionStatus(props));
}

async function renderCheckoutBanner(
  status: "success" | "cancelled" | null,
  usageLabel = "3 rewrites left",
) {
  vi.stubGlobal("React", {
    createElement(
      type: unknown,
      props: Record<string, unknown> | null,
      ...children: unknown[]
    ) {
      return {
        type,
        props: {
          ...(props ?? {}),
          children: children.length === 1 ? children[0] : children,
        },
      };
    },
  });

  const { CheckoutBanner } = await import("../../components/app/checkout-banner");
  return CheckoutBanner({ status, usageLabel });
}

async function clickButton(label: string, paid = false) {
  resetHookRender();
  const root = await renderSubscriptionStatus(paid);
  const button = walkElements(root).find(
    (element) => isButtonElement(element) && textContent(element).includes(label),
  );

  expect(button, `${label} button`).toBeDefined();
  expect(button?.props.type).toBe("button");

  const onClick = button?.props.onClick as
    | (() => void | Promise<void>)
    | undefined;
  expect(onClick).toBeTypeOf("function");
  await onClick?.();
}

async function clickLandingBuyButton(sku: string) {
  vi.stubGlobal("React", {
    createElement(
      type: unknown,
      props: Record<string, unknown> | null,
      ...children: unknown[]
    ) {
      return {
        type,
        props: {
          ...(props ?? {}),
          children: children.length === 1 ? children[0] : children,
        },
      };
    },
  });

  const { BuyButton } = await import("../../components/landing/buy-button");

  resetHookRender();
  const root = asElement(
    BuyButton({
      sku,
      label: "Buy selected pack",
    }),
  );
  const children = root.props.children as unknown[];
  const button = asElement(children[0]);
  const onClick = button.props.onClick as () => Promise<void>;
  await onClick();
}

function checkoutFetchCall(fetchMock: ReturnType<typeof vi.fn>) {
  return fetchMock.mock.calls.find((call) => call[0] === "/api/stripe/checkout");
}

function findDialog(root: ElementLike) {
  return walkElements(root).find(
    (element) =>
      typeof element.type === "function" &&
      element.type.name === "BuyRewritesDialog",
  );
}

function stubPricingWindow(search: string, hash = "") {
  const windowStub = {
    location: {
      assign: vi.fn(),
      hash,
      pathname: "/pricing",
      search,
    },
    history: {
      state: { unit: true },
      replaceState: vi.fn(
        (_state: unknown, _title: string, nextPath: string | URL | null) => {
          const nextUrl = new URL(
            String(nextPath),
            "https://replyinmyvoice.test",
          );
          windowStub.location.pathname = nextUrl.pathname;
          windowStub.location.search = nextUrl.search;
          windowStub.location.hash = nextUrl.hash;
        },
      ),
    },
  };

  vi.stubGlobal("window", windowStub);
  return windowStub;
}

function stubAppWindow(search: string, hash = "") {
  const windowStub = {
    location: {
      hash,
      pathname: "/app",
      search,
    },
    history: {
      state: { unit: true },
      replaceState: vi.fn(
        (_state: unknown, _title: string, nextPath: string | URL | null) => {
          const nextUrl = new URL(
            String(nextPath),
            "https://replyinmyvoice.test",
          );
          windowStub.location.pathname = nextUrl.pathname;
          windowStub.location.search = nextUrl.search;
          windowStub.location.hash = nextUrl.hash;
        },
      ),
    },
  };

  vi.stubGlobal("window", windowStub);
  return windowStub;
}

async function renderPricingCheckoutResume() {
  vi.stubGlobal("React", {
    createElement(
      type: unknown,
      props: Record<string, unknown> | null,
      ...children: unknown[]
    ) {
      return {
        type,
        props: {
          ...(props ?? {}),
          children: children.length === 1 ? children[0] : children,
        },
      };
    },
  });

  const { PricingCheckoutResume } = await import(
    "../../components/landing/pricing-checkout-resume"
  );

  resetHookRender();
  return PricingCheckoutResume();
}

describe("workspace Buy rewrites pack picker", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
    resetHookState();
    stateUpdates.loading = [];
    stateUpdates.error = [];
    stateUpdates.other = [];
  });

  it("threads the selected pack SKU through the signed-out checkout redirect", async () => {
    const assign = vi.fn();
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      new Response(null, { status: 401 }),
    );
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("window", { location: { assign } });

    await clickLandingBuyButton("value_pack");

    expect(fetchMock).toHaveBeenCalledWith("/api/stripe/checkout", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ sku: "value_pack" }),
    });
    expect(assign).toHaveBeenCalledWith(
      "/sign-in?redirectTo=%2Fpricing&intent=buy&sku=value_pack",
    );
  });

  it("auto-resumes pricing checkout once and strips purchase intent params", async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      Response.json({ url: "https://checkout.stripe.test/session" }),
    );
    vi.stubGlobal("fetch", fetchMock);
    const windowStub = stubPricingWindow(
      "?intent=buy&sku=value_pack&keep=dashboard",
      "#plans",
    );

    await renderPricingCheckoutResume();
    const firstRender = await renderPricingCheckoutResume();
    await Promise.resolve();

    expect(textContent(firstRender)).toContain("Continuing your purchase");
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith("/api/stripe/checkout", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ sku: "value_pack" }),
      signal: expect.any(Object),
    });
    expect(windowStub.history.replaceState).toHaveBeenCalledWith(
      { unit: true },
      "",
      "/pricing?keep=dashboard#plans",
    );

    await renderPricingCheckoutResume();

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("lets users cancel the pricing checkout resume", async () => {
    const abort = vi.fn();
    vi.stubGlobal(
      "AbortController",
      class {
        signal = { aborted: false };

        abort() {
          this.signal.aborted = true;
          abort();
        }
      },
    );
    const fetchMock = vi.fn<typeof fetch>().mockImplementation(
      () => new Promise<Response>(() => {}),
    );
    vi.stubGlobal("fetch", fetchMock);
    const windowStub = stubPricingWindow("?intent=buy&sku=quick_pack");

    await renderPricingCheckoutResume();
    const root = await renderPricingCheckoutResume();
    const cancelButton = walkElements(root).find(
      (element) =>
        element.type === "button" && textContent(element).includes("Cancel"),
    );

    expect(cancelButton, "Cancel button").toBeDefined();
    const onClick = cancelButton?.props.onClick as (() => void) | undefined;
    onClick?.();

    expect(abort).toHaveBeenCalledTimes(1);
    expect(windowStub.history.replaceState).toHaveBeenLastCalledWith(
      { unit: true },
      "",
      "/pricing",
    );
    expect(windowStub.location.search).toBe("");
  });

  it("opens the pack picker instead of starting one fixed checkout", async () => {
    const assign = vi.fn();
    const fetchMock = vi.fn<typeof fetch>();
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("window", { location: { assign } });

    await clickButton("Buy rewrites");

    // Free users no longer jump straight to the value-pack checkout; the picker
    // opens client-side so they can choose a pack first.
    expect(checkoutFetchCall(fetchMock)).toBeUndefined();
    expect(fetchMock).not.toHaveBeenCalled();
    expect(assign).not.toHaveBeenCalled();
    expect(stateUpdates.loading).toContain(true);
  });

  it("renders purchase confirmation once and strips the checkout param", async () => {
    const windowStub = stubAppWindow("?checkout=success&keep=usage", "#draft");

    resetHookRender();
    const root = asElement(await renderCheckoutBanner("success"));
    const text = textContent(root);

    expect(text).toContain("Purchase confirmed");
    expect(text).toContain("3 rewrites left");
    expect(windowStub.history.replaceState).toHaveBeenCalledWith(
      { unit: true },
      "",
      "/app?keep=usage#draft",
    );

    resetHookRender();
    const clearedRoot = await renderCheckoutBanner(null);
    expect(clearedRoot).toBeNull();
  });

  it("renders cancelled checkout once with a pricing retry path", async () => {
    const windowStub = stubAppWindow("?checkout=cancelled&keep=workspace");

    resetHookRender();
    const root = asElement(await renderCheckoutBanner("cancelled"));
    const text = textContent(root);

    expect(text).toContain("Checkout cancelled");
    expect(text).toContain("No charge was made");
    expect(windowStub.history.replaceState).toHaveBeenCalledWith(
      { unit: true },
      "",
      "/app?keep=workspace",
    );

    const pricingLink = walkElements(root).find(
      (element) =>
        typeof element.type === "function" &&
        element.type.name === "LinkButton" &&
        textContent(element).includes("Back to pricing"),
    );
    expect(pricingLink?.props.href).toBe("/pricing");

    resetHookRender();
    const clearedRoot = await renderCheckoutBanner(null);
    expect(clearedRoot).toBeNull();
  });

  it("threads checkout status into the subscription strip", async () => {
    vi.stubGlobal("window", {
      location: { hash: "", pathname: "/app", search: "" },
      history: { replaceState: vi.fn(), state: null },
    });

    resetHookRender();
    const root = await renderSubscriptionStatus(false, "success");
    const banner = walkElements(root).find(
      (element) =>
        typeof element.type === "function" &&
        element.type.name === "CheckoutBanner",
    );

    expect(banner?.props.status).toBe("success");
    expect(banner?.props.usageLabel).toBe("3 rewrites left");
  });

  it("renders the pack picker dialog wired to a close handler", async () => {
    vi.stubGlobal("fetch", vi.fn());
    vi.stubGlobal("window", { location: { assign: vi.fn() } });

    const root = await renderSubscriptionStatus(false);
    const dialog = findDialog(root);

    expect(dialog, "BuyRewritesDialog").toBeDefined();
    expect(dialog?.props.open).toBe(false);
    expect(dialog?.props.onClose).toBeTypeOf("function");
  });

  it("keeps Manage billing wired to the existing portal path", async () => {
    azureApiMock.azureApiFetch.mockResolvedValue(
      Response.json({ url: "https://billing.stripe.test/session" }),
    );
    vi.stubGlobal("window", { location: { href: "" } });

    await clickButton("Manage billing", true);

    expect(azureApiMock.azureApiFetch).toHaveBeenCalledWith(
      "/api/stripe/portal",
      {
        method: "POST",
      },
    );
  });
});
