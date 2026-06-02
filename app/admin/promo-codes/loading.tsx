import { Loader2 } from "lucide-react";

import { SiteHeader } from "../../../components/site-header";

export default function AdminPromoCodesLoading() {
  return (
    <>
      <SiteHeader />
      <main className="min-h-screen bg-paper">
        <section className="wrap py-12">
          <div className="rounded-lg border border-line bg-white/80 p-6 shadow-crisp">
            <div className="flex items-center gap-3 text-sm font-semibold text-ink/70">
              <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
              Loading promo codes
            </div>
          </div>
        </section>
      </main>
    </>
  );
}
