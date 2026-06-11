import { Window } from "happy-dom";
import type { ComponentType, ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type {
  AdminBillingSupportRequest,
  AdminStatsResponse,
  AdminUserDetailResponse,
  AdminUsersListResponse,
} from "../../lib/admin-types";

type Act = typeof import("react").act;
type CreateElement = typeof import("react").createElement;
type CreateRoot = typeof import("react-dom/client").createRoot;
type AdminDashboardComponent = ComponentType;
type AdminUserDetailComponent = ComponentType<{ userId: string }>;

function installDom(path: string) {
  const testWindow = new Window({ url: `http://localhost${path}` });

  vi.stubGlobal("window", testWindow);
  vi.stubGlobal("self", testWindow);
  vi.stubGlobal("document", testWindow.document);
  vi.stubGlobal("navigator", testWindow.navigator);
  vi.stubGlobal("HTMLElement", testWindow.HTMLElement);
  vi.stubGlobal("HTMLInputElement", testWindow.HTMLInputElement);
  vi.stubGlobal("HTMLSelectElement", testWindow.HTMLSelectElement);
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
  vi.stubGlobal("URL", testWindow.URL);

  (
    globalThis as typeof globalThis & {
      IS_REACT_ACT_ENVIRONMENT: boolean;
    }
  ).IS_REACT_ACT_ENVIRONMENT = true;

  return testWindow;
}

async function loadAdminModules(search = "") {
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
  vi.doMock("next/navigation", () => ({
    useSearchParams: () => new URLSearchParams(search),
  }));

  const [reactDom, dashboard, userDetail] = await Promise.all([
    import("react-dom/client"),
    import("../../components/admin/admin-dashboard"),
    import("../../components/admin/admin-user-detail"),
  ]);

  return {
    act: react.act as Act,
    AdminDashboard: dashboard.AdminDashboard as AdminDashboardComponent,
    AdminUserDetail: userDetail.AdminUserDetail as AdminUserDetailComponent,
    createElement: react.createElement as CreateElement,
    createRoot: reactDom.createRoot as CreateRoot,
  };
}

function jsonResponse(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), {
    headers: { "Content-Type": "application/json" },
    status,
  });
}

async function flushReact(act: Act) {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

function buttonByText(container: HTMLElement, text: string) {
  return Array.from(container.querySelectorAll("button")).find((button) =>
    button.textContent?.includes(text),
  );
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

function setSelectValue(
  testWindow: Window,
  select: HTMLSelectElement,
  value: string,
) {
  const valueSetter = Object.getOwnPropertyDescriptor(
    testWindow.HTMLSelectElement.prototype,
    "value",
  )?.set;
  valueSetter?.call(select, value);
  select.dispatchEvent(
    new testWindow.Event("change", { bubbles: true }) as unknown as Event,
  );
}

function setTextareaValue(
  testWindow: Window,
  textarea: HTMLTextAreaElement,
  value: string,
) {
  const valueSetter = Object.getOwnPropertyDescriptor(
    testWindow.HTMLTextAreaElement.prototype,
    "value",
  )?.set;
  valueSetter?.call(textarea, value);
  textarea.dispatchEvent(
    new testWindow.Event("input", { bubbles: true }) as unknown as Event,
  );
}

function installConfirm(
  testWindow: Window,
  confirm: (message?: string) => boolean,
) {
  Object.defineProperty(testWindow, "confirm", {
    configurable: true,
    value: confirm,
  });
}

function adminDetail(): AdminUserDetailResponse {
  return {
    costToDateUsd: 12,
    createdAt: "2026-06-01T00:00:00.000Z",
    credits: [],
    email: "admin-target@example.test",
    externalAuthUserId: "external_admin_target",
    id: "user_admin_target",
    payments: [
      {
        amountTotal: 1200,
        creditId: "credit_1",
        creditsConsumed: 0,
        creditsGranted: 40,
        creditsRemaining: 40,
        currency: "nzd",
        eventId: "evt_1",
        expiresAt: null,
        grantedAt: "2026-06-01T00:00:00.000Z",
        paymentIntentId: "pi_admin_payment",
        receiptUrl: null,
        sku: "pro",
        source: "stripe",
      },
    ],
    subscription: {
      currentPeriodEnd: null,
      status: "active",
      stripeCustomerId: "cus_admin",
      stripeSubscriptionId: "sub_admin",
    },
    updatedAt: "2026-06-02T00:00:00.000Z",
    usage: [],
  };
}

function adminStats(): AdminStatsResponse {
  return {
    costToDateUsd: 12,
    creditRemaining: 40,
    freeUsers: 0,
    paidUsers: 1,
    paymentAmountTotal: 1200,
    paymentCount: 1,
    refundReview: {
      flaggedUserCount: 0,
      refundAmountThreshold: 1000,
      refundCountThreshold: 2,
      totalRefundAmount: 0,
      totalRefundCount: 0,
    },
    totalUsers: 1,
    usageReserved: 0,
    usageUsed: 0,
  };
}

function adminUsers(): AdminUsersListResponse {
  return {
    page: 1,
    pageSize: 25,
    totalCount: 1,
    totalPages: 1,
    users: [
      {
        costToDateUsd: 12,
        createdAt: "2026-06-01T00:00:00.000Z",
        creditRemaining: 40,
        email: "admin-target@example.test",
        externalAuthUserId: "external_admin_target",
        id: "user_admin_target",
        reservedRewrites: 0,
        subscriptionStatus: "active",
        updatedAt: "2026-06-02T00:00:00.000Z",
        usedRewrites: 0,
      },
    ],
  };
}

function supportQueue(): AdminBillingSupportRequest[] {
  return [];
}

function refundForm(container: HTMLElement) {
  const form = Array.from(container.querySelectorAll("form")).find((candidate) =>
    candidate.textContent?.includes("Payment"),
  );
  expect(form).toBeTruthy();
  return form!;
}

describe("admin safety interactions", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("rejects refund amounts above the original payment before submit", async () => {
    const testWindow = installDom("/admin/users/user_admin_target");
    const fetchSpy = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(adminDetail()))
      .mockResolvedValueOnce(jsonResponse({ refundId: "refund_1" }))
      .mockResolvedValueOnce(jsonResponse(adminDetail()));
    const confirmSpy = vi.fn(() => true);
    installConfirm(testWindow, confirmSpy);
    vi.stubGlobal("fetch", fetchSpy);

    const { act, AdminUserDetail, createElement, createRoot } =
      await loadAdminModules();
    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(AdminUserDetail, { userId: "user_admin_target" }));
    });
    await flushReact(act);

    const form = refundForm(container);
    await act(async () => {
      setSelectValue(
        testWindow,
        form.querySelector("select")!,
        "pi_admin_payment",
      );
      setInputValue(testWindow, form.querySelector('input[type="number"]')!, "1201");
      setInputValue(
        testWindow,
        Array.from(form.querySelectorAll("input")).find(
          (input) => input.type !== "number",
        )!,
        "nzd",
      );
      setTextareaValue(testWindow, form.querySelector("textarea")!, "Requested once.");
    });

    await act(async () => {
      form.dispatchEvent(
        new testWindow.Event("submit", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });
    await flushReact(act);

    expect(container.textContent).toContain(
      "Refund amount cannot exceed the original payment amount.",
    );
    expect(confirmSpy).not.toHaveBeenCalled();
    expect(fetchSpy).toHaveBeenCalledTimes(1);

    await act(async () => {
      root.unmount();
    });
  });

  it("rejects refund currency changes before submit", async () => {
    const testWindow = installDom("/admin/users/user_admin_target");
    const fetchSpy = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(adminDetail()))
      .mockResolvedValueOnce(jsonResponse({ refundId: "refund_1" }))
      .mockResolvedValueOnce(jsonResponse(adminDetail()));
    const confirmSpy = vi.fn(() => true);
    installConfirm(testWindow, confirmSpy);
    vi.stubGlobal("fetch", fetchSpy);

    const { act, AdminUserDetail, createElement, createRoot } =
      await loadAdminModules();
    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(AdminUserDetail, { userId: "user_admin_target" }));
    });
    await flushReact(act);

    const form = refundForm(container);
    await act(async () => {
      setSelectValue(
        testWindow,
        form.querySelector("select")!,
        "pi_admin_payment",
      );
      setInputValue(testWindow, form.querySelector('input[type="number"]')!, "1200");
      setInputValue(
        testWindow,
        Array.from(form.querySelectorAll("input")).find(
          (input) => input.type !== "number",
        )!,
        "usd",
      );
      setTextareaValue(testWindow, form.querySelector("textarea")!, "Requested once.");
    });

    await act(async () => {
      form.dispatchEvent(
        new testWindow.Event("submit", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });
    await flushReact(act);

    expect(container.textContent).toContain(
      "Refund currency must match the original payment currency.",
    );
    expect(confirmSpy).not.toHaveBeenCalled();
    expect(fetchSpy).toHaveBeenCalledTimes(1);

    await act(async () => {
      root.unmount();
    });
  });

  it("requires a suspension reason before asking for confirmation", async () => {
    const testWindow = installDom("/admin/users/user_admin_target");
    const fetchSpy = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(adminDetail()))
      .mockResolvedValueOnce(jsonResponse({ suspended: true }))
      .mockResolvedValueOnce(jsonResponse(adminDetail()));
    const confirmSpy = vi.fn(() => true);
    installConfirm(testWindow, confirmSpy);
    vi.stubGlobal("fetch", fetchSpy);

    const { act, AdminUserDetail, createElement, createRoot } =
      await loadAdminModules();
    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(AdminUserDetail, { userId: "user_admin_target" }));
    });
    await flushReact(act);

    await act(async () => {
      buttonByText(container, "Suspend")?.dispatchEvent(
        new testWindow.MouseEvent("click", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });
    await flushReact(act);

    expect(container.textContent).toContain(
      "Enter a suspension reason before suspending this user.",
    );
    expect(confirmSpy).not.toHaveBeenCalled();
    expect(fetchSpy).toHaveBeenCalledTimes(1);

    await act(async () => {
      root.unmount();
    });
  });

  it("gates user erase behind an exact typed confirmation", async () => {
    const testWindow = installDom("/admin");
    const fetchSpy = vi.fn((url: string, init?: RequestInit) => {
      if (url === "/api/admin/stats") {
        return Promise.resolve(jsonResponse(adminStats()));
      }
      if (url.startsWith("/api/admin/users?")) {
        return Promise.resolve(jsonResponse(adminUsers()));
      }
      if (url === "/api/admin/billing-support-requests") {
        return Promise.resolve(jsonResponse(supportQueue()));
      }
      if (
        url === "/api/admin/users/user_admin_target" &&
        init?.method === "DELETE"
      ) {
        return Promise.resolve(new Response(null, { status: 204 }));
      }
      return Promise.resolve(jsonResponse({ error: "Unexpected request." }, 500));
    });
    const confirmSpy = vi.fn(() => true);
    installConfirm(testWindow, confirmSpy);
    vi.stubGlobal("fetch", fetchSpy);

    const { act, AdminDashboard, createElement, createRoot } =
      await loadAdminModules();
    const container = document.createElement("div");
    document.body.append(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(createElement(AdminDashboard));
    });
    await flushReact(act);

    await act(async () => {
      buttonByText(container, "Delete")?.dispatchEvent(
        new testWindow.MouseEvent("click", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(container.querySelector('[role="dialog"]')?.textContent).toContain(
      "Type ERASE to confirm account erase.",
    );
    const finalButton = buttonByText(container, "Erase account");
    expect(finalButton?.disabled).toBe(true);

    const confirmationInput = container.querySelector<HTMLInputElement>(
      'input[aria-label="Type ERASE to confirm account erase"]',
    );
    expect(confirmationInput).toBeTruthy();

    await act(async () => {
      setInputValue(testWindow, confirmationInput!, "erase");
    });
    expect(finalButton?.disabled).toBe(true);

    await act(async () => {
      setInputValue(testWindow, confirmationInput!, "ERASE");
    });
    expect(finalButton?.disabled).toBe(false);

    await act(async () => {
      finalButton?.dispatchEvent(
        new testWindow.MouseEvent("click", {
          bubbles: true,
          cancelable: true,
        }) as unknown as Event,
      );
    });
    await flushReact(act);

    expect(fetchSpy).toHaveBeenCalledWith("/api/admin/users/user_admin_target", {
      cache: "no-store",
      method: "DELETE",
    });

    await act(async () => {
      root.unmount();
    });
  });
});
