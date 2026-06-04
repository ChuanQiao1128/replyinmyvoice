import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("react", () => ({
  useEffect: () => {},
}));

type ElementLike = {
  type: unknown;
  props: Record<string, unknown>;
};

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

function stubCreateElement() {
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
}

async function loadDialog() {
  const mod = await import("../../components/app/buy-rewrites-dialog");
  return mod.BuyRewritesDialog;
}

function buyButtons(root: unknown) {
  return walkElements(root).filter(
    (element) =>
      typeof element.type === "function" && element.type.name === "BuyButton",
  );
}

describe("BuyRewritesDialog pack picker", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it("offers exactly the three packs with their checkout SKUs in order", async () => {
    stubCreateElement();
    const BuyRewritesDialog = await loadDialog();

    const root = BuyRewritesDialog({ open: true, onClose: vi.fn() });
    const buttons = buyButtons(root);

    expect(buttons.map((button) => button.props.sku)).toEqual([
      "quick_pack",
      "value_pack",
      "pro_api",
    ]);
    expect(buttons.map((button) => button.props.label)).toEqual([
      "Get Quick Pack",
      "Get Value Pack",
      "Go Pro/API",
    ]);
  });

  it("renders an accessible modal with a close affordance", async () => {
    stubCreateElement();
    const BuyRewritesDialog = await loadDialog();

    const root = BuyRewritesDialog({
      open: true,
      onClose: vi.fn(),
    }) as ElementLike;

    expect(root.props.role).toBe("dialog");
    expect(root.props["aria-modal"]).toBe("true");

    const closeButton = walkElements(root).find(
      (element) => element.props["aria-label"] === "Close",
    );
    expect(closeButton, "close button").toBeDefined();
    expect(closeButton?.props.onClick).toBeTypeOf("function");
  });

  it("renders nothing when closed", async () => {
    stubCreateElement();
    const BuyRewritesDialog = await loadDialog();

    expect(BuyRewritesDialog({ open: false, onClose: vi.fn() })).toBeNull();
  });
});
