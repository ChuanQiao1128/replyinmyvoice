import { afterEach, describe, expect, it, vi } from "vitest";

const stateUpdates = {
  loading: [] as boolean[],
  error: [] as string[],
};

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

async function clickBuyButton() {
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
  const root = asElement(
    BuyButton({
      sku: "quick_pack",
      label: "Buy quick pack",
    }),
  );
  const children = root.props.children as unknown[];
  const button = asElement(children[0]);
  const onClick = button.props.onClick as () => Promise<void>;
  await onClick();
}

describe("BuyButton checkout flow", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
    stateUpdates.loading = [];
    stateUpdates.error = [];
  });

  it("sends signed-out users to sign in with pricing as the return route", async () => {
    const assign = vi.fn();
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      new Response(null, { status: 401 }),
    );
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("window", { location: { assign } });

    await clickBuyButton();

    expect(fetchMock).toHaveBeenCalledWith("/api/stripe/checkout", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ sku: "quick_pack" }),
    });
    expect(assign).toHaveBeenCalledWith("/sign-in?redirectTo=%2Fpricing");
    expect(stateUpdates.error).toEqual([""]);
  });

  it("redirects to the checkout URL when the API returns one", async () => {
    const assign = vi.fn();
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      Response.json({ url: "https://checkout.stripe.test/session" }),
    );
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("window", { location: { assign } });

    await clickBuyButton();

    expect(assign).toHaveBeenCalledWith("https://checkout.stripe.test/session");
    expect(stateUpdates.error).toEqual([""]);
  });

  it("shows the API error message inline when checkout cannot start", async () => {
    const assign = vi.fn();
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      Response.json(
        { error: "Checkout is unavailable right now." },
        { status: 503 },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("window", { location: { assign } });

    await clickBuyButton();

    expect(assign).not.toHaveBeenCalled();
    expect(stateUpdates.error).toEqual([
      "",
      "Checkout is unavailable right now.",
    ]);
    expect(stateUpdates.loading).toEqual([true, false]);
  });
});
