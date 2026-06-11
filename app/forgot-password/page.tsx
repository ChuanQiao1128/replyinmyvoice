import { ResetAuthPage } from "@/components/auth/google-oauth-card";

import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Reset password",
  description: "Reset your Reply In My Voice account password.",
};

type ResetPageProps = {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
};

export default async function ResetPage({
  searchParams,
}: ResetPageProps) {
  const params = await searchParams;
  const rawRedirectTo = firstParam(params?.redirectTo);
  const redirectTo = rawRedirectTo ?? "/app";

  return (
    <ResetAuthPage
      initialEmail={firstParam(params?.email) ?? ""}
      intent={firstParam(params?.intent)}
      redirectTo={redirectTo}
      showReturnHint={rawRedirectTo !== undefined}
      sku={firstParam(params?.sku)}
    />
  );
}

function firstParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
