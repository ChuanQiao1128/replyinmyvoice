# Promo-Code Trial Specification

> Replace the automatic "3 free rewrites on sign-up" with a **redeemable promo code**: a
> signed-in user enters an alphanumeric code to unlock 3 trial rewrites. Launch with one
> universal code for everyone, but build a commercial-grade, multi-code, analytics-ready
> system underneath.

- **Status:** Design only — no code written yet (per owner instruction).
- **Branch:** `feat/promo-code-trial`
- **Author:** Claude Code (supervisor), via `system-spec-synthesis` (+ `state-machine-modeling`, `data-module-review` lenses).
- **Date:** 2026-06-02
- **Implementation model:** substantive source changes are delegated to Codex workers (supervisor mode); this doc is the brief.

---

## TL;DR (the one big idea)

The live backend **already has everything we need to grant trial rewrites**: the
`RewriteCredit` table + `QuotaService` "credit overflow" consumption + `/api/me` per-source
balance display. Purchased packs and admin grants both flow through it. So:

1. **Stop auto-granting free rewrites.** Change the free baseline quota from `3` → `0`
   (one constant in `AccountService.GetUsagePlan`) + a one-time data migration so display
   and enforcement agree.
2. **Add a promo layer.** Two new tables (`PromoCode` definition + `PromoCodeRedemption`
   ledger), one redeem service, one HTTP endpoint, one Next.js proxy, and a small UI.
3. **Redemption = grant a credit.** A successful redemption inserts a
   `RewriteCredit { Source = "PROMO", AmountGranted = 3, ExpiresAt = … }` — the *exact* row
   shape that `AdminService.GrantCreditsAsync` and the Stripe purchase path already create.
   **No change to the consumption / paywall / quota-race logic at all.**

The net new surface area is small and isolated. The risk is concentrated in two places: (a)
the free-baseline = 0 cutover (display vs. enforcement consistency), and (b) the redemption
transaction's idempotency/race handling. Both are addressed below.

---

## Context

### Product change requested

| | Today | After this change |
|---|---|---|
| New signed-in user | Gets **3 free lifetime rewrites** automatically | Gets **0**; must **redeem a code** to unlock 3 |
| Free trial trigger | Implicit, frictionless | Explicit user action (enter a code) |
| Code model | none | **One universal alphanumeric code** for everyone (commercial-grade system underneath) |
| Expiry | n/a | Code stops working after **end of August 2026** |
| Goal | Maximize free funnel | **Filter for users with genuine intent**, not no-threshold free |

### Why (owner's stated intent)

> "筛选出真正有需求的用户来使用这三次机会，而不是单纯的无门槛免费."
> *Select for users who actually have a need, rather than no-threshold free.*

The filter is **the act of obtaining + entering a code**, plus the existing sign-up + email
verification. See [How this filters for intent — and its honest limits](#how-this-filters-for-intent--and-its-honest-limits).

### Grounding — what the live system actually is (verified 2026-06-02)

Production backend is **C# / .NET on Azure Functions + Azure SQL (EF Core)**. The TypeScript
`lib/` pipeline and `lib/generated/prisma/**` are **dead Slice-7 code** — ignore them (note:
the dead Prisma schema shows an old `User.referralCode` field; that is **not** the live model).

| Concern | Live location |
|---|---|
| Free "3" constant | `backend-dotnet/src/ReplyInMyVoice.Infrastructure/Services/AccountService.cs` → `GetUsagePlan()` returns `new AccountUsagePlan("free", "free:lifetime", 3)` |
| Account summary (`/api/me`) | `AccountService.GetOrCreateAccountSummaryAsync()` — computes `remaining = periodRemaining + creditRemaining`, returns per-`Sources` breakdown with `ExpiresInDays` |
| Credit grant pattern to mirror | `AdminService.GrantCreditsAsync()` — inserts `RewriteCredit{Source="ADMIN", ExpiresAt=now+90d}` + `AdminAuditLog`, all in one `SaveChanges` |
| Credit entity | `…/Domain/Entities/RewriteCredit.cs` (`Source`, `AmountGranted`, `AmountConsumed`, `GrantedAt`, `ExpiresAt`, `RowVersion`) |
| Consumption / paywall gate | `…/Infrastructure/Services/QuotaService.cs` → `ReserveAsync()` + `FindUsableCreditAsync()` (period quota first, then usable credits; **no change needed**) |
| Serializable tx helper pattern | `StripeEventService.ExecuteInTransactionAsync()` (execution strategy + `IsolationLevel.Serializable`) |
| DbContext | `…/Infrastructure/Data/AppDbContext.cs` (DbSets + `OnModelCreating` conventions) |
| Migrations | `…/Infrastructure/Migrations/` (`YYYYMMDDhhmmss_Name`), applied on push to `main` via `.github/workflows/dotnet-azure.yml` (`dotnet ef database update` on live Azure SQL) |
| HTTP endpoints | `…/Functions/Functions/*HttpFunctions.cs` — isolated-worker `[Function]` + `HttpTrigger(… Route = "…")`; auth via `FunctionAuthResolver` (Entra `oid` → `AppUser.ExternalAuthUserId`) |
| Frontend proxy | `app/api/me/route.ts` → `${getAzureApiBaseUrl()}/api/me` |
| `/app` quota UI | `app/app/page.tsx`, `components/app/subscription-status.tsx`, `components/app/paywall-card.tsx` |
| Copy contract tests | `tests/unit/pricing-auth-visual-system.test.ts`, `tests/unit/workspace-copy.test.ts` |

There is **no existing promo / coupon / redeem code** in the live stack. The C# `Referral`
entity exists but is **vestigial** (declared in `AppDbContext` only, used by no service); we do
not extend it, though we borrow its `SignupIpHash` anti-abuse idea.

---

## Goals

1. New signed-in users receive **0** automatic rewrites.
2. A signed-in user can **redeem an alphanumeric code** to receive **3** trial rewrites.
3. The code is **one universal value** at launch, distributed through owner-chosen channels.
4. The code **expires** (default end of August 2026 NZ time); redemption after expiry fails with a clear message.
5. **Commercial-grade**: multi-code capable, per-code configurable, abuse-resistant, idempotent, race-safe.
6. **Analytics**: persist enough to answer "how many distinct users redeemed code X, and when" to evaluate promo effectiveness.
7. No regression to purchase/paywall/quota/Stripe behavior.

## Non-Goals

- **Not** percentage/amount discounts on Stripe checkout (this grants *rewrites*, not money off). The schema leaves room (`PromoCodeKind`) but only `TrialCredits` is implemented now.
- **Not** per-user unique codes at launch (architecture supports them later; see limits section).
- **Not** referral/affiliate mechanics (separate concern; `Referral` table untouched).
- **Not** changing the rewrite engine, Stripe packs, pricing, or paid quota (90/mo).
- **Not** a user-visible "account settings" page redesign — redeem entry lives in `/app` and `/pricing`.
- **Not** automatic free credits for anonymous (signed-out) users — redemption requires auth.

---

## Current System (free-quota flow today)

```
sign up + verify email (Entra native) ──▶ AppUser{SubscriptionStatus=Inactive}
        │
        ▼
GET /api/me ──▶ GetUsagePlan() = ("free","free:lifetime",3)
        │        remaining = max(3 - used - reserved, 0) + activeCredits
        ▼
/app shows workspace while remaining > 0
        │
POST /api/rewrite ──▶ QuotaService.ReserveAsync(periodKey="free:lifetime", quota=3)
        │   period quota first; if exhausted → FindUsableCreditAsync() (purchased packs)
        │   if neither → 402 / QuotaExceeded
        ▼
remaining == 0 ──▶ /app shows paywall-card ("Buy a rewrite pack")
```

Key insight: **the free "3" is a `UsagePeriod.QuotaLimit`, not a `RewriteCredit`.** Credits
are the *overflow* mechanism. We will flip the trial to ride on the credit mechanism.

---

## Proposed Architecture

### Decision: trial credits ride on `RewriteCredit` (Source = "PROMO"), free baseline = 0

```
sign up + verify email ──▶ AppUser{Inactive}
        │
        ▼
GET /api/me ──▶ GetUsagePlan() = ("free","free:lifetime",0)   ← change 3→0
        │        remaining = 0 + activeCredits(=0)  → exhausted=true, promo.hasRedeemed=false
        ▼
/app shows "Redeem your code" card  ← NEW empty state (NOT the buy-paywall)
        │
POST /api/promo/redeem {code}
        │   PromoService.RedeemAsync(): validate → in Serializable tx:
        │     insert RewriteCredit{Source="PROMO", AmountGranted=3, ExpiresAt=now+TTL}
        │     insert PromoCodeRedemption{...}  (unique on (codeId,userId))
        │     code.RedemptionCount++           (analytics + global cap)
        ▼
GET /api/me ──▶ remaining = 0 + 3 = 3, Sources=[…, {Source="PROMO", ExpiresInDays}]
        │
        ▼
/app shows workspace (3 trial rewrites)
        │
POST /api/rewrite ──▶ QuotaService.ReserveAsync(quota=0)
        │   period quota 0 → FindUsableCreditAsync() → consumes the PROMO credit  ← UNCHANGED
        ▼
PROMO credit exhausted ──▶ /app shows paywall-card (buy packs)   (promo.hasRedeemed=true)
```

**Why this design**
- The consumption path, quota-race guard, 402 logic, paywall, and Stripe purchase grant are
  **completely unchanged**. `FindUsableCreditAsync` already orders "soonest-expiring first",
  so a PROMO credit is consumed before a later-expiring purchased pack — correct behavior.
- `/api/me` already sums credits into `remaining` and emits a per-source row with
  `ExpiresInDays`, so the UI can show "3 trial rewrites · expires in N days" with near-zero
  backend display work.
- Redemption reuses the proven `AdminService.GrantCreditsAsync` grant shape and the
  `ExecuteInTransactionAsync` serializable-tx pattern.

### Component ownership

| Component | New / changed | Layer |
|---|---|---|
| `PromoCode`, `PromoCodeRedemption` entities | **new** | Domain |
| `AppDbContext` DbSets + config + migration | **changed** | Infrastructure |
| `PromoService` (validate + redeem + status) | **new** | Infrastructure |
| `AccountService.GetUsagePlan` free quota 3→0 | **changed** | Infrastructure |
| `AccountService` summary: add `promo` block + friendly source labels | **changed** | Infrastructure |
| `PromoHttpFunctions` (`POST promo/redeem`, `GET promo/status`) | **new** | Functions |
| `AdminService` / `AdminHttpFunctions`: create/list codes + redemption stats | **new** | Infrastructure/Functions |
| `app/api/promo/redeem/route.ts`, `app/api/promo/status/route.ts` proxies | **new** | Next.js |
| `components/app/redeem-code-card.tsx` + `/app` state logic | **new/changed** | Next.js |
| Landing/pricing/footer/auth copy | **changed** | Next.js |
| Copy contract tests | **changed** | tests |

---

## Data Model

Follow existing EF conventions: `Guid Id = Guid.NewGuid()`, `Guid RowVersion` concurrency
token, `DateTimeOffset` timestamps, enums stored via `.HasConversion<string>()` with
`HasMaxLength`, unique/composite indexes in `OnModelCreating`.

### `PromoCode` (code definition — the knob)

```csharp
public sealed class PromoCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }              // normalized: trimmed, UPPERCASE, no spaces/dashes. UNIQUE.
    public string? Description { get; set; }               // internal label, e.g. "Launch universal trial"
    public PromoCodeKind Kind { get; set; } = PromoCodeKind.TrialCredits;
    public int CreditsGranted { get; set; }                // e.g. 3
    public int GrantTtlDays { get; set; } = 90;            // lifetime of the granted credit after redemption
    public DateTimeOffset ValidFrom { get; set; }          // redemption window start
    public DateTimeOffset ValidUntil { get; set; }         // redemption window end (e.g. 2026-08-31T23:59:59+12:00)
    public int? MaxRedemptionsGlobal { get; set; }         // null = unlimited
    public int MaxRedemptionsPerUser { get; set; } = 1;    // launch = 1
    public int RedemptionCount { get; set; }               // denormalized counter (cap check + quick stats)
    public bool IsActive { get; set; } = true;             // admin kill switch (instant disable without delete)
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    public ICollection<PromoCodeRedemption> Redemptions { get; } = new List<PromoCodeRedemption>();
}

public enum PromoCodeKind { TrialCredits }   // extensible (PercentOff, AmountOff…) — not implemented now
```

Indexes / config:
- `HasIndex(x => x.Code).IsUnique()`
- `Property(x => x.Code).HasMaxLength(40)`; `Description` max 200; `Kind` `HasConversion<string>().HasMaxLength(40)`
- `Property(x => x.RowVersion).IsConcurrencyToken()`

### `PromoCodeRedemption` (event ledger — analytics source of truth)

```csharp
public sealed class PromoCodeRedemption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PromoCodeId { get; set; }
    public PromoCode? PromoCode { get; set; }
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public Guid RewriteCreditId { get; set; }              // the RewriteCredit this redemption created
    public int CreditsGranted { get; set; }
    public string CodeSnapshot { get; set; } = string.Empty;// normalized code text as redeemed (audit; survives code edits)
    public string? SignupIpHash { get; set; }              // optional anti-abuse (mirrors Referral.SignupIpHash); never raw IP
    public PromoCodeRedemptionStatus Status { get; set; } = PromoCodeRedemptionStatus.Applied;
    public DateTimeOffset RedeemedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReversedAt { get; set; }        // admin clawback
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}

public enum PromoCodeRedemptionStatus { Applied, Reversed }
```

Indexes / config:
- **`HasIndex(x => new { x.PromoCodeId, x.UserId }).IsUnique()`** — enforces one-redemption-per-user **and** is the race guard (concurrent dup → unique violation → caught as `AlreadyRedeemed`). Mirrors the `RewriteAttempt (UserId, IdempotencyKey)` unique-index precedent.
- `HasIndex(x => x.PromoCodeId)` (analytics rollups), `HasIndex(x => x.UserId)`
- FK `PromoCodeId → PromoCode` `OnDelete(Restrict)` (never cascade-delete redemption history). FK `UserId → AppUser` `OnDelete(Cascade)` (consistent with other per-user tables; account-erase path handles anonymization).
- `RewriteCreditId`: plain indexed `Guid` (no cascade FK, to avoid SQL Server multiple-cascade-path errors — same reasoning the codebase used for `Referral.RefereeId`).

> **Account deletion / GDPR:** `AccountService.DeleteAccountAsync` currently anonymizes
> `RewriteAttempt/UsagePeriod/UsageReservation/RewriteCredit`. It must be extended to also
> handle `PromoCodeRedemption` for the erased user (null the `SignupIpHash`, keep an
> anonymized count row or delete the row). Decide: keep anonymized aggregate vs. hard-delete.
> Recommended: null PII fields, keep the row (so promo totals stay accurate), set
> `Status = Reversed` only if we also reclaim credits (we don't on erase).

### Analytics query (answers the owner's question directly)

```sql
-- distinct users who redeemed a given code, and when
SELECT COUNT(*) AS redemptions, MIN(RedeemedAt) AS first, MAX(RedeemedAt) AS last
FROM   PromoCodeRedemptions
WHERE  PromoCodeId = @id AND Status = 'Applied';

-- redemptions per day (promo curve)
SELECT CAST(RedeemedAt AS date) AS day, COUNT(*) AS n
FROM   PromoCodeRedemptions WHERE PromoCodeId = @id AND Status = 'Applied'
GROUP  BY CAST(RedeemedAt AS date) ORDER BY day;
```

Plus a coarse activation signal by joining `RewriteCreditId → RewriteCredit.AmountConsumed`
("how many redeemers actually used ≥1 trial rewrite") — a direct measure of "real need".

### Migration

New migration `…_AddPromoCodes` creating both tables + indexes. Applied automatically on
merge to `main` (CI `dotnet ef database update` on live Azure SQL). Migration safety rules
from `AGENTS.md` apply: **no** `migrate reset` / `--force-reset` / table drops.

**Free-baseline data migration (separate concern, see Rollout):** within the same migration
or a dedicated one, normalize any existing `UsagePeriod` rows so display == enforcement after
the constant flips to 0 (see Rollout Plan step 2).

### Seeding the launch code

Do **not** hardcode the code value in source/env (not commercial-grade, and it would land in
the banned-term-free but still leak-prone code). Instead create it as data via the **admin
create-code endpoint** (below). Optionally a seed migration that inserts a placeholder row the
owner edits — but the admin endpoint is cleaner and reusable. The actual code string,
`ValidUntil`, and `CreditsGranted` are **owner inputs** (see Open Questions).

---

## API and Job Contracts

### Backend (C# Azure Functions) — `PromoHttpFunctions.cs`

All authenticated like `AccountHttpFunctions` (resolve Entra `oid` via `FunctionAuthResolver`;
same-origin/JWT handling consistent with existing `me` function).

**`POST /api/promo/redeem`**
```jsonc
// request
{ "code": "WELCOME2026" }       // server normalizes (trim, uppercase, strip spaces/dashes)
// 200 OK
{ "creditsGranted": 3, "totalRemaining": 3, "expiresAt": "2026-09-01T11:59:59Z",
  "source": "PROMO", "alreadyRedeemed": false }
// errors (JSON { "error": <code>, "message": <friendly> })
401 unauthorized
400 invalid_request        // missing/oversized body
422 invalid_code           // not found / inactive / not-yet-valid  (merged to limit enumeration)
422 code_expired           // past ValidUntil
409 already_redeemed       // this user already redeemed this code (idempotent; no new grant)
409 code_exhausted         // global cap reached
429 too_many_attempts      // rate limit
500 server_error
```

**`GET /api/promo/status`** *(or fold into `/api/me`; see below)*
```jsonc
// 200 OK — drives the /app empty-state branching
{ "eligible": true,            // a redeemable code generally exists & user hasn't redeemed
  "hasRedeemed": false,        // user has an Applied redemption (any code)
  "activeTrialRemaining": 0,   // remaining PROMO credits
  "trialExpiresAt": null }
```

**Recommended:** extend `/api/me` (`AccountUsageSummary`) with a small `promo` object rather
than add a separate round-trip, since `/app` already fetches `/api/me`:
```jsonc
"promo": { "hasRedeemed": false, "eligible": true, "trialRemaining": 0, "trialExpiresAt": null }
```
The UI uses `promo.hasRedeemed` to pick the empty state. Keep `GET /api/promo/status` too if a
lighter poll is wanted, but `/api/me` extension is the primary path.

### Backend admin (extend `AdminHttpFunctions.cs` / `AdminService.cs`)

- **`POST /api/admin/promo-codes`** — create a code `{code, description, creditsGranted, grantTtlDays, validFrom, validUntil, maxRedemptionsGlobal?, maxRedemptionsPerUser?}` → writes `PromoCode` + `AdminAuditLog{Action="create_promo_code"}`. (Mirrors `GrantCreditsAsync` audit pattern; admin auth already exists.)
- **`GET /api/admin/promo-codes`** — list codes with `RedemptionCount`, validity, active flag.
- **`POST /api/admin/promo-codes/{id}/disable`** — set `IsActive=false` (kill switch).
- **`GET /api/admin/promo-codes/{id}/stats`** — redemptions, distinct users, daily curve, activation rate (redeemers who consumed ≥1). Feeds the promo-effectiveness evaluation.
- Extend `AdminStatsResponse` with `promoRedemptions` total (optional).

### Frontend (Next.js proxies, mirror `app/api/me/route.ts`)

- `app/api/promo/redeem/route.ts` — `POST`, same-origin enforced (per `AGENTS.md` "Same-Origin Protection"), forwards bearer/cookie to `${getAzureApiBaseUrl()}/api/promo/redeem`.
- `app/api/promo/status/route.ts` — `GET` (only if not folding into `/api/me`).

### Jobs

None required. Expiry is evaluated at redeem time and at credit-consumption time (existing
`FindUsableCreditAsync` filters `ExpiresAt > now`). No cron. (Optional later: a reminder email
before trial credits expire — out of scope.)

---

## State and Error Handling

### State machine — `PromoCode`

```
                 ┌───────────── (admin disable) ─────────────┐
                 ▼                                            │
[Active]  (now∈[ValidFrom,ValidUntil] ∧ IsActive ∧ Redemptions<Cap)
   │  │                                                       ▲
   │  ├── now > ValidUntil ───────────────▶ [Expired]  (terminal for redemption)
   │  ├── RedemptionCount ≥ MaxGlobal ─────▶ [Exhausted]
   │  └── now < ValidFrom ─────────────────▶ [Pending]  (→ Active when time passes)
   └── IsActive=false ─────────────────────▶ [Disabled]
```
- **Redeemable iff:** `IsActive ∧ ValidFrom ≤ now ≤ ValidUntil ∧ (MaxRedemptionsGlobal == null ∨ RedemptionCount < MaxRedemptionsGlobal)`.
- These are *derived* states (computed from columns), not a stored status enum — keeps it simple and race-safe (the cap/time checks happen inside the redeem transaction).
- **Invariant:** `RedemptionCount == COUNT(redemptions WHERE Status=Applied)`.

### State machine — `PromoCodeRedemption` (per user × code)

```
(none) ──redeem success──▶ [Applied] ──admin clawback──▶ [Reversed]
   ▲                            │
   └───── illegal: second Applied for same (code,user) — blocked by unique index
```
- **Invariant:** at most one row per `(PromoCodeId, UserId)`.
- **Invariant:** every `Applied` row references a real `RewriteCredit` (created in the same tx).
- `Reversed` (admin clawback) optionally zeroes the linked `RewriteCredit` (`AmountGranted=0`) — mirror the erase path; out of scope to fully build now, but the column exists.

### Redeem algorithm (idempotent, race-safe)

```
RedeemAsync(externalAuthUserId, email, rawCode, ipHash?, now):
  normalized = Normalize(rawCode)              // trim; uppercase; strip spaces & dashes
  if !FormatValid(normalized): return InvalidCode
  user = AccountService.GetOrCreateUser(...)
  return ExecuteInTransactionAsync(Serializable):     // reuse StripeEventService pattern
    code = db.PromoCodes.SingleOrDefault(c => c.Code == normalized)
    if code == null || !code.IsActive:  return InvalidCode
    if now <  code.ValidFrom:           return InvalidCode      // not-yet-valid → generic
    if now >  code.ValidUntil:          return Expired
    if db.PromoCodeRedemptions.Any(r => r.PromoCodeId==code.Id && r.UserId==user.Id):
        return AlreadyRedeemed(existing grant)                  // idempotent: no new credit
    if code.MaxRedemptionsGlobal is int cap && code.RedemptionCount >= cap:
        return GlobalCapReached
    credit = new RewriteCredit{ UserId=user.Id, Source="PROMO",
                                AmountGranted=code.CreditsGranted, AmountConsumed=0,
                                GrantedAt=now, ExpiresAt=now.AddDays(code.GrantTtlDays) }
    db.RewriteCredits.Add(credit)
    db.PromoCodeRedemptions.Add(new {... RewriteCreditId=credit.Id, CodeSnapshot=normalized,
                                      SignupIpHash=ipHash, Status=Applied, RedeemedAt=now})
    code.RedemptionCount++; code.UpdatedAt=now; code.RowVersion=Guid.NewGuid()
    try: SaveChanges(); commit
    catch DbUpdateException when unique (PromoCodeId,UserId) violated:
        rollback; return AlreadyRedeemed     // lost a concurrent race — treat as success-ish
  return Success(credit.AmountGranted, expiresAt)
```

### Resilience / failure matrix (drives tests — `resilience-test-generation` lens)

| Scenario | Expected |
|---|---|
| Two concurrent redeems, same user+code | Exactly one `Applied` row + one PROMO credit; the other → `AlreadyRedeemed`, no double grant |
| Replay (user taps redeem twice) | Second → `409 already_redeemed`, credits unchanged (idempotent) |
| Redeem at `ValidUntil` boundary | `now ≤ ValidUntil` passes; `now > ValidUntil` → `code_expired` (use a single `now` per request) |
| Global cap race (cap=N, N+1 concurrent) | At most N `Applied`; overflow → `code_exhausted` (cap re-checked inside tx; unique index is the hard backstop) |
| Grant succeeds, then rewrite consumes it | `FindUsableCreditAsync` picks the PROMO credit (soonest-expiring first) — unchanged path |
| DB transient failure | Execution strategy retries (Serializable + `CreateExecutionStrategy`) |
| Sapling/engine fails on the trial rewrite | Existing no-charge-on-failure logic releases the reservation — credit not consumed (unchanged) |
| Unknown / malformed code | `422 invalid_code` (no enumeration of which codes exist) |
| Brute-force redeem attempts | `429 too_many_attempts` (per-user + per-IP limiter) |

---

## Security and Privacy

- **Auth required** for redeem/status (Entra `oid`). No anonymous grants.
- **Enumeration resistance:** unknown/inactive/not-yet-valid all return the *same* generic
  `invalid_code`. Only `expired` and `already_redeemed` are distinguished (useful, low-risk).
- **Rate limiting:** redeem endpoint limited per user and per IP (reuse the existing auth
  rate-limit approach). A single universal code makes brute force moot today, but this is
  required once per-user-unique codes ship.
- **No secrets in source:** the code value lives in the DB, created via admin endpoint — not
  in `.env.local`, not hardcoded. Never log the code value in app logs.
- **Banned terms:** none of the new identifiers/copy may contain `humanizer|bypass|undetect|detector|evade`. The CI grep scans `app components public lib`; this doc lives in `plans/` (not scanned) but proposed code names (`PromoCode`, `RedeemCodeCard`, etc.) are clean. Run the guard before completion.
- **PII:** `SignupIpHash` is a salted hash, never a raw IP (mirror `Referral.SignupIpHash`). Account-erase must clear it (see Data Model note).
- **Audit:** admin code creation/disable writes `AdminAuditLog` (existing table + pattern).

### How this filters for intent — and its honest limits

The owner's goal is to select for genuine need. Be realistic about what a **single universal
code** does and does not achieve:

**What genuinely filters (keep these):**
1. **Removing frictionless auto-free** — the biggest lever. Pure tire-kickers who'd burn 3 free without engaging now self-select out.
2. **Sign-up + email verification** (already required) — a real, verified identity per trial.
3. **The deliberate act of entering a code** — small but real intent signal.
4. **One redemption per account** + optional **shorter credit TTL** (e.g. 30 days) to select for users who act now, not someday.
5. **Controlled distribution** — show the code only in targeted channels (launch email, a specific campaign/partner, a "request access" reply), not splashed on the hero.

**What does *not* filter (be honest):**
- A universal code **will leak** (forums, screenshots). Secrecy is not the moat.
- Free email sign-ups mean a determined user can make N accounts for 3N rewrites. Email
  verification + `MaxRedemptionsGlobal` + `SignupIpHash` *bound* this; they don't eliminate it.

**The real upgrade path (architecture already supports it):** issue **per-user unique codes**
(one row per code, `MaxRedemptionsGlobal=1`, distributed individually). The schema, redeem
flow, and analytics work unchanged — only the code-generation/distribution differs. Recommend
launching universal, watching `PromoCodeRedemption` + activation rate, and switching to unique
codes if abuse or low-intent redemption shows up in the data.

---

## Rollout Plan

Ordered, each step independently reviewable; substantive code delegated to Codex.

1. **Schema + service + endpoints (no UI cutover yet).** Add entities, migration, `PromoService`, `PromoHttpFunctions`, admin code-management, `/api/me` `promo` block. Free baseline still `3`. Ship behind no user-visible change. Merge → migration applies to Azure SQL.
2. **Free baseline cutover (`3 → 0`) + consistency migration.**
   - Change `AccountService.GetUsagePlan` free `QuotaLimit` `3 → 0` (consider a `FREE_BASELINE_REWRITES` config/const for reversibility).
   - **Consistency:** `GetOrCreateAccountSummaryAsync` computes `periodRemaining` from the
     *constant* (`usagePlan.QuotaLimit`), while `QuotaService.ReserveAsync` enforces against the
     *persisted* `UsagePeriod.QuotaLimit`. For any pre-existing `free:lifetime` rows these can
     now disagree. Mitigation: a one-time data migration `UPDATE UsagePeriods SET QuotaLimit=0,
     UpdatedAt=… WHERE PeriodKey='free:lifetime'`, **and** confirm `ReserveAsync` reconciles an
     existing row's `QuotaLimit` to the plan value (if it does not, the migration is the source
     of truth). Implementation checkpoint: verify in code which value `ReserveAsync` trusts.
   - Decision: existing users' grandfathered free — since the live site has ~no real users
     (sprint note 2026-05-21), zeroing all `free:lifetime` rows is safe and simplest. If real
     users exist, optionally grant them a one-off PROMO credit to avoid a bad surprise.
3. **UI.** Add `redeem-code-card`, branch `/app` empty state on `promo.hasRedeemed`, update copy + contract tests. Verify with Playwright/`ui-browser-testing` (desktop+mobile, console/network).
4. **Seed the launch code** via admin endpoint with owner-provided value/expiry/credits.
5. **Verify end-to-end on Worker preview** before prod (per `AGENTS.md` cutover gates): redeem happy path, expired/already-redeemed/invalid, rewrite consumes PROMO credit, paywall after exhaustion. Banned-term grep clean.
6. **Deploy.** Push to `main` → CI build-test → `cf:deploy` + `dotnet ef database update`. Rollback = `wrangler rollback` (frontend) + the code is disablable via `IsActive=false` without a deploy.

> **Deployment guardrails (unchanged):** Stripe stays in its configured mode; do **not** touch
> `LAUNCH_CONFIRMED`, `STRIPE_*`, price IDs, DNS, or the Pages custom domain. No real charges.
> Keep Worker `vars` and Azure Functions app settings in sync if any new config is added.

---

## Verification Plan

### Backend (`dotnet-backend-testing` lens — xUnit + EF SQLite + WebApplicationFactory)
- `PromoService`: happy redeem grants exactly `CreditsGranted` credits with correct `ExpiresAt`.
- Idempotency: second redeem → `AlreadyRedeemed`, balance unchanged, one credit row.
- Concurrency: two parallel redeems (retrying execution strategy + unique index) → one grant.
- Validity gates: not-found/inactive/not-yet-valid → `invalid_code`; past `ValidUntil` → `expired`; `ValidUntil` boundary inclusive.
- Global cap: cap=N, N+1 redeems → N applied, overflow `code_exhausted`.
- Consumption: after redeem, `QuotaService.ReserveAsync` consumes the PROMO credit; `/api/me` `remaining` goes 3→2→1→0 then paywall.
- Free baseline: brand-new user → `remaining=0`, `exhausted=true`, `promo.hasRedeemed=false`.
- Account-erase extended to `PromoCodeRedemption` (no orphan PII).
- Admin: create/list/disable/stats endpoints; audit log written.

### Frontend (`ui-browser-testing` lens — Playwright)
- New user `/app` shows **redeem** card (not buy-paywall); after redeem shows workspace with "3 trial rewrites · expires in N days".
- Invalid/expired/already-redeemed show friendly inline errors.
- After exhausting trial → buy-paywall appears.
- Desktop + mobile screenshots: no overflow/clipping, no console/network errors.
- Copy contract tests (`pricing-auth-visual-system.test.ts`, `workspace-copy.test.ts`) updated to new strings and **passing** (they gate `cf:deploy`).

### Gates before deploy
- `npm run test` green (copy contracts), `dotnet test` green, banned-term grep clean, Worker-preview smoke of the full redeem→rewrite→paywall loop.

---

## Copy & Interaction Design (scope 1a)

Concrete strings are owner-tunable; below is the recommended direction. **Every string change
must update the contract tests** (see [grounding table](#grounding--what-the-live-system-actually-is-verified-2026-06-02)).

### Homepage / landing (de-emphasize "free", emphasize "redeem")
- **Hero stat** (`components/landing/hero.tsx:6`): `"3 free"` → `"3 rewrites"` with sub `"when you redeem a trial code"` (or drop the stat). Eyebrow drop "Start free".
- **Closing CTA** (`components/landing/closing-cta.tsx:28`): `"3 free rewrites · No card required"` → `"Have a trial code? Redeem it for 3 rewrites · No card required"`.
- **Landing pricing block** (`components/landing/pricing-v2.tsx`) & **pricing page** (`app/pricing/page.tsx`): replace "Everyone starts with 3 lifetime rewrites" with "Redeem a trial code for 3 rewrites, then buy packs…". Free-tier card becomes a **"Trial code"** card: "Enter a code to unlock 3 rewrites" with a CTA to `/app` (sign-in first).
- **Footer** (`components/site-footer.tsx:68`) & **developers page** (`app/developers/page.tsx:245`): swap "3 free rewrites" → "Redeem a trial code".
- **Auth sign-up highlights** (`components/auth/google-oauth-card.tsx:45`): "Start with three free rewrites" → "Redeem a trial code for three rewrites".

### `/app` workspace (the redeem entry — primary surface)
- **New empty state** when `scope=="free" && remaining==0 && !promo.hasRedeemed`: a
  `redeem-code-card` with a single text input + "Redeem" button:
  - Title: "Unlock your trial" · Body: "Enter your trial code to get 3 rewrites." · input placeholder "Trial code" · button "Redeem".
  - Inline states: success ("3 rewrites unlocked — they expire in N days."), `invalid_code` ("That code isn't valid."), `code_expired` ("This code has expired."), `already_redeemed` ("You've already redeemed a trial code.").
- **Quota label** (`app/app/page.tsx`): for a PROMO source show "N of 3 trial rewrites remaining" (map `Source=="PROMO"` → friendly label "Trial rewrites"; today `AccountUsageSource.Label` is just the raw source string).
- **Exhausted** when `promo.hasRedeemed && remaining==0` → existing `paywall-card` (buy packs) — unchanged.
- Optionally surface a small "Have a code?" link in the paywall so users with a code who hit a zero state can still redeem.

### Interaction principles
- Redeem requires sign-in; signed-out users clicking a redeem CTA route through `/sign-in?redirectTo=/app`.
- One input, instant inline validation, no page reload (call proxy, re-fetch `/api/me`).
- Do **not** print the universal code anywhere it would defeat the intent filter (no hero banner). Distribution is the owner's channel choice.

---

## Open Questions (owner inputs — do not invent these)

1. **Code value** — the actual universal string (e.g. `WELCOME2026`, `REPLY3`, …)? Recommend 6–12 uppercase alphanumerics, easy to type.
2. **Expiry instant** — confirm "end of August 2026". Recommended default: `2026-08-31T23:59:59+12:00` (NZST) = `2026-08-31T11:59:59Z`. Confirm timezone.
3. **Granted-credit TTL** — how long do the 3 trial rewrites last *after* redemption? Default 90 days (matches purchases/admin grants); a shorter 30 days reinforces "act now / real need". Pick `GrantTtlDays`.
4. **Global cap** — any limit on total redemptions (`MaxRedemptionsGlobal`)? Default null (unlimited) for a launch universal code; a cap bounds abuse exposure.
5. **Existing users** — if any real users already have free quota, grandfather them or zero them? (Likely moot — ~no live users.)
6. **Distribution channel** — where will the code be shown? (Decides how loud the homepage copy is.) 
7. **Universal vs. per-user** — confirm launching with one universal code (yes per current plan); revisit if redemption data shows abuse.

---

## Implementation Checkpoints (for the Codex brief)

1. Add `PromoCode` + `PromoCodeRedemption` entities; register DbSets + `OnModelCreating` config (unique `Code`; unique `(PromoCodeId,UserId)`; string enums; concurrency tokens); create EF migration `_AddPromoCodes`. **Do not** edit other entities except adding nav collection.
2. `PromoService.RedeemAsync` + `GetStatusAsync` using `ExecuteInTransactionAsync` (Serializable), mirroring `AdminService.GrantCreditsAsync` grant shape and catching the unique-index race.
3. `PromoHttpFunctions` (`POST promo/redeem`, optional `GET promo/status`); admin code-management endpoints + audit logs.
4. Extend `AccountService.GetOrCreateAccountSummaryAsync` with a `promo` block + friendly source labels; **separately** flip free `QuotaLimit` 3→0 (config-backed) + consistency data migration; verify which `QuotaLimit` `ReserveAsync` trusts.
5. Next.js proxies `app/api/promo/redeem/route.ts` (+ status), same-origin enforced.
6. `components/app/redeem-code-card.tsx` + `/app` empty-state branching + copy changes across hero/pricing/footer/auth/developers; update `tests/unit/pricing-auth-visual-system.test.ts` + `tests/unit/workspace-copy.test.ts`.
7. Extend `AccountService.DeleteAccountAsync` to cover `PromoCodeRedemption`.
8. Tests per Verification Plan; banned-term grep; Worker-preview smoke; then deploy.

**Codex constraints to echo in every brief:** banned terms `humanizer|bypass|undetect|detector|evade`; no secrets in source; validate env at runtime; no `migrate reset`/force-reset/table drops; do not touch `LAUNCH_CONFIRMED`/Stripe price/webhook secrets/DNS; no real charges.
