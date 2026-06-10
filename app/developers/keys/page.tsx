import { redirect } from "next/navigation";

export const dynamic = "force-dynamic";

// The developer console moved into the app shell at /app/keys. A permanent
// redirect is also configured in next.config.ts; this is the runtime fallback.
export default function DeveloperApiKeysPage() {
  redirect("/app/keys");
}
