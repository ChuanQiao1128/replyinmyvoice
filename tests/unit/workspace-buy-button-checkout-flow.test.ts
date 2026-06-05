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

function isButtonElement(element: ElementLike) {
  return (
    element.type === "button" ||
    (typeof element.type === "function" && element.type.name === "Button")
  );
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
      paymentGraceEndsAt: null,
      canRedeem: false,
      onRedeemClick: vi.fn(),
    }),
  );
}

async function clickButton(label: string, paid = false) {
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

describe("workspace Buy rewrites pack picker", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
    stateUpdates.loading = [];
    stateUpdates.error = [];
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
