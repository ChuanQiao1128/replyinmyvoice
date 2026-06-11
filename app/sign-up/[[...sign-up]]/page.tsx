import { SignUpAuthPage } from "@/components/auth/google-oauth-card";

import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Sign up",
  description: "Create your Reply In My Voice account with email or Google.",
};

type SignUpPageProps = {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
};

export default async function SignUpPage({ searchParams }: SignUpPageProps) {
  const params = await searchParams;
  const rawRedirectTo = firstParam(params?.redirectTo);
  const redirectTo = rawRedirectTo ?? "/app";

  return (
    <SignUpAuthPage
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
