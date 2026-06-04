import { afterEach, describe, expect, it, vi } from "vitest";

const stateUpdates = {
  loading: [] as boolean[],
  error: [] as string[],
};

const azureApiMock = vi.hoisted(() => ({
  azureApiFetch: vi.fn(),
}));

vi.mock("../../lib/client-azure-api", () => azureApiMock);

vi.mock("react", () => ({
  useState(initialValue: boolean | string) {
    if (typeof initialValue === "boolean") {
      return [
        initialValue,
        (nextValue: boolean) => {
          stateUpdates.loading.push(nextValue);
        },
      ] as const;
    }

    return [
      initialValue,
      (nextValue: string) => {
        stateUpdates.error.push(nextValue);
      },
    ] as const;
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

async function renderSubscriptionStatus(paid = false) {
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

  return asElement(
    SubscriptionStatus({
      status: paid ? "Active" : "Inactive",
      usageLabel: paid ? "80 rewrites left" : "3 rewrites left",
      paid,
      canRedeem: false,
      onRedeemClick: vi.fn(),
    }),
  );
}

async function clickButton(label: string, paid = false) {
  const root = await renderSubscriptionStatus(paid);
  const button = walkElements(root).find(
    (element) => element.type === "button" && textContent(element).includes(label),
  );

  expect(button, `${label} button`).toBeDefined();
  expect(button?.props.type).toBe("button");

  const onClick = button?.props.onClick as (() => void | Promise<void>) | undefined;
  expect(onClick).toBeTypeOf("function");
  await onClick?.();
}

function checkoutFetchCall(fetchMock: ReturnType<typeof vi.fn>) {
  return fetchMock.mock.calls.find((call) => call[0] === "/api/stripe/checkout");
}

describe("workspace Buy rewrites checkout flow", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
    stateUpdates.loading = [];
    stateUpdates.error = [];
  });

  it("sends the value pack sku to the checkout proxy and redirects to checkout", async () => {
    const assign = vi.fn();
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      Response.json({ url: "https://checkout.stripe.test/session" }),
    );
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("window", { location: { assign } });

    await clickButton("Buy rewrites");

    expect(azureApiMock.azureApiFetch).not.toHaveBeenCalled();
    expect(checkoutFetchCall(fetchMock)).toEqual([
      "/api/stripe/checkout",
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ sku: "value_pack" }),
      },
    ]);
    expect(assign).toHaveBeenCalledWith("https://checkout.stripe.test/session");
    expect(stateUpdates.error).toEqual([""]);
  });

  it("sends signed-out users to sign in with app as the return route", async () => {
    const assign = vi.fn();
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      new Response(null, { status: 401 }),
    );
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("window", { location: { assign } });

    await clickButton("Buy rewrites");

    expect(checkoutFetchCall(fetchMock)).toBeDefined();
    expect(assign).toHaveBeenCalledWith("/sign-in?redirectTo=%2Fapp");
    expect(stateUpdates.error).toEqual([""]);
  });

  it("shows the checkout error inline when the proxy cannot start checkout", async () => {
    const assign = vi.fn();
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      Response.json(
        { error: "Checkout is unavailable right now." },
        { status: 503 },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("window", { location: { assign } });

    await clickButton("Buy rewrites");

    expect(assign).not.toHaveBeenCalled();
    expect(stateUpdates.error).toEqual([
      "",
      "Checkout is unavailable right now.",
    ]);
    expect(stateUpdates.loading).toEqual([true, false]);
  });

  it("keeps Manage billing wired to the existing portal path", async () => {
    azureApiMock.azureApiFetch.mockResolvedValue(
      Response.json({ url: "https://billing.stripe.test/session" }),
    );
    vi.stubGlobal("window", { location: { href: "" } });

    await clickButton("Manage billing", true);

    expect(azureApiMock.azureApiFetch).toHaveBeenCalledWith("/api/stripe/portal", {
      method: "POST",
    });
  });
});
