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
  const redirectTo = firstParam(params?.redirectTo) ?? "/app";

  return (
    <SignUpAuthPage
      initialEmail={firstParam(params?.email) ?? ""}
      redirectTo={redirectTo}
    />
  );
}

function firstParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
