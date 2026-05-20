import { SignUp } from "@clerk/nextjs";

export default function SignInPage() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-paper px-6">
      <SignUp
        forceRedirectUrl="/app"
        fallbackRedirectUrl="/app"
        oauthFlow="redirect"
        path="/sign-in"
        routing="path"
        signInUrl="/sign-in"
      />
    </main>
  );
}
