import { SignUp } from "@clerk/nextjs";

export default function SignUpPage() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-paper px-6">
      <SignUp
        forceRedirectUrl="/app"
        fallbackRedirectUrl="/app"
        oauthFlow="redirect"
        path="/sign-up"
        routing="path"
        signInUrl="/sign-in"
      />
    </main>
  );
}
