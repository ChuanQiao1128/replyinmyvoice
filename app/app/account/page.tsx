import { AccountPanel } from "../../../components/account/account-panel";

export const dynamic = "force-dynamic";

// Rendered inside the app shell (app/app/layout.tsx). FE-S6 decomposes this
// into a slim account page + a dedicated /app/billing page.
export default function AccountPage() {
  return (
    <div className="rimv">
      <AccountPanel />
    </div>
  );
}
