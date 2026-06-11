import { SignInAuthPage } from "@/components/auth/google-oauth-card";

import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Sign in",
  description: "Sign in to Reply In My Voice with email or Google.",
};

type SignInPageProps = {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
};

export default async function SignInPage({ searchParams }: SignInPageProps) {
  const params = await searchParams;
  const rawRedirectTo = firstParam(params?.redirectTo);
  const redirectTo = rawRedirectTo ?? "/app";

  return (
    <SignInAuthPage
      callbackError={firstParam(params?.error)}
      initialEmail={firstParam(params?.email) ?? ""}
      intent={firstParam(params?.intent)}
      redirectTo={redirectTo}
      resetSuccess={firstParam(params?.reset) === "success"}
      showReturnHint={rawRedirectTo !== undefined}
      sku={firstParam(params?.sku)}
    />
  );
}

function firstParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
