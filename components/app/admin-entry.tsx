import { Settings } from "lucide-react";
import Link from "next/link";

export function AdminEntry({ visible }: { visible: boolean }) {
  if (!visible) {
    return null;
  }

  return (
    <Link
      className="inline-flex h-10 items-center gap-2 rounded-md border border-line bg-white px-3 text-sm font-semibold text-ink/70 transition hover:border-clay hover:text-clay"
      href="/admin"
      title="Admin dashboard"
    >
      <Settings className="h-4 w-4" aria-hidden="true" />
      <span>Admin</span>
    </Link>
  );
}
