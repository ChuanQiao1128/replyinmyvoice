import type { Metadata } from "next";

import { HistoryList } from "../../../components/app/history-list";
import { PageHeader } from "../../../components/app/shell/shell-primitives";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "History" };

export default function HistoryPage() {
  return (
    <>
      <PageHeader
        title="History"
        description="Every rewrite you save, available across your devices."
      />
      <HistoryList />
    </>
  );
}
