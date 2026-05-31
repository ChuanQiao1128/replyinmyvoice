import { ResetAuthPage } from "@/components/auth/google-oauth-card";

import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Reset sign-in",
  description: "Reset your Reply In My Voice account sign-in value.",
};

type ResetPageProps = {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
};

export default async function ResetPage({
  searchParams,
}: ResetPageProps) {
  const params = await searchParams;

  return (
    <ResetAuthPage
      initialEmail={firstParam(params?.email) ?? ""}
    />
  );
}

function firstParam(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
