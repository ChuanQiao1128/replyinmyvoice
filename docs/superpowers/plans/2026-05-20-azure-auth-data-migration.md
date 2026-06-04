# Azure Auth And Data Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fully remove Clerk and Neon from Reply In My Voice by migrating customer authentication to Microsoft Entra External ID and moving the application database to Azure Database for PostgreSQL, while preserving Stripe subscriptions, rewrite usage, admin access, and production deployability.

**Architecture:** Use Microsoft Entra External ID as the customer identity provider and Auth.js as the app-side OIDC session layer. Use Azure Database for PostgreSQL Flexible Server as the Postgres database. Because the current Cloudflare Worker runtime is optimized for Neon’s serverless HTTP driver, the clean Azure-only path is to deploy the Next.js app to Azure App Service and keep Cloudflare only as DNS/proxy after smoke testing.

**Tech Stack:** Next.js 15 App Router, Auth.js, Microsoft Entra External ID, Azure Database for PostgreSQL Flexible Server, Azure App Service, Prisma/PostgreSQL, Stripe, OpenAI, Sapling, GitHub Actions, Azure CLI.

---

## Source Notes

- Microsoft Entra External ID is the current Microsoft CIAM direction for customer-facing apps. Microsoft documentation says External ID supports social identities such as Google and Facebook in external tenants.
- Azure AD B2C has an end-of-sale notice for new customers, so do not start a new build on Azure AD B2C unless the user already owns an eligible tenant and explicitly chooses that path.
- Entra External ID external tenant auth uses a `ciamlogin.com` authority and exposes OIDC metadata at:

```text
https://<tenant-subdomain>.ciamlogin.com/<tenant-id>/v2.0/.well-known/openid-configuration
```

- Google and Facebook are configured as identity providers inside the Entra External ID tenant and then selected in the sign-up/sign-in user flow.
- The app currently depends on:
  - Clerk package and middleware.
  - Clerk user ids stored in `User.clerkUserId`.
  - Neon serverless driver in `lib/db.ts`.
  - Cloudflare/OpenNext deployment.

## Missing Inputs From User

These are required before implementation can complete end-to-end. Do not print values in chat, commits, logs, or docs.

### Azure Subscription And Resource Provisioning

```env
AZURE_SUBSCRIPTION_ID=
AZURE_TENANT_ID=
AZURE_LOCATION=australiaeast
AZURE_RESOURCE_GROUP=replyinmyvoice-prod-rg
```

Required local login/permission:

```text
Azure CLI logged in with permission to create:
- Resource group
- Azure Database for PostgreSQL Flexible Server
- Azure App Service plan
- Azure Web App
- App Service app settings
```

### Azure Database For PostgreSQL

```env
AZURE_POSTGRES_SERVER_NAME=replyinmyvoice-pg-prod
AZURE_POSTGRES_DATABASE_NAME=replyinmyvoice
AZURE_POSTGRES_ADMIN_USER=
AZURE_POSTGRES_ADMIN_PASSWORD=
```

Decision needed:

```text
Use a generated strong admin password during automation, or user provides one in local env.
```

Recommended implementation:

```text
Azure Database for PostgreSQL Flexible Server
Postgres provider remains `postgresql`
Prisma schema remains mostly unchanged
DATABASE_URL and DIRECT_URL become Azure PostgreSQL connection strings
```

### Microsoft Entra External ID

Create or provide an external tenant for customers:

```env
AZURE_EXTERNAL_ID_TENANT_ID=
AZURE_EXTERNAL_ID_TENANT_SUBDOMAIN=
AZURE_EXTERNAL_ID_AUTHORITY=https://<tenant-subdomain>.ciamlogin.com/<tenant-id>/v2.0
AZURE_EXTERNAL_ID_CLIENT_ID=
AZURE_EXTERNAL_ID_CLIENT_SECRET=
AZURE_EXTERNAL_ID_WELL_KNOWN=https://<tenant-subdomain>.ciamlogin.com/<tenant-id>/v2.0/.well-known/openid-configuration
```

Required Entra configuration:

```text
External tenant created
Web application registered
Redirect URI added:
  https://replyinmyvoice.com/api/auth/callback/azure-external-id
Local redirect URI added for testing:
  http://localhost:3000/api/auth/callback/azure-external-id
Sign-up/sign-in user flow created
Reply In My Voice application added to that user flow
Token includes stable subject and email claims
```

### Google Sign-In

Google is configured in Entra, not directly in the app.

Required from Google Cloud Console:

```env
GOOGLE_CLIENT_ID_FOR_ENTRA=
GOOGLE_CLIENT_SECRET_FOR_ENTRA=
```

Required dashboard actions:

```text
Create Google OAuth client
Add Entra redirect URI required by Microsoft docs/admin center
Add consent screen app name, support email, and domain
Add replyinmyvoice.com as an authorized domain
Add Google client id/secret to Entra External ID identity providers
Select Google in the Entra user flow
```

### Facebook Sign-In

Facebook is configured in Entra, not directly in the app.

Required from Meta Developer Console:

```env
FACEBOOK_APP_ID_FOR_ENTRA=
FACEBOOK_APP_SECRET_FOR_ENTRA=
```

Required dashboard/product URLs:

```env
FACEBOOK_PRIVACY_POLICY_URL=https://replyinmyvoice.com/privacy
FACEBOOK_DATA_DELETION_URL=
```

Required dashboard actions:

```text
Create Meta app
Configure Facebook Login
Add the Entra redirect URI required by Microsoft docs/admin center
Add privacy policy URL
Add user data deletion URL or instructions URL
Add Facebook app id/secret to Entra External ID identity providers
Select Facebook in the Entra user flow
```

### Auth.js Runtime

```env
AUTH_SECRET=
AUTH_TRUST_HOST=true
NEXT_PUBLIC_AUTH_SIGN_IN_URL=/sign-in
NEXT_PUBLIC_AUTH_SIGN_UP_URL=/sign-up
NEXT_PUBLIC_AUTH_AFTER_SIGN_IN_URL=/app
NEXT_PUBLIC_AUTH_AFTER_SIGN_UP_URL=/app
ADMIN_AUTH_SUBJECTS=
```

Generate `AUTH_SECRET` locally:

```bash
openssl rand -base64 32
```

`ADMIN_EMAILS=chuanqiao1128@gmail.com` can remain the primary admin gate.

## Important Architecture Decision

### Why App Service Is The Recommended Runtime

Current production is Cloudflare Workers/OpenNext and current database access uses Neon’s serverless driver:

```text
lib/db.ts -> @neondatabase/serverless
```

Azure Database for PostgreSQL is a normal Postgres service. To avoid adding another third-party data proxy after removing Neon, deploy the Next.js server runtime to Azure App Service where normal Node database clients work. Cloudflare can still host DNS and proxy `replyinmyvoice.com`, but the app runtime should be Azure App Service for this migration.

Rejected alternatives for this migration:

```text
Keep Cloudflare Worker + Azure PostgreSQL + add Cloudflare Hyperdrive:
  Works conceptually, but keeps another Cloudflare data layer in the DB path.

Move to Azure SQL:
  More invasive because Prisma provider, migrations, raw SQL, and type assumptions change.

Use Azure AD B2C:
  Not preferred for new work because Microsoft’s current customer identity direction is Entra External ID.
```

## File Structure

### Create

```text
auth.ts
app/api/auth/[...nextauth]/route.ts
lib/auth.ts
lib/auth-users.ts
lib/azure-postgres.ts
lib/legacy-user-linking.ts
tests/unit/auth-admin.test.ts
tests/unit/auth-user-upsert.test.ts
tests/unit/stripe-auth-metadata.test.ts
tests/unit/azure-db-config.test.ts
.github/workflows/azure-app-service.yml
docs/azure-auth-data-migration-runbook.md
```

### Modify

```text
package.json
app/layout.tsx
middleware.ts
app/sign-in/[[...sign-in]]/page.tsx
app/sign-up/[[...sign-up]]/page.tsx
components/site-header.tsx
components/app/admin-entry.tsx
lib/users.ts
lib/admin-auth.ts
lib/admin-visible.ts
lib/stripe.ts
app/api/rewrite/route.ts
app/api/stripe/checkout/route.ts
app/api/stripe/portal/route.ts
app/api/stripe/webhook/route.ts
app/app/page.tsx
app/admin/page.tsx
app/admin/rewrites/page.tsx
app/admin/rewrites/[id]/page.tsx
lib/db.ts
prisma/schema.prisma
.env.example
docs/manual-setup.md
docs/next-development-brief.md
```

### Remove After Replacement Is Verified

```text
@clerk/nextjs
@neondatabase/serverless
@prisma/adapter-neon
NEXT_PUBLIC_CLERK_*
CLERK_SECRET_KEY
ADMIN_CLERK_USER_IDS
```

## Data Model Target

Replace Clerk-specific identity with provider-neutral identity:

```prisma
model User {
  id                   String   @id @default(cuid())
  authProvider         String   @default("azure-external-id")
  authSubject          String   @unique
  authIssuer           String?
  email                String?
  stripeCustomerId     String?  @unique
  stripeSubscriptionId String?
  stripePriceId        String?
  subscriptionStatus   String   @default("inactive")
  currentPeriodEnd     DateTime?
  createdAt            DateTime @default(now())
  updatedAt            DateTime @updatedAt
}
```

Migration rule:

```text
Existing users are matched by email first when they sign in with Entra External ID.
If an existing row has the same email and no authSubject, attach the Azure authSubject to that row.
This preserves Stripe customer ids, subscriptions, rewrite usage, cost logs, and admin history.
If two rows share an email, stop and document manual reconciliation.
```

## Implementation Tasks

### Task 1: Azure/External ID Preflight

**Files:**
- Create: `docs/azure-auth-data-migration-runbook.md`

- [ ] **Step 1: Add a preflight command section**

Write this exact content into `docs/azure-auth-data-migration-runbook.md`:

```markdown
# Azure Auth And Data Migration Runbook

## Required dashboard resources

- Microsoft Entra External ID external tenant.
- Reply In My Voice web app registration.
- Sign-up/sign-in user flow.
- Google identity provider added to user flow.
- Facebook identity provider added to user flow.
- Azure Database for PostgreSQL Flexible Server.
- Azure App Service Linux web app.

## Required environment variables

See `.env.example` for the full list. Do not commit real values.

## Preflight

```bash
az account show --query "{subscriptionId:id, tenantId:tenantId, name:name}" -o table
az group show --name "$AZURE_RESOURCE_GROUP" --query "{name:name, location:location}" -o table || true
curl -fsS "$AZURE_EXTERNAL_ID_WELL_KNOWN" | node -e 'let s="";process.stdin.on("data",d=>s+=d);process.stdin.on("end",()=>{const j=JSON.parse(s); console.log(j.issuer); console.log(Boolean(j.authorization_endpoint));})'
```

Expected:

- Azure CLI returns the intended subscription.
- The resource group exists or can be created.
- External ID well-known config returns an issuer and authorization endpoint.
```

- [ ] **Step 2: Run no-secret preflight**

Run:

```bash
az account show --query "{subscriptionId:id, tenantId:tenantId, name:name}" -o table
```

Expected:

```text
A table with subscription id, tenant id, and name. Do not paste secrets into logs.
```

- [ ] **Step 3: Commit docs**

```bash
git add docs/azure-auth-data-migration-runbook.md
git commit -m "docs: add azure auth data migration runbook"
```

### Task 2: Add Auth.js And Provider-Neutral Auth Contract

**Files:**
- Modify: `package.json`
- Create: `auth.ts`
- Create: `app/api/auth/[...nextauth]/route.ts`
- Create: `lib/auth.ts`
- Test: `tests/unit/auth-admin.test.ts`

- [ ] **Step 1: Install dependencies**

Run:

```bash
npm install next-auth
```

Expected:

```text
next-auth added to package.json.
```

- [ ] **Step 2: Write admin auth allowlist test**

Create `tests/unit/auth-admin.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { isAdminIdentityAllowed } from "../../lib/admin-auth";

describe("isAdminIdentityAllowed", () => {
  it("allows configured admin emails", () => {
    expect(
      isAdminIdentityAllowed({
        email: "chuanqiao1128@gmail.com",
        authSubject: "abc",
        adminEmails: "chuanqiao1128@gmail.com",
        adminAuthSubjects: "",
      }),
    ).toBe(true);
  });

  it("allows configured provider-neutral auth subjects", () => {
    expect(
      isAdminIdentityAllowed({
        email: "someone@example.com",
        authSubject: "azure-subject-1",
        adminEmails: "",
        adminAuthSubjects: "azure-subject-1",
      }),
    ).toBe(true);
  });

  it("rejects non-admin identities", () => {
    expect(
      isAdminIdentityAllowed({
        email: "other@example.com",
        authSubject: "not-admin",
        adminEmails: "chuanqiao1128@gmail.com",
        adminAuthSubjects: "azure-subject-1",
      }),
    ).toBe(false);
  });
});
```

- [ ] **Step 3: Run test to verify current Clerk-only code fails**

Run:

```bash
npm test -- tests/unit/auth-admin.test.ts
```

Expected:

```text
FAIL because isAdminIdentityAllowed does not accept authSubject/adminAuthSubjects yet.
```

- [ ] **Step 4: Implement Auth.js config**

Create `auth.ts`:

```ts
import NextAuth from "next-auth";

function requiredEnv(name: string) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is required`);
  }
  return value;
}

export const { handlers, auth, signIn, signOut } = NextAuth({
  trustHost: true,
  session: { strategy: "jwt" },
  providers: [
    {
      id: "azure-external-id",
      name: "Microsoft Entra External ID",
      type: "oidc",
      clientId: requiredEnv("AZURE_EXTERNAL_ID_CLIENT_ID"),
      clientSecret: requiredEnv("AZURE_EXTERNAL_ID_CLIENT_SECRET"),
      wellKnown: requiredEnv("AZURE_EXTERNAL_ID_WELL_KNOWN"),
      authorization: { params: { scope: "openid profile email" } },
      checks: ["pkce", "state"],
      profile(profile) {
        return {
          id: String(profile.sub),
          name:
            typeof profile.name === "string"
              ? profile.name
              : typeof profile.preferred_username === "string"
                ? profile.preferred_username
                : null,
          email:
            typeof profile.email === "string"
              ? profile.email
              : typeof profile.preferred_username === "string"
                ? profile.preferred_username
                : null,
          image: null,
        };
      },
    },
  ],
  callbacks: {
    jwt({ token, account, profile }) {
      if (account?.provider) {
        token.authProvider = account.provider;
      }
      if (profile?.sub) {
        token.authSubject = String(profile.sub);
      }
      if (process.env.AZURE_EXTERNAL_ID_AUTHORITY) {
        token.authIssuer = process.env.AZURE_EXTERNAL_ID_AUTHORITY;
      }
      return token;
    },
    session({ session, token }) {
      session.user.id = String(token.authSubject ?? token.sub ?? "");
      session.user.authProvider = String(token.authProvider ?? "azure-external-id");
      session.user.authSubject = String(token.authSubject ?? token.sub ?? "");
      session.user.authIssuer =
        typeof token.authIssuer === "string" ? token.authIssuer : null;
      return session;
    },
  },
});
```

Create `app/api/auth/[...nextauth]/route.ts`:

```ts
import { handlers } from "../../../../auth";

export const { GET, POST } = handlers;
```

Create `lib/auth.ts`:

```ts
import { auth } from "../auth";

export type AppSessionUser = {
  authProvider: string;
  authSubject: string;
  authIssuer: string | null;
  email: string | null;
  name?: string | null;
};

export async function getCurrentSessionUser(): Promise<AppSessionUser | null> {
  const session = await auth();
  const user = session?.user;
  const authSubject = user?.authSubject || user?.id;

  if (!authSubject) {
    return null;
  }

  return {
    authProvider: user.authProvider || "azure-external-id",
    authSubject,
    authIssuer: user.authIssuer ?? null,
    email: user.email ?? null,
    name: user.name ?? null,
  };
}
```

- [ ] **Step 5: Add NextAuth type augmentation**

Create `types/next-auth.d.ts`:

```ts
import "next-auth";
import "next-auth/jwt";

declare module "next-auth" {
  interface User {
    authProvider?: string;
    authSubject?: string;
    authIssuer?: string | null;
  }

  interface Session {
    user: {
      id: string;
      authProvider: string;
      authSubject: string;
      authIssuer: string | null;
      name?: string | null;
      email?: string | null;
      image?: string | null;
    };
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    authProvider?: string;
    authSubject?: string;
    authIssuer?: string | null;
  }
}
```

- [ ] **Step 6: Make admin allowlist provider-neutral**

Modify `lib/admin-auth.ts` so it exports:

```ts
export function isAdminIdentityAllowed({
  email,
  authSubject,
  adminEmails,
  adminAuthSubjects,
}: {
  email?: string | null;
  authSubject?: string | null;
  adminEmails?: string;
  adminAuthSubjects?: string;
}) {
  const emails = parseList(adminEmails).map((item) => item.toLowerCase());
  const subjects = parseList(adminAuthSubjects);
  const normalizedEmail = email?.trim().toLowerCase() ?? "";
  const normalizedSubject = authSubject?.trim() ?? "";

  return (
    (normalizedEmail.length > 0 && emails.includes(normalizedEmail)) ||
    (normalizedSubject.length > 0 && subjects.includes(normalizedSubject))
  );
}
```

Then update `getAdminUser` to call `getCurrentSessionUser()` and use:

```ts
adminAuthSubjects: optionalEnv("ADMIN_AUTH_SUBJECTS", ""),
```

- [ ] **Step 7: Run auth admin test**

Run:

```bash
npm test -- tests/unit/auth-admin.test.ts
```

Expected:

```text
PASS.
```

- [ ] **Step 8: Commit**

```bash
git add package.json package-lock.json auth.ts app/api/auth lib/auth.ts lib/admin-auth.ts types/next-auth.d.ts tests/unit/auth-admin.test.ts
git commit -m "feat: add azure external id auth contract"
```

### Task 3: Replace Clerk User Persistence

**Files:**
- Modify: `prisma/schema.prisma`
- Modify: `lib/users.ts`
- Create: `lib/legacy-user-linking.ts`
- Test: `tests/unit/auth-user-upsert.test.ts`

- [ ] **Step 1: Write user linking tests**

Create `tests/unit/auth-user-upsert.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { shouldLinkExistingUserByEmail } from "../../lib/legacy-user-linking";

describe("shouldLinkExistingUserByEmail", () => {
  it("links an Azure identity to an existing email row when authSubject is missing", () => {
    expect(
      shouldLinkExistingUserByEmail({
        existingEmail: "User@Example.com",
        existingAuthSubject: null,
        sessionEmail: "user@example.com",
      }),
    ).toBe(true);
  });

  it("does not link when the email differs", () => {
    expect(
      shouldLinkExistingUserByEmail({
        existingEmail: "old@example.com",
        existingAuthSubject: null,
        sessionEmail: "new@example.com",
      }),
    ).toBe(false);
  });

  it("does not link when a provider-neutral auth subject already exists", () => {
    expect(
      shouldLinkExistingUserByEmail({
        existingEmail: "user@example.com",
        existingAuthSubject: "existing-subject",
        sessionEmail: "user@example.com",
      }),
    ).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
npm test -- tests/unit/auth-user-upsert.test.ts
```

Expected:

```text
FAIL because lib/legacy-user-linking.ts does not exist.
```

- [ ] **Step 3: Add provider-neutral user columns**

Modify `prisma/schema.prisma`:

```prisma
model User {
  id                   String   @id @default(cuid())
  authProvider         String   @default("azure-external-id")
  authSubject          String?  @unique
  authIssuer           String?
  clerkUserId          String?  @unique
  email                String?
  stripeCustomerId     String?  @unique
  stripeSubscriptionId String?
  stripePriceId        String?
  subscriptionStatus   String   @default("inactive")
  currentPeriodEnd     DateTime?
  createdAt            DateTime @default(now())
  updatedAt            DateTime @updatedAt

  usages          RewriteUsage[]
  learningSamples RewriteLearningSample[]
  costLogs        RewriteCostLog[]
}
```

Important:

```text
Keep clerkUserId nullable for one migration only so existing rows do not break.
The application must not read or write clerkUserId after this task.
After production migration and user reconciliation, create a later cleanup migration to drop clerkUserId.
```

- [ ] **Step 4: Create legacy linking helper**

Create `lib/legacy-user-linking.ts`:

```ts
export function normalizeEmail(email: string | null | undefined) {
  return email?.trim().toLowerCase() ?? "";
}

export function shouldLinkExistingUserByEmail({
  existingEmail,
  existingAuthSubject,
  sessionEmail,
}: {
  existingEmail: string | null | undefined;
  existingAuthSubject: string | null | undefined;
  sessionEmail: string | null | undefined;
}) {
  return (
    normalizeEmail(existingEmail).length > 0 &&
    normalizeEmail(existingEmail) === normalizeEmail(sessionEmail) &&
    !existingAuthSubject
  );
}
```

- [ ] **Step 5: Replace lib/users.ts**

Replace Clerk imports with `getCurrentSessionUser()` and implement:

```ts
export async function upsertUserFromSession(sessionUser: AppSessionUser): Promise<User> {
  const sql = getSql();
  const rows = (await sql`
    INSERT INTO "User" (
      "id",
      "authProvider",
      "authSubject",
      "authIssuer",
      "email",
      "createdAt",
      "updatedAt"
    )
    VALUES (
      ${createId()},
      ${sessionUser.authProvider},
      ${sessionUser.authSubject},
      ${sessionUser.authIssuer},
      ${sessionUser.email},
      now(),
      now()
    )
    ON CONFLICT ("authSubject")
    DO UPDATE SET
      "authProvider" = EXCLUDED."authProvider",
      "authIssuer" = EXCLUDED."authIssuer",
      "email" = COALESCE(EXCLUDED."email", "User"."email"),
      "updatedAt" = now()
    RETURNING *
  `) as UserRow[];

  return mapUser(rows[0]);
}
```

Also implement `findUserByAuthSubject`, `updateStripeCustomerByAuthSubject`, and `getCurrentAppUser()`.

- [ ] **Step 6: Run tests**

Run:

```bash
npm test -- tests/unit/auth-user-upsert.test.ts tests/unit/quota.test.ts
```

Expected:

```text
PASS.
```

- [ ] **Step 7: Generate migration**

Run:

```bash
npx prisma migrate dev --name add_provider_neutral_auth
```

Expected:

```text
Migration created and Prisma client regenerated.
```

- [ ] **Step 8: Commit**

```bash
git add prisma/schema.prisma prisma/migrations lib/users.ts lib/legacy-user-linking.ts tests/unit/auth-user-upsert.test.ts
git commit -m "feat: add provider neutral user identities"
```

### Task 4: Replace Clerk UI And Middleware

**Files:**
- Modify: `app/layout.tsx`
- Modify: `middleware.ts`
- Modify: `app/sign-in/[[...sign-in]]/page.tsx`
- Modify: `app/sign-up/[[...sign-up]]/page.tsx`
- Modify: `components/site-header.tsx`
- Modify: `components/app/admin-entry.tsx`

- [ ] **Step 1: Remove ClerkProvider from layout**

Replace `ClerkProvider` wrapper in `app/layout.tsx` with plain HTML/body rendering.

- [ ] **Step 2: Replace Clerk middleware**

Replace `middleware.ts` with Auth.js middleware:

```ts
export { auth as middleware } from "./auth";

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
```

- [ ] **Step 3: Replace sign-in page**

Replace Clerk SignIn component with a server action or client button that calls Auth.js:

```tsx
import { signIn } from "../../../auth";

export default function SignInPage() {
  async function signInWithAzure() {
    "use server";
    await signIn("azure-external-id", { redirectTo: "/app" });
  }

  return (
    <main className="min-h-screen bg-paper px-6 py-16 text-ink">
      <section className="mx-auto max-w-md rounded-lg border border-line bg-white p-6">
        <h1 className="text-3xl font-semibold">Sign in</h1>
        <p className="mt-3 text-sm leading-6 text-ink/65">
          Continue with email, Google, or Facebook through Microsoft Entra External ID.
        </p>
        <form action={signInWithAzure}>
          <button className="mt-6 w-full rounded-md bg-ink px-4 py-3 text-sm font-semibold text-paper" type="submit">
            Continue
          </button>
        </form>
      </section>
    </main>
  );
}
```

- [ ] **Step 4: Replace sign-up page**

Use the same provider button and text:

```tsx
import { signIn } from "../../../auth";

export default function SignUpPage() {
  async function signUpWithAzure() {
    "use server";
    await signIn("azure-external-id", { redirectTo: "/app" });
  }

  return (
    <main className="min-h-screen bg-paper px-6 py-16 text-ink">
      <section className="mx-auto max-w-md rounded-lg border border-line bg-white p-6">
        <h1 className="text-3xl font-semibold">Create your account</h1>
        <p className="mt-3 text-sm leading-6 text-ink/65">
          Sign up with email, Google, or Facebook through Microsoft Entra External ID.
        </p>
        <form action={signUpWithAzure}>
          <button className="mt-6 w-full rounded-md bg-ink px-4 py-3 text-sm font-semibold text-paper" type="submit">
            Continue
          </button>
        </form>
      </section>
    </main>
  );
}
```

- [ ] **Step 5: Replace UserButton**

In `components/site-header.tsx`, replace Clerk `SignedIn`, `SignedOut`, `UserButton` with session-aware server data passed from pages or a small custom sign-out form.

Use:

```tsx
import { signOut } from "../auth";
```

and:

```tsx
<form action={async () => {
  "use server";
  await signOut({ redirectTo: "/" });
}}>
  <button type="submit">Sign out</button>
</form>
```

- [ ] **Step 6: Run typecheck**

Run:

```bash
npm run typecheck
```

Expected:

```text
No Clerk import errors remain in UI routes.
```

- [ ] **Step 7: Commit**

```bash
git add app/layout.tsx middleware.ts app/sign-in app/sign-up components/site-header.tsx components/app/admin-entry.tsx
git commit -m "feat: replace clerk UI with external id sign in"
```

### Task 5: Update Protected API Routes And Stripe Metadata

**Files:**
- Modify: `app/api/rewrite/route.ts`
- Modify: `app/api/stripe/checkout/route.ts`
- Modify: `app/api/stripe/portal/route.ts`
- Modify: `app/api/stripe/webhook/route.ts`
- Modify: `lib/stripe.ts`
- Test: `tests/unit/stripe-auth-metadata.test.ts`

- [ ] **Step 1: Write Stripe metadata test**

Create `tests/unit/stripe-auth-metadata.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { buildCheckoutMetadata } from "../../lib/stripe";

describe("buildCheckoutMetadata", () => {
  it("uses provider-neutral auth metadata and not Clerk metadata", () => {
    const metadata = buildCheckoutMetadata({
      appUserId: "user_123",
      authSubject: "azure-subject",
      email: "user@example.com",
    });

    expect(metadata).toMatchObject({
      appUserId: "user_123",
      authSubject: "azure-subject",
      email: "user@example.com",
    });
    expect(metadata).not.toHaveProperty("clerkUserId");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
npm test -- tests/unit/stripe-auth-metadata.test.ts
```

Expected:

```text
FAIL because buildCheckoutMetadata does not exist or still uses clerkUserId.
```

- [ ] **Step 3: Add Stripe metadata helper**

In `lib/stripe.ts`, add:

```ts
export function buildCheckoutMetadata({
  appUserId,
  authSubject,
  email,
}: {
  appUserId: string;
  authSubject: string;
  email: string | null;
}) {
  return {
    appUserId,
    authSubject,
    ...(email ? { email } : {}),
  };
}
```

Use this helper in checkout session creation.

- [ ] **Step 4: Replace auth checks**

In API routes replace:

```ts
const { userId } = await auth();
```

with:

```ts
const sessionUser = await getCurrentSessionUser();
if (!sessionUser) {
  return jsonError("Authentication required.", 401);
}
```

Then call:

```ts
const user = await getCurrentAppUser();
```

where `getCurrentAppUser()` uses Auth.js session.

- [ ] **Step 5: Update webhook user lookup**

In `app/api/stripe/webhook/route.ts`, replace `clerkUserId` metadata lookup with:

```ts
const authSubject =
  session.client_reference_id ?? session.metadata?.authSubject ?? null;
```

Use `updateStripeCustomerByAuthSubject`.

- [ ] **Step 6: Run route tests/typecheck**

Run:

```bash
npm test -- tests/unit/stripe-auth-metadata.test.ts tests/unit/stripe-webhook-events.test.ts
npm run typecheck
```

Expected:

```text
PASS.
```

- [ ] **Step 7: Commit**

```bash
git add app/api lib/stripe.ts tests/unit/stripe-auth-metadata.test.ts
git commit -m "feat: use provider neutral auth in api routes"
```

### Task 6: Move Database Runtime From Neon To Azure PostgreSQL

**Files:**
- Modify: `package.json`
- Modify: `lib/db.ts`
- Create: `lib/azure-postgres.ts`
- Test: `tests/unit/azure-db-config.test.ts`

- [ ] **Step 1: Install Postgres client and remove Neon packages**

Run:

```bash
npm install postgres
npm uninstall @neondatabase/serverless @prisma/adapter-neon
```

Expected:

```text
postgres added; Neon packages removed.
```

- [ ] **Step 2: Write Azure DB config test**

Create `tests/unit/azure-db-config.test.ts`:

```ts
import { describe, expect, it } from "vitest";

import { isAzurePostgresConnectionString } from "../../lib/azure-postgres";

describe("isAzurePostgresConnectionString", () => {
  it("accepts Azure PostgreSQL hostnames", () => {
    expect(
      isAzurePostgresConnectionString(
        "postgresql://user:pass@replyinmyvoice-pg-prod.postgres.database.azure.com:5432/replyinmyvoice?sslmode=require",
      ),
    ).toBe(true);
  });

  it("rejects Neon hostnames", () => {
    expect(
      isAzurePostgresConnectionString(
        "postgresql://user:pass@ep-example.neon.tech/replyinmyvoice?sslmode=require",
      ),
    ).toBe(false);
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run:

```bash
npm test -- tests/unit/azure-db-config.test.ts
```

Expected:

```text
FAIL because lib/azure-postgres.ts does not exist.
```

- [ ] **Step 4: Add Azure DB helper**

Create `lib/azure-postgres.ts`:

```ts
export function isAzurePostgresConnectionString(value: string) {
  try {
    const url = new URL(value);
    return url.hostname.endsWith(".postgres.database.azure.com");
  } catch {
    return false;
  }
}
```

- [ ] **Step 5: Replace lib/db.ts**

Replace Neon client creation with `postgres`:

```ts
import postgres from "postgres";

import { requireEnv } from "./env";

type SqlClient = ReturnType<typeof postgres>;

let sqlClient: SqlClient | null = null;

export function getSql() {
  if (!sqlClient) {
    sqlClient = postgres(requireEnv("DATABASE_URL"), {
      ssl: "require",
      max: 5,
      idle_timeout: 20,
      connect_timeout: 10,
    });
  }

  return sqlClient;
}
```

- [ ] **Step 6: Update Prisma generation if needed**

Remove Neon adapter references from generated config/imports. Keep `provider = "postgresql"` in `prisma/schema.prisma`.

- [ ] **Step 7: Run tests/typecheck**

Run:

```bash
npm test -- tests/unit/azure-db-config.test.ts tests/unit/quota.test.ts
npm run typecheck
```

Expected:

```text
PASS.
```

- [ ] **Step 8: Commit**

```bash
git add package.json package-lock.json lib/db.ts lib/azure-postgres.ts tests/unit/azure-db-config.test.ts
git commit -m "feat: switch runtime database client to azure postgres"
```

### Task 7: Provision Azure PostgreSQL And Migrate Data

**Files:**
- Modify: `docs/azure-auth-data-migration-runbook.md`

- [ ] **Step 1: Create resource group**

Run:

```bash
az group create \
  --name "$AZURE_RESOURCE_GROUP" \
  --location "$AZURE_LOCATION"
```

- [ ] **Step 2: Create Azure PostgreSQL Flexible Server**

Run:

```bash
az postgres flexible-server create \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name "$AZURE_POSTGRES_SERVER_NAME" \
  --location "$AZURE_LOCATION" \
  --admin-user "$AZURE_POSTGRES_ADMIN_USER" \
  --admin-password "$AZURE_POSTGRES_ADMIN_PASSWORD" \
  --database-name "$AZURE_POSTGRES_DATABASE_NAME" \
  --version 16 \
  --tier Burstable \
  --sku-name Standard_B1ms \
  --storage-size 32 \
  --public-access 0.0.0.0
```

Security note:

```text
For first migration, public access is used so GitHub Actions/Azure App Service can connect.
After App Service VNet integration is configured, tighten firewall rules.
```

- [ ] **Step 3: Build Azure connection strings**

Set locally:

```env
DATABASE_URL=postgresql://<admin-user>:<password>@<server>.postgres.database.azure.com:5432/<db>?sslmode=require
DIRECT_URL=postgresql://<admin-user>:<password>@<server>.postgres.database.azure.com:5432/<db>?sslmode=require
```

- [ ] **Step 4: Apply migrations**

Run:

```bash
npx prisma migrate deploy
```

Expected:

```text
All existing migrations applied to Azure PostgreSQL.
```

- [ ] **Step 5: Export Neon data**

Run with old Neon `DIRECT_URL` in a separate shell variable:

```bash
pg_dump "$NEON_DIRECT_URL" \
  --format=custom \
  --no-owner \
  --no-acl \
  --file /tmp/replyinmyvoice-neon.dump
```

- [ ] **Step 6: Restore into Azure PostgreSQL**

Run:

```bash
pg_restore \
  --dbname "$DIRECT_URL" \
  --no-owner \
  --no-acl \
  --clean \
  --if-exists \
  /tmp/replyinmyvoice-neon.dump
```

- [ ] **Step 7: Re-run migrations after restore**

Run:

```bash
npx prisma migrate deploy
```

- [ ] **Step 8: Smoke check user/subscription tables**

Run:

```bash
node -e 'const postgres=require("postgres"); const sql=postgres(process.env.DATABASE_URL,{ssl:"require"}); Promise.all([sql`select count(*)::int as count from "User"`, sql`select count(*)::int as count from "RewriteUsage"`, sql`select count(*)::int as count from "StripeEvent"`]).then(r=>{console.log(r.map(x=>x[0].count).join(",")); return sql.end();})'
```

Expected:

```text
Counts print without exposing row data.
```

- [ ] **Step 9: Commit runbook update**

```bash
git add docs/azure-auth-data-migration-runbook.md
git commit -m "docs: record azure postgres migration steps"
```

### Task 8: Deploy Next.js To Azure App Service

**Files:**
- Create: `.github/workflows/azure-app-service.yml`
- Modify: `package.json`
- Modify: `docs/manual-setup.md`

- [ ] **Step 1: Add start script if missing**

In `package.json`, ensure:

```json
"start": "next start"
```

- [ ] **Step 2: Create Azure App Service plan**

Run:

```bash
az appservice plan create \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name replyinmyvoice-appservice-plan-prod \
  --is-linux \
  --sku B1
```

- [ ] **Step 3: Create Azure Web App**

Run:

```bash
az webapp create \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --plan replyinmyvoice-appservice-plan-prod \
  --name replyinmyvoice-web-prod \
  --runtime "NODE:20-lts"
```

- [ ] **Step 4: Set App Service settings**

Run:

```bash
az webapp config appsettings set \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name replyinmyvoice-web-prod \
  --settings \
  NEXT_PUBLIC_APP_URL="https://replyinmyvoice.com" \
  AUTH_SECRET="$AUTH_SECRET" \
  AUTH_TRUST_HOST="true" \
  AZURE_EXTERNAL_ID_CLIENT_ID="$AZURE_EXTERNAL_ID_CLIENT_ID" \
  AZURE_EXTERNAL_ID_CLIENT_SECRET="$AZURE_EXTERNAL_ID_CLIENT_SECRET" \
  AZURE_EXTERNAL_ID_WELL_KNOWN="$AZURE_EXTERNAL_ID_WELL_KNOWN" \
  AZURE_EXTERNAL_ID_AUTHORITY="$AZURE_EXTERNAL_ID_AUTHORITY" \
  DATABASE_URL="$DATABASE_URL" \
  DIRECT_URL="$DIRECT_URL" \
  STRIPE_SECRET_KEY="$STRIPE_SECRET_KEY" \
  STRIPE_PRICE_ID="$STRIPE_PRICE_ID" \
  STRIPE_WEBHOOK_SECRET="$STRIPE_WEBHOOK_SECRET" \
  OPENAI_API_KEY="$OPENAI_API_KEY" \
  SAPLING_API_KEY="$SAPLING_API_KEY" \
  ADMIN_EMAILS="$ADMIN_EMAILS" \
  ADMIN_AUTH_SUBJECTS="$ADMIN_AUTH_SUBJECTS"
```

- [ ] **Step 5: Add GitHub Actions deploy workflow**

Create `.github/workflows/azure-app-service.yml`:

```yaml
name: azure-app-service

on:
  push:
    branches: [main]
  workflow_dispatch:

jobs:
  build-test-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: npm
      - run: npm ci
      - run: npm run prisma:generate
      - run: npm run typecheck
      - run: npm run test
      - run: npm run build
      - uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      - run: npx prisma migrate deploy
        env:
          DATABASE_URL: ${{ secrets.AZURE_DATABASE_URL }}
          DIRECT_URL: ${{ secrets.AZURE_DIRECT_URL }}
      - uses: azure/webapps-deploy@v3
        with:
          app-name: replyinmyvoice-web-prod
          package: .
```

Required GitHub secrets:

```text
AZURE_CREDENTIALS
AZURE_DATABASE_URL
AZURE_DIRECT_URL
```

- [ ] **Step 6: Commit**

```bash
git add package.json .github/workflows/azure-app-service.yml docs/manual-setup.md
git commit -m "ci: deploy next app to azure app service"
```

### Task 9: Remove Clerk And Neon Surface Area

**Files:**
- Modify: `.env.example`
- Modify: `docs/manual-setup.md`
- Modify: `docs/next-development-brief.md`
- Modify: `wrangler.jsonc`
- Modify: `.github/workflows/cloudflare-worker.yml`

- [ ] **Step 1: Remove Clerk env names**

Remove from `.env.example`:

```env
NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY
CLERK_SECRET_KEY
NEXT_PUBLIC_CLERK_SIGN_IN_URL
NEXT_PUBLIC_CLERK_SIGN_UP_URL
NEXT_PUBLIC_CLERK_AFTER_SIGN_IN_URL
NEXT_PUBLIC_CLERK_AFTER_SIGN_UP_URL
ADMIN_CLERK_USER_IDS
```

Add:

```env
AUTH_SECRET=
AUTH_TRUST_HOST=true
AZURE_EXTERNAL_ID_TENANT_ID=
AZURE_EXTERNAL_ID_TENANT_SUBDOMAIN=
AZURE_EXTERNAL_ID_AUTHORITY=
AZURE_EXTERNAL_ID_CLIENT_ID=
AZURE_EXTERNAL_ID_CLIENT_SECRET=
AZURE_EXTERNAL_ID_WELL_KNOWN=
ADMIN_AUTH_SUBJECTS=
```

- [ ] **Step 2: Remove old Cloudflare deployment as production path**

Update docs to say:

```text
Production runtime is Azure App Service.
Cloudflare remains DNS/proxy only.
The old Cloudflare Worker route must not be considered production after cutover.
```

- [ ] **Step 3: Search for old names**

Run:

```bash
rg -n "Clerk|clerk|CLERK|Neon|neon|@clerk|@neondatabase|adapter-neon|clerkUserId|ADMIN_CLERK" app components lib prisma tests docs package.json .env.example .github wrangler.jsonc
```

Expected:

```text
Only migration history and explicitly marked legacy migration notes may remain.
No runtime code imports Clerk or Neon packages.
```

- [ ] **Step 4: Run verification**

Run:

```bash
npm run prisma:generate
npm run typecheck
npm test
npm run build
```

Expected:

```text
All pass.
```

- [ ] **Step 5: Commit**

```bash
git add .env.example docs/manual-setup.md docs/next-development-brief.md wrangler.jsonc .github package.json package-lock.json
git commit -m "chore: remove clerk and neon production surface"
```

### Task 10: Production Cutover And Smoke Test

**Files:**
- Modify: `docs/azure-auth-data-migration-runbook.md`

- [ ] **Step 1: Test Azure App Service URL**

Run:

```bash
curl -I https://replyinmyvoice-web-prod.azurewebsites.net
curl -fsS https://replyinmyvoice-web-prod.azurewebsites.net/api/health/db
```

Expected:

```text
HTTP 200 for app.
{"ok":true} for DB health.
```

- [ ] **Step 2: Test unauthenticated rewrite**

Run:

```bash
curl -sS -D - \
  -X POST https://replyinmyvoice-web-prod.azurewebsites.net/api/rewrite \
  -H 'content-type: application/json' \
  --data '{"roughDraftReply":"Hello, this is a test draft.","tone":"warm"}'
```

Expected:

```text
401 Authentication required.
```

- [ ] **Step 3: Manual auth smoke**

Open:

```text
https://replyinmyvoice-web-prod.azurewebsites.net/sign-in
```

Expected:

```text
Entra External ID login page appears.
Email, Google, and Facebook sign-in options are visible if configured in the user flow.
After sign-in, user lands on /app.
The User row is created or linked by email.
```

- [ ] **Step 4: Stripe smoke**

Use a non-live-charge path first:

```text
Open /app as signed-in user.
Confirm free quota appears.
Start checkout only if live payment testing is explicitly approved.
```

- [ ] **Step 5: DNS cutover**

Only after Azure URL passes smoke:

```text
Point replyinmyvoice.com to Azure App Service custom domain.
Keep Cloudflare proxy enabled if desired.
Update Entra redirect URI to include https://replyinmyvoice.com/api/auth/callback/azure-external-id.
Update Stripe webhook endpoint only if route host changes.
```

- [ ] **Step 6: Final smoke on production domain**

Run:

```bash
curl -I https://replyinmyvoice.com
curl -fsS https://replyinmyvoice.com/api/health/db
```

Expected:

```text
App is served by Azure-backed deployment and DB health is ok.
```

- [ ] **Step 7: Commit runbook results**

```bash
git add docs/azure-auth-data-migration-runbook.md
git commit -m "docs: record azure auth data migration verification"
```

## Stop Conditions

Only stop and ask the user if one of these happens:

```text
Azure permission denied creating App Service, External ID, or PostgreSQL resources.
Entra External ID tenant creation or user-flow setup requires dashboard-only action unavailable from CLI/Graph permissions.
Google or Facebook developer console requires business verification, privacy URL, data deletion URL, or review that cannot be automated.
Azure PostgreSQL cannot be reached from the selected runtime without opening unsafe firewall rules.
Live Stripe payment or real charge is required.
Any operation would expose or commit secrets.
```

## Verification Gate

Before final deployment claim:

```bash
npm run prisma:generate
npm run typecheck
npm test
npm run build
rg -n "Clerk|clerk|CLERK|@clerk|Neon|neon|@neondatabase|adapter-neon|ADMIN_CLERK" app components lib tests package.json .env.example .github || true
curl -fsS https://replyinmyvoice.com/api/health/db
```

Expected:

```text
Build/test pass.
No runtime Clerk/Neon references remain.
Production DB health is ok.
Sign-in uses Entra External ID.
Google and Facebook are visible in the Entra-hosted sign-in flow if provider dashboards are configured.
```

## Implementation Notes For Future Agent

- Do not silently delete old users. Preserve subscription state by linking by email where safe.
- Do not expose social provider secrets in `.env.example`, docs, logs, or commits.
- Do not do a real Stripe payment unless the user explicitly asks.
- Keep the active pricing model unchanged; as of the pricing-redesign wave, that is trial-code access plus Quick, Value, and Pro/API rewrite packs.
- If social login is not visible, the likely cause is Entra user-flow identity provider configuration, not app code.
- If `email` is absent from Entra tokens, fix Entra token claims before changing app logic.
- After cutover, create a follow-up cleanup migration to drop `clerkUserId` once there is confidence no rollback is needed.
