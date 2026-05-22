import { GoogleOAuthCard } from "@/components/auth/google-oauth-card";

import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Sign up",
  description: "Create your Reply In My Voice account with Google.",
};


export default function SignUpPage() {
  return <GoogleOAuthCard mode="sign-up" />;
}
