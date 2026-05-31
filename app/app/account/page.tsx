import { AccountPanel } from "../../../components/account/account-panel";
import { SiteHeader } from "../../../components/site-header";

export const dynamic = "force-dynamic";

export default function AccountPage() {
  return (
    <>
      <SiteHeader />
      <main className="rimv">
        <section className="wrap py-10 sm:py-14">
          <AccountPanel />
        </section>
      </main>
    </>
  );
}
