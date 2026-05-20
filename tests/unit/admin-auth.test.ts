import { describe, expect, it } from "vitest";

import { isAdminIdentityAllowed } from "../../lib/admin-auth";

describe("admin auth allowlist", () => {
  it("allows a matching admin email case-insensitively", () => {
    expect(
      isAdminIdentityAllowed({
        email: "ChuanQiao1128@gmail.com",
        clerkUserId: "user_other",
        adminEmails: "chuanqiao1128@gmail.com",
        adminClerkUserIds: "",
      }),
    ).toBe(true);
  });

  it("allows a matching Clerk user id", () => {
    expect(
      isAdminIdentityAllowed({
        email: "other@example.com",
        clerkUserId: "user_admin",
        adminEmails: "",
        adminClerkUserIds: "user_admin,user_second",
      }),
    ).toBe(true);
  });

  it("denies users when the allowlist is blank", () => {
    expect(
      isAdminIdentityAllowed({
        email: "chuanqiao1128@gmail.com",
        clerkUserId: "user_admin",
        adminEmails: "",
        adminClerkUserIds: "",
      }),
    ).toBe(false);
  });

  it("denies non-matching signed-in users", () => {
    expect(
      isAdminIdentityAllowed({
        email: "customer@example.com",
        clerkUserId: "user_customer",
        adminEmails: "chuanqiao1128@gmail.com",
        adminClerkUserIds: "user_admin",
      }),
    ).toBe(false);
  });
});
