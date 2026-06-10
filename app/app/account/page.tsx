import type { Metadata } from "next";

import { AccountPanel } from "../../../components/account/account-panel";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Account" };

// Rendered inside the app shell (app/app/layout.tsx) as a console page.
export default function AccountPage() {
  return <AccountPanel />;
}
