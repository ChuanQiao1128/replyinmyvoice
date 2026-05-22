import { GoogleOAuthCard } from "@/components/auth/google-oauth-card";

import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Sign in",
  description: "Sign in to Reply In My Voice with your Google account.",
};


export default function SignInPage() {
  return <GoogleOAuthCard mode="sign-in" />;
}
