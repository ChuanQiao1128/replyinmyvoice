import { describe, expect, it } from "vitest";

import { isAdminIdentityAllowed } from "../../lib/admin-auth";

describe("admin auth allowlist", () => {
  it("allows a matching admin email case-insensitively", () => {
    expect(
      isAdminIdentityAllowed({
        email: "ChuanQiao1128@gmail.com",
        userId: "user_other",
        adminEmails: "chuanqiao1128@gmail.com",
        adminUserIds: "",
      }),
    ).toBe(true);
  });

  it("allows a matching Clerk user id", () => {
    expect(
      isAdminIdentityAllowed({
        email: "other@example.com",
        userId: "user_admin",
        adminEmails: "",
        adminUserIds: "user_admin,user_second",
      }),
    ).toBe(true);
  });

  it("denies users when the allowlist is blank", () => {
    expect(
      isAdminIdentityAllowed({
        email: "chuanqiao1128@gmail.com",
        userId: "user_admin",
        adminEmails: "",
        adminUserIds: "",
      }),
    ).toBe(false);
  });

  it("denies non-matching signed-in users", () => {
    expect(
      isAdminIdentityAllowed({
        email: "customer@example.com",
        userId: "user_customer",
        adminEmails: "chuanqiao1128@gmail.com",
        adminUserIds: "user_admin",
      }),
    ).toBe(false);
  });
});
