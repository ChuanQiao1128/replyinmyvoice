# Promo-Code Trial Specification (v2 — feasibility-evaluated & risk-hardened)

> Replace the automatic "3 free rewrites on sign-up" with a **redeemable promo code**. Launch
> with one universal code (`ReplyAsHuman2026` is the owner's example/test value — admin-settable),
> but build a commercial-grade, admin-managed, abuse-resistant, analytics-ready system underneath.

- **Status:** Design only — no source code written yet (owner instruction: evaluate & design first).
- **Branch:** `feat/promo-code-trial`
- **Author:** Claude Code (supervisor) — `system-spec-synthesis` (+ `state-machine-modeling`, `data-module-review`, `resilience-test-generation` lenses).
- **Date:** 2026-06-02 · **v2** adds: feasibility/gap assessment against the live code, admin console design, full threat model, user stories, and a risk register.
- **Implementation:** delegated to Codex workers after owner sign-off on the Open Questions.

---

## 0. Feasibility & Gap Assessment — "can my backend already do this?"

Answer: **the data + grant + consumption machinery is already there; the promo layer, the
admin self-service UI, and durable IP-based abuse defense are the real net-new work.** Verified
against live C#/Azure source on 2026-06-02.

| Owner requirement | Exists today? | Gap / what's missing | Effort |
|---|---|---|---|
| 1a Credits count + validity per code | ⚠️ partial | `RewriteCredit` grants exist (admin/purchase) but no per-code *config*; need `PromoCode` table | M |
| 1b Per-person redemption cap | ❌ | New: unique `(codeId,userId)` index + `MaxRedemptionsPerUser` | S |
| 1c Max total redemptions ("1001st fails") | ❌ | New: `MaxRedemptionsGlobal` + **atomic** counter guard | S |
| 1d "一人一码" (one redemption per identity) | ❌ | New: per-user uniqueness + anti-multi-account defense | M |
| 2 Admin creates / disables codes | ⚠️ **API-only** | Admin auth (`ADMIN_EMAILS` allowlist) + audit log + grant pattern all exist — **but there is NO admin web UI anywhere in `app/`**. Today admins call HTTP endpoints directly (curl/Postman). | endpoints S; **UI M** |
| 2 Disable → immediate | ✅ (pattern) | `IsActive=false` checked at redeem time; no deploy needed | S |
| 3a Credit TTL 90 days | ✅ | `AdminCreditExpiryDays = 90` precedent; per-code `GrantTtlDays` | S |
| 3b Two clocks: code window vs 90-day credit | ✅ design | Code `ValidUntil` (redeem window) is independent of granted `RewriteCredit.ExpiresAt = redeemedAt + 90d`. Matches owner's example exactly. | — |
| 3c Cap optional (capped *or* uncapped) | ❌ | `MaxRedemptionsGlobal` **nullable** (null = unlimited) | S |
| 4 Trace IP / anti multi-account | ⚠️ **weak** | `cf-connecting-ip` is readable at the **Worker/proxy** (`lib/auth-rate-limit.ts` already does). BUT: (a) that limiter is **in-memory per-isolate** → ephemeral, not global; (b) the **C# backend sits behind the Worker and does not see the real client IP** unless the proxy forwards it; (c) a forwarded `X-Forwarded-For` is **spoofable** by anyone hitting the Functions URL directly. Need durable, trustworthy IP capture + DB-backed velocity. | M–L |

**Bottom line:** No blocker. The heaviest items are the **admin console UI** (none exists) and
**doing IP abuse-defense properly** (the existing rate limiter is not sufficient on its own).
Everything else is small, isolated, and rides on proven patterns.

---

## 1. TL;DR (the load-bearing idea)

The live backend already grants & consumes "rewrite credits": `RewriteCredit` rows (Source =
`PURCHASE` / `ADMIN`) are consumed by `QuotaService` once a user's period quota is used up, and
`/api/me` already sums them into `remaining` with a per-source "expires in N days" breakdown.

So:
1. **Stop auto-granting.** Free baseline quota `3 → 0` (one constant in `AccountService.GetUsagePlan`) + a one-time data migration so display and enforcement agree.
2. **Add a promo layer.** Two tables (`PromoCode` config + `PromoCodeRedemption` ledger), one redeem service, one user endpoint, admin code-management endpoints + a small admin UI, a `/app` redeem card, and copy changes.
3. **Redemption = grant a credit.** A valid redemption inserts `RewriteCredit{Source="PROMO", AmountGranted=3, ExpiresAt=redeemedAt+90d}` — the exact shape `AdminService.GrantCreditsAsync` already creates. **Consumption, paywall, 402, quota-race, Stripe — all unchanged.**

Risk is concentrated in three places, each addressed below: (a) the free-baseline=0 cutover, (b) redemption idempotency/race + global-cap race, (c) multi-account abuse defense.

---

## 2. Requirements (consolidated)

### 2.1 Per-code configuration (admin-set)
- **Credits granted** (e.g. 3) and **granted-credit TTL** (default **90 days** from redemption).
- **Redemption window**: `ValidFrom`..`ValidUntil` (e.g. until end of Aug 2026, **NZ time**). After `ValidUntil`, redemption fails with a clear "expired" message.
- **Per-person cap** (`MaxRedemptionsPerUser`, default **1**).
- **Global cap** (`MaxRedemptionsGlobal`, **nullable** = optional): e.g. 1000 → the **1001st** distinct redemption is rejected with "this code has reached its limit"; the code is effectively spent.
- **On/off** kill switch (`IsActive`): disabling takes effect **immediately** (checked per redeem, no deploy).

### 2.2 "一人一码" — interpretation (please confirm)
Read as **one redemption per identity** (each account may redeem a given code at most once),
enforced by a unique `(PromoCodeId, UserId)` index and hardened by anti-multi-account defenses
(§7). This is **not** literal per-user-unique code *strings* — though the same schema supports
that later (issue many single-use codes, `MaxRedemptionsGlobal=1` each) if you want true 1:1
issuance. **If you meant unique strings per user, say so — it changes distribution, not the core schema.**

### 2.3 Two independent clocks (owner's example, locked in)
- **Code redemption window**: until `2026-08-31` 23:59:59 NZ. Stops *new* redemptions after that.
- **Granted-credit lifetime**: a user who redeems on, say, Aug 20 gets 3 rewrites valid **90 days from Aug 20** (≈ Nov 18). Expiry of the *code* does **not** retroactively kill already-granted credits.

### 2.4 Admin (owner) can self-serve
Create codes, list them with live redemption counts, disable them, and view per-code
effectiveness stats — from an authenticated admin surface (see §6).

### 2.5 Goal: select for genuine intent
The filter is the friction of obtaining + entering a code, plus sign-up + email verification,
plus one-redemption-per-identity. Honest limits and the upgrade path are in §7.4.

---

## 3. Current System (grounded; verified 2026-06-02)

Production = **C#/.NET Azure Functions + Azure SQL (EF Core)**, behind **Cloudflare Worker
(OpenNext Next.js)**. The TS `lib/` rewrite pipeline and `lib/generated/prisma/**` are **dead
Slice-7 code** (the dead Prisma schema's `User.referralCode` is *not* the live model).

| Concern | Live location & fact |
|---|---|
| Free "3" constant | `…/Infrastructure/Services/AccountService.cs` → `GetUsagePlan()` returns `("free","free:lifetime",3)`. It's a **`UsagePeriod.QuotaLimit`, not a credit.** |
| `/api/me` builder | `AccountService.GetOrCreateAccountSummaryAsync()` → `remaining = periodRemaining + creditRemaining`; emits `Sources[]` per credit with `ExpiresInDays`; `Exhausted = remaining<=0`. |
| Grant pattern to mirror | `AdminService.GrantCreditsAsync()` → inserts `RewriteCredit{Source="ADMIN", ExpiresAt=now+90d}` + `AdminAuditLog`, one `SaveChanges`. |
| Consumption / paywall | `QuotaService.ReserveAsync()` + `FindUsableCreditAsync()` (period quota first, then usable credits, **soonest-expiring first**). **No change needed.** |
| Serializable tx helper | `StripeEventService.ExecuteInTransactionAsync()` (execution strategy + `IsolationLevel.Serializable`). |
| Admin auth | `…/Functions/Auth/AdminAccess.cs` → `RequireAdminAsync` resolves the Entra user (`FunctionAuthResolver`), allows if `oid` **or** `email` ∈ `ADMIN_EMAILS` (comma-sep, case-insensitive). |
| Admin endpoints | `…/Functions/Functions/AdminHttpFunctions.cs`: `admin/ping|users|users/{id}|stats|users/{id}/credits|.../suspension|.../refund`. **All API-only — no admin page in `app/`.** |
| Rate-limit precedent | `lib/auth-rate-limit.ts`: per-email + per-IP buckets, `clientIpFromRequest()` reads `x-forwarded-for` then `cf-connecting-ip`. **In-memory per-isolate → ephemeral, best-effort only.** |
| IP plumbing | `cf-connecting-ip` visible at the Worker. `Referral.SignupIpHash` (max 128) exists but **unused**; no signup-IP capture wired. |
| User identity | Entra `oid` → `AppUser.ExternalAuthUserId` (unique); PK `Guid Id`. |
| Migrations | `…/Infrastructure/Migrations/` (`YYYYMMDDhhmmss_Name`), auto-applied on merge to `main` via `.github/workflows/dotnet-azure.yml` (`dotnet ef database update` on live Azure SQL). |
| Copy contract tests | `tests/unit/pricing-auth-visual-system.test.ts`, `tests/unit/workspace-copy.test.ts` (gate `cf:deploy`). |

No existing promo/coupon/redeem code. C# `Referral` is vestigial (declared in `AppDbContext`, used by no service).

---

## 4. Proposed Architecture

```
sign up + verify email (Entra) ──▶ AppUser{Inactive}
        │
        ▼
GET /api/me ── GetUsagePlan()=("free","free:lifetime",0)  ← change 3→0
        │      remaining = 0 + activeCredits(0); promo.hasRedeemed=false
        ▼
/app shows "Redeem your code" card   ← NEW empty state (NOT buy-paywall)
        │
POST /api/promo/redeem {code}  (proxy forwards trusted client IP)
        │  PromoService.RedeemAsync(): normalize → validate window/active/cap
        │   → IP velocity check (DB) → in Serializable tx:
        │       atomic conditional ++RedemptionCount (cap guard)
        │       insert RewriteCredit{Source="PROMO",Amount=3,ExpiresAt=now+90d}
        │       insert PromoCodeRedemption{...unique(codeId,userId)..., RedeemIpHash}
        ▼
GET /api/me ── remaining = 3; Sources=[…,{Source="PROMO",ExpiresInDays≈90}]
        ▼
/app workspace (3 trial rewrites) ── POST /api/rewrite consumes PROMO credit (UNCHANGED path)
        ▼
PROMO credit exhausted ── /app buy-paywall (promo.hasRedeemed=true)
```

Why: consumption/paywall/quota-race/Stripe untouched; `/api/me` already shows per-source
expiry; redemption reuses the proven grant + serializable-tx patterns.

---

## 5. Data Model (`data-module-review` lens)

EF conventions: `Guid Id`, `Guid RowVersion` concurrency token, `DateTimeOffset`, string enums
via `.HasConversion<string>()`+`HasMaxLength`, indexes in `OnModelCreating`. Migration
`_AddPromoCodes`; **no** reset/force-reset/drops (AGENTS.md).

### `PromoCode` (config — the knob)
```csharp
public sealed class PromoCode {
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }            // normalized: trim+UPPER+strip space/dash. UNIQUE.
    public string? Description { get; set; }             // internal label
    public PromoCodeKind Kind { get; set; } = PromoCodeKind.TrialCredits;
    public int CreditsGranted { get; set; }              // e.g. 3
    public int GrantTtlDays { get; set; } = 90;          // credit lifetime after redemption
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }       // e.g. 2026-08-31T23:59:59+12:00
    public int? MaxRedemptionsGlobal { get; set; }       // null = uncapped
    public int MaxRedemptionsPerUser { get; set; } = 1;
    public int RedemptionCount { get; set; }             // denormalized; cap guard + fast stats
    public bool IsActive { get; set; } = true;           // immediate kill switch
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();
    public ICollection<PromoCodeRedemption> Redemptions { get; } = new List<PromoCodeRedemption>();
}
public enum PromoCodeKind { TrialCredits }   // extensible; only this is implemented now
```
Config: `HasIndex(Code).IsUnique()`; `Code` max 40, `Description` max 200, `Kind` conv-string max 40; `RowVersion` concurrency token.

### `PromoCodeRedemption` (ledger — analytics source of truth)
```csharp
public sealed class PromoCodeRedemption {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PromoCodeId { get; set; }  public PromoCode? PromoCode { get; set; }
    public Guid UserId { get; set; }       public AppUser? User { get; set; }
    public Guid RewriteCreditId { get; set; }            // the credit this redemption created
    public int CreditsGranted { get; set; }
    public string CodeSnapshot { get; set; } = "";       // normalized code text as redeemed (audit)
    public string? RedeemIpHash { get; set; }            // salted SHA-256(IP) — never raw IP
    public PromoCodeRedemptionStatus Status { get; set; } = PromoCodeRedemptionStatus.Applied;
    public DateTimeOffset RedeemedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReversedAt { get; set; }      // admin clawback
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
public enum PromoCodeRedemptionStatus { Applied, Reversed }
```
Config / indexes:
- **`HasIndex(new {PromoCodeId, UserId}).IsUnique()`** — enforces one-per-user **and** is the redemption race backstop (mirrors `RewriteAttempt (UserId,IdempotencyKey)`).
- `HasIndex(PromoCodeId)`, `HasIndex(UserId)`, `HasIndex(RedeemIpHash)` (velocity queries), `HasIndex(RedeemedAt)`.
- FK `PromoCodeId→PromoCode` `OnDelete(Restrict)`; `UserId→AppUser` `OnDelete(Cascade)`; `RewriteCreditId` a plain indexed `Guid` (no cascade FK — avoids SQL Server multiple-cascade-path error, same reason as `Referral.RefereeId`).
- `RedeemIpHash` max 128, nullable.

### Global-cap correctness (the "1001st" guarantee)
Do **not** read-then-write the counter — under load it would over-issue. Use an **atomic
conditional increment** as the cap guard (race-proof even at READ COMMITTED):
```sql
UPDATE PromoCodes
SET RedemptionCount = RedemptionCount + 1, UpdatedAt=@now, RowVersion=@new
WHERE Id=@id AND IsActive=1 AND @now BETWEEN ValidFrom AND ValidUntil
  AND (MaxRedemptionsGlobal IS NULL OR RedemptionCount < MaxRedemptionsGlobal);
-- rows affected == 0 ⇒ disabled / outside window / cap reached → re-read to pick the message
```
(EF: `ExecuteUpdateAsync` with that predicate, inside the same tx as the credit + redemption inserts.)

### Account deletion
Extend `AccountService.DeleteAccountAsync` (which already anonymizes attempts/periods/reservations/credits)
to also handle `PromoCodeRedemption` for the erased user: **null `RedeemIpHash`**, keep the row
(so promo totals stay accurate). Do not resurrect free quota on erase.

### Analytics (answers "how many people used code X, and did they actually use it?")
```sql
-- distinct redeemers + window
SELECT COUNT(*) AS redemptions, MIN(RedeemedAt) first, MAX(RedeemedAt) last
FROM PromoCodeRedemptions WHERE PromoCodeId=@id AND Status='Applied';
-- daily curve
SELECT CAST(RedeemedAt AS date) day, COUNT(*) n FROM PromoCodeRedemptions
WHERE PromoCodeId=@id AND Status='Applied' GROUP BY CAST(RedeemedAt AS date);
-- ACTIVATION: redeemers who used ≥1 of the granted rewrites (the real "genuine need" signal)
SELECT SUM(CASE WHEN rc.AmountConsumed>0 THEN 1 ELSE 0 END) activated, COUNT(*) total
FROM PromoCodeRedemptions r JOIN RewriteCredits rc ON rc.Id=r.RewriteCreditId
WHERE r.PromoCodeId=@id AND r.Status='Applied';
-- abuse signal: same IP hash redeeming many times across accounts
SELECT RedeemIpHash, COUNT(*) n FROM PromoCodeRedemptions
WHERE PromoCodeId=@id AND RedeemIpHash IS NOT NULL GROUP BY RedeemIpHash HAVING COUNT(*)>3;
```

---

## 6. Admin Capabilities & Console (net-new — owner self-service)

**Auth (reuse):** all promo-admin endpoints gated by `AdminAccess.RequireAdminAsync` (owner's
Entra email in `ADMIN_EMAILS`). Every mutating action writes `AdminAuditLog` (existing pattern).

**Backend endpoints** (extend `AdminService` + new `PromoAdminHttpFunctions` or fold into `AdminHttpFunctions`):
| Method/Route | Purpose |
|---|---|
| `POST /api/admin/promo-codes` | Create: `{code, description, creditsGranted, grantTtlDays, validFrom, validUntil, maxRedemptionsGlobal?, maxRedemptionsPerUser?}` → validates (unique code, sane numbers, `validUntil>validFrom`) → audit. |
| `GET /api/admin/promo-codes` | List with `redemptionCount`, derived status (active/pending/expired/exhausted/disabled), config. |
| `GET /api/admin/promo-codes/{id}` | Detail + stats (redemptions, distinct users, daily curve, activation rate, top IP-hash buckets). |
| `PATCH /api/admin/promo-codes/{id}` | Edit `validUntil`, caps, description (audited). Lowering a cap below current count blocks new redemptions but keeps existing. |
| `POST /api/admin/promo-codes/{id}/disable` (and `/enable`) | Flip `IsActive` — **immediate**. |

**Admin UI decision (please pick — see Open Questions):** there is **no admin web page today**.
- **Option A (recommended): minimal admin UI** — a new authenticated `app/admin/promo-codes` page (table + "New code" form + disable toggle + per-code stats). Matches "在后台操作". Effort M. Reuses the same admin auth (server-side fetch with the admin's session/bearer; page returns 403 for non-admins). It can also surface the *other* existing admin APIs later (users/stats) since none are currently UI-exposed.
- **Option B: API-only (status quo)** — owner runs documented `curl`/HTTP calls (or a tiny local script) to create/disable codes. Zero UI effort; least friendly. Reasonable as a *stopgap* to launch the first code, then build the UI.

> Recommendation: ship endpoints first (Option B unblocks creating `ReplyAsHuman2026` immediately), build the small UI (Option A) in the same wave.

---

## 7. Security — Threat Model & Defense-in-Depth (the "real commercial risk" the owner flagged)

### 7.1 Trust boundary & IP capture (the architecture that makes "追查 IP" actually work)
```
Client ──(cf-connecting-ip set by Cloudflare)──▶ Worker/Next.js proxy ──▶ Azure Functions (C#)
                                                  ▲ sees real IP        ▲ sees ONLY what proxy forwards
```
- **Capture point:** the Worker/proxy (`app/api/promo/redeem/route.ts`) reads `cf-connecting-ip` (trustworthy — Cloudflare sets it) and forwards it to C# as a header.
- **Spoofing caveat (must address):** the Functions endpoint is internet-reachable and `AuthorizationLevel.Anonymous` (JWT-checked, but not IP-restricted). A direct caller could forge `X-Forwarded-For`. **Therefore C# must not trust an arbitrary client XFF.** Choose one:
  1. Forward the client IP in a header guarded by a **proxy shared secret** (`X-RIMV-Proxy-Secret`, a new Worker→Functions secret); C# accepts the IP header only when the secret matches. *(Recommended; verify whether such a shared secret already exists between proxy and Functions — today the proxy forwards the user bearer only.)*
  2. Restrict Functions ingress to Cloudflare IP ranges / use Azure access restrictions, then trust the forwarded header.
- **Storage:** C# hashes the IP (salted SHA-256, salt from config — never raw IP, never logged) → `PromoCodeRedemption.RedeemIpHash`. (Optionally also stamp signup IP on `AppUser` via a new nullable column — bigger change to the Entra signup path; mark optional.)

### 7.2 Velocity / anti-multi-account (durable, not the in-memory limiter)
- The existing `lib/auth-rate-limit.ts` is **per-isolate in-memory** → fine as a cheap first
  layer on the proxy, **insufficient alone**. Authoritative defense is **DB-backed** in the C#
  redeem path:
  - Reject/flag if `COUNT(PromoCodeRedemption WHERE RedeemIpHash=@h AND RedeemedAt > now-24h) ≥ N` (e.g. N=3). Tune N; log flagged attempts for the owner to review.
- **Global cap** (§5) bounds *total* exposure regardless of who redeems.
- **Per-user unique index** stops the same account double-dipping.

### 7.3 Threats & mitigations
| # | Threat | Mitigation | Residual |
|---|---|---|---|
| T1 | One human, many accounts (disposable emails) → 3×N | Email verification (already) + IP velocity (§7.2) + global cap + activation analytics to spot farming | Determined attacker across IPs/emails still gets some free use; bounded by global cap |
| T2 | Brute-force code guessing | Generic `invalid_code` (no enumeration) + per-IP/per-user rate limit on redeem | Low (single known universal code today) |
| T3 | Universal code leaks publicly | Expected; bounded by **global cap + `ValidUntil` + instant disable**; upgrade to per-user codes if abused | Accepted by design |
| T4 | Bots auto signup+redeem | IP velocity + optional **Cloudflare Turnstile/BotID** on signup &/or redeem | Medium without CAPTCHA; low with |
| T5 | Replay / concurrent double-grant | Idempotent redeem + unique `(codeId,userId)` + atomic cap increment + Serializable tx | Negligible |
| T6 | Delete account → re-signup to re-farm | `RedeemIpHash` retained (anonymized PII) so IP velocity still catches bursts | Low–medium |
| T7 | IP header spoofing to dodge velocity | Proxy shared-secret-guarded IP header / ingress restriction (§7.1) | Low if implemented; **must implement** |
| T8 | Admin endpoint abuse | `ADMIN_EMAILS` allowlist + audit log on every mutation | Low |

### 7.4 How this filters for intent — and honest limits
**Genuinely filters:** removing frictionless auto-free (biggest lever) · sign-up + email verify ·
the deliberate act of entering a code · one-redemption-per-identity · (optional) shorter credit
TTL to favor "act now" · controlled distribution (don't splash the code on the hero).
**Does *not* filter:** a universal code **will leak**; free email signups enable multi-account
farming (bounded, not eliminated). **Real upgrade path (schema already supports):** issue
**per-user unique codes** (`MaxRedemptionsGlobal=1` each) — switch if redemption/activation data
shows abuse. Recommend launching universal, watching the data, escalating only if needed.

### 7.5 Hygiene
No banned terms (`humanizer|bypass|undetect|detector|evade`) in code identifiers/copy (CI grep
scans `app components public lib`). The code *value* `ReplyAsHuman2026` does **not** trip the grep
(no banned substring) — usable as the test value. *(Brand note: AGENTS.md positions the product
as "reply in my voice", not "guaranteed human"; a code like `ReplyInMyVoice2026`/`Welcome2026`
sits more cleanly with positioning. Your call — it's admin-set.)* No secrets in source; the code
value lives in the DB; never log code values or raw IPs.

---

## 8. State Machines (`state-machine-modeling` lens)

### `PromoCode` (derived states — computed from columns, not a stored enum)
```
[Pending] now<ValidFrom ──▶ [Active] ──┬─ now>ValidUntil ─▶ [Expired]   (terminal for redeem)
                                        ├─ RedemptionCount≥MaxGlobal ─▶ [Exhausted]
                                        └─ IsActive=false ─▶ [Disabled] ⇄ (admin enable) ─▶ [Active]
```
Redeemable iff `IsActive ∧ ValidFrom≤now≤ValidUntil ∧ (MaxGlobal==null ∨ RedemptionCount<MaxGlobal)`.
Invariant: `RedemptionCount == COUNT(redemptions WHERE Status=Applied)`.

### `PromoCodeRedemption` (per user×code)
```
(none) ──success──▶ [Applied] ──admin clawback──▶ [Reversed]
  ▲ second Applied for same (code,user) → blocked by unique index (illegal)
```
Invariants: ≤1 row per `(codeId,userId)`; every `Applied` references a real `RewriteCredit` created in the same tx. `Reversed` optionally zeroes the linked credit (mirror erase path; column present, full build out of scope now).

---

## 9. Redeem Algorithm (idempotent, race-safe, IP-aware)

```
RedeemAsync(extUserId, email, rawCode, clientIpTrusted?, now):
  code = Normalize(rawCode)                          // trim; UPPER; strip spaces/dashes
  if !FormatValid(code): return InvalidCode
  user = AccountService.GetOrCreateUser(...)
  ipHash = clientIpTrusted is null ? null : HashIp(clientIpTrusted)   // salted SHA-256
  if ipHash != null && RecentRedemptions(ipHash, now-24h) >= N: return IpVelocityBlocked   // flag+log
  return ExecuteInTransactionAsync(Serializable):
    pc = db.PromoCodes.SingleOrDefault(c => c.Code == code)
    if pc == null || !pc.IsActive:        return InvalidCode          // also covers unknown
    if now <  pc.ValidFrom:               return InvalidCode          // not-yet-valid → generic
    if now >  pc.ValidUntil:              return Expired
    if db.PromoCodeRedemptions.Any(r => r.PromoCodeId==pc.Id && r.UserId==user.Id):
        return AlreadyRedeemed                                        // idempotent, no new grant
    affected = ExecuteUpdate(++RedemptionCount WHERE cap predicate)   // §5 atomic cap guard
    if affected == 0:                     return CapReachedOrInactive // re-read → Exhausted/Expired/Disabled
    credit = RewriteCredit{ UserId, Source="PROMO", AmountGranted=pc.CreditsGranted,
                            AmountConsumed=0, GrantedAt=now, ExpiresAt=now.AddDays(pc.GrantTtlDays) }
    db.RewriteCredits.Add(credit)
    db.PromoCodeRedemptions.Add(new {... RewriteCreditId=credit.Id, CodeSnapshot=code,
                                      RedeemIpHash=ipHash, Status=Applied, RedeemedAt=now})
    try: SaveChanges(); commit
    catch DbUpdateException when unique(codeId,userId) violated:
        rollback; return AlreadyRedeemed                             // lost concurrent race
  return Success(credit.AmountGranted, credit.ExpiresAt)
```

### Resilience matrix (drives tests)
| Scenario | Expected |
|---|---|
| Same user, two concurrent redeems | exactly one Applied + one PROMO credit; other → AlreadyRedeemed |
| Replay (double tap) | 409 already_redeemed; balance unchanged |
| `ValidUntil` boundary | `now ≤ ValidUntil` ok; `now > ValidUntil` → expired (single `now` per request) |
| Global cap N, N+5 concurrent | **exactly N** Applied (atomic ++); overflow → code_exhausted |
| 1001st redemption (cap 1000) | rejected, message "code has reached its limit" |
| Grant then rewrite | `FindUsableCreditAsync` consumes PROMO credit (soonest-expiring first) — unchanged |
| Engine/Sapling fails on trial rewrite | existing no-charge releases reservation; PROMO credit not consumed |
| Same IP, many accounts in 24h | ≥N → IpVelocityBlocked + logged for owner review |
| Spoofed XFF direct to Functions | ignored unless proxy-secret valid (§7.1) |

---

## 10. User Stories (with acceptance)

### Admin (owner)
- **A1 Create code:** As admin I create `ReplyAsHuman2026` with credits=3, TTL=90d, validUntil=Aug 31 NZ, perUser=1, global=1000 (or none). *Accept:* code appears Active; audit logged.
- **A2 Disable code:** I toggle a code off. *Accept:* next redemption → invalid immediately, no deploy.
- **A3 Measure effectiveness:** I view a code's stats. *Accept:* see total redemptions, distinct users, daily curve, **activation rate** (used ≥1), top IP-hash buckets.
- **A4 Edit code:** I extend `validUntil` or change the cap. *Accept:* audited; lowering cap below count blocks new but keeps existing.
- **A5 Duplicate code:** I try to create an existing code string. *Accept:* rejected (unique).
- **A6 List/triage:** I see all codes with derived status. *Accept:* Active/Pending/Expired/Exhausted/Disabled shown.

### End user
- **U1 New user, no code:** signed-in, 0 rewrites → sees **Redeem** card (not buy-paywall).
- **U2 Redeem valid:** enters code → +3, "expires in ~90 days", can rewrite.
- **U3 Redeem twice:** → "already redeemed", no double grant.
- **U4 Expired code:** after Aug 31 → "this code has expired".
- **U5 Invalid/typo:** → "that code isn't valid".
- **U6 Cap reached (1001st):** → "this code is no longer available".
- **U7 TTL expiry:** redeems Aug 20, uses 1; day 89 → 2 left; day 91 → expired, buy-paywall.
- **U8 Paid subscriber redeems:** allowed; +3 PROMO credits sit as overflow (rarely used at 90/mo). *(Or restrict to non-paid — Open Question.)*
- **U9 Two different codes:** with multiple codes, user may redeem each once (per-code cap). *(Optional "one trial per user ever" policy — Open Question.)*
- **U10 Signed-out CTA:** redeem CTA → `/sign-in?redirectTo=/app` → back to redeem.
- **U11 Mobile:** redeem card responsive, no overflow.

### Abuser (negative)
- **T1 Multi-account farm:** blocked/slowed by email verify + IP velocity + global cap; surfaced in stats.
- **T2 Brute force:** generic errors + rate limit.
- **T5 Race/replay:** no double grant.

---

## 11. Risk Register
| Risk | Likelihood | Impact | Mitigation | Owner action |
|---|---|---|---|---|
| Free-baseline=0 display/enforcement mismatch for existing free rows | Med | Med | Constant→0 **+** one-time `UPDATE UsagePeriods SET QuotaLimit=0 WHERE PeriodKey='free:lifetime'`; verify which value `ReserveAsync` trusts | confirm grandfathering (likely ~no live users) |
| Global-cap over-issue under load | Low | Med | Atomic conditional `++RedemptionCount` (not read-then-write) | — |
| IP unobtainable / spoofable at C# | Med | High (defeats anti-abuse) | Proxy forwards `cf-connecting-ip` via shared-secret header; C# ignores untrusted XFF | confirm/establish proxy↔Functions secret |
| Multi-account farming despite defenses | Med | Med | global cap + velocity + activation monitoring; per-user codes as escalation | watch data after launch |
| Copy change breaks contract tests → blocks deploy | High if missed | Med | Update `pricing-auth-visual-system` + `workspace-copy` tests in same PR | — |
| Admin UI scope creep | Med | Low | Ship endpoints first; minimal table UI | pick Option A vs B |
| Code value brand drift ("AsHuman") | Low | Low | optional rename; grep-clean either way | choose value |

---

## 12. API Contracts (user-facing)

**`POST /api/promo/redeem`** (auth required; proxy forwards trusted client IP)
```jsonc
// req: { "code": "ReplyAsHuman2026" }
// 200: { "creditsGranted":3, "totalRemaining":3, "expiresAt":"2026-11-18T…Z", "alreadyRedeemed":false }
// errors {error,message}: 401 unauthorized · 400 invalid_request · 422 invalid_code ·
//   422 code_expired · 409 already_redeemed · 409 code_exhausted · 429 too_many_attempts · 500 server_error
```
**`/api/me` extension** (preferred over a 2nd round-trip; drives `/app` empty-state branching):
```jsonc
"promo": { "hasRedeemed": false, "eligible": true, "trialRemaining": 0, "trialExpiresAt": null }
```
**Proxies (Next.js, mirror `app/api/me/route.ts`):** `app/api/promo/redeem/route.ts` (POST, same-origin enforced per AGENTS.md, forwards `cf-connecting-ip` + proxy secret).

---

## 13. Frontend & Backend Change List (explicit)

### Backend (C#)
1. **Domain:** `PromoCode.cs`, `PromoCodeRedemption.cs` (+ enums).
2. **Infrastructure:** `AppDbContext` DbSets + `OnModelCreating` (unique `Code`; unique `(codeId,userId)`; indexes; FKs); migration `_AddPromoCodes`.
3. **`PromoService.cs`:** `RedeemAsync`, `GetStatusAsync` (reuse `ExecuteInTransactionAsync`); `HashIp`; IP-velocity query; atomic cap update.
4. **`AccountService`:** free `QuotaLimit` 3→0 (config-backed `FREE_BASELINE_REWRITES`); add `promo` block + friendly source labels (`"PROMO"`→"Trial rewrites") to summary; **+ data migration** for existing free rows; extend `DeleteAccountAsync` for redemptions.
5. **Admin:** `AdminService` promo methods (create/list/detail/edit/disable/stats) + `AdminAuditLog`; `PromoAdminHttpFunctions` (or extend `AdminHttpFunctions`) using `RequireAdminAsync`.
6. **`PromoHttpFunctions.cs`:** `POST promo/redeem` (+ optional `GET promo/status`); read trusted client-IP header.
7. **Config/secrets:** `FREE_BASELINE_REWRITES`, `PROMO_IP_HASH_SALT`, `PROMO_IP_VELOCITY_MAX_24H`, proxy shared secret (names only; set in Worker vars + Functions app settings, kept in sync).

### Frontend (Next.js)
8. **Proxies:** `app/api/promo/redeem/route.ts` (+ status), same-origin + IP/secret forwarding.
9. **`components/app/redeem-code-card.tsx`** (input + Redeem + inline states) and `/app/page.tsx` empty-state branching on `promo.hasRedeemed`; map PROMO source to "Trial rewrites".
10. **Admin UI (Option A):** `app/admin/promo-codes/page.tsx` (+ components) — list/create/disable/stats; 403 for non-admins.
11. **Copy** (de-emphasize "free", emphasize "redeem"): `components/landing/hero.tsx:6`, `…/closing-cta.tsx:28`, `…/pricing-v2.tsx`, `app/pricing/page.tsx`, `components/site-footer.tsx:68`, `app/developers/page.tsx:245`, `components/auth/google-oauth-card.tsx:45`, `app/app/page.tsx` quota label.
12. **Tests:** update `tests/unit/pricing-auth-visual-system.test.ts` + `tests/unit/workspace-copy.test.ts` to new strings (they gate `cf:deploy`).

---

## 14. Rollout Plan
1. **Schema + service + endpoints + admin** (free baseline still 3; no user-visible change). Merge → migration applies to Azure SQL. Create `ReplyAsHuman2026` via admin endpoint (Option B) to validate end-to-end.
2. **Free baseline 3→0** + consistency data migration (`UsagePeriods` free rows). Decide grandfathering (≈no live users → safe to zero).
3. **User UI** (redeem card + `/app` branching + copy + tests) — Playwright verify desktop/mobile.
4. **Admin UI** (Option A).
5. **Worker-preview smoke** of redeem→rewrite→paywall + invalid/expired/already/cap + IP forwarding; banned-term grep clean.
6. **Deploy** (push `main` → CI → `cf:deploy` + `dotnet ef database update`). Rollback = `wrangler rollback`; code instantly disablable via `IsActive=false`.

**Guardrails (unchanged):** don't touch `LAUNCH_CONFIRMED`/Stripe price/webhook secrets/DNS; Stripe stays in configured mode; no real charges; keep Worker vars ↔ Functions app settings in sync.

---

## 15. Verification Plan
- **Backend (`dotnet-backend-testing`):** redeem happy/idempotent/concurrent (retrying execution strategy + unique index → one grant); validity gates (invalid/expired/boundary); **global cap exactness** under parallel load; consumption 3→2→1→0→paywall; new-user remaining=0; IP-velocity block; account-erase covers redemptions; admin create/list/disable/stats + audit.
- **Frontend (`ui-browser-testing`):** new-user redeem card (not paywall); redeem success → workspace "3 trial · expires in N days"; invalid/expired/already inline errors; exhaustion → buy-paywall; admin page create/disable/stats; desktop+mobile screenshots, no console/network errors; copy contract tests green.
- **Gates before deploy:** `npm run test` + `dotnet test` green; banned-term grep clean; Worker-preview smoke of the full loop incl. IP forwarding.

---

## 16. Open Questions (owner inputs — do not invent)
1. **"一人一码" meaning:** confirm = *one redemption per identity* (my reading), or *literal unique code string per user*?
2. **Admin UI:** Option A (build minimal `/admin/promo-codes` page — recommended) or B (API-only stopgap)?
3. **Code value:** `ReplyAsHuman2026` for testing (confirmed). Final launch value? (brand note in §7.5).
4. **Expiry instant:** `2026-08-31 23:59:59` NZ (`+12:00` NZST = `11:59:59Z`) — confirm.
5. **Credit TTL:** 90 days (default) or shorter (e.g. 30d) to reinforce "act now"?
6. **Global cap:** capped (e.g. 1000) or uncapped at launch? (admin can set either.)
7. **Paid users redeeming (U8)** + **one-trial-per-user-across-codes (U9):** allow or restrict?
8. **IP defense depth:** OK to add a proxy↔Functions shared secret + DB IP-velocity now? Add Cloudflare Turnstile on signup/redeem (T4)?
9. **Existing users:** grandfather any current free quota or zero it?

---

## 17. Implementation Checkpoints (for the Codex brief)
1. Entities + DbContext config + migration `_AddPromoCodes` (no other entity edits beyond nav collection).
2. `PromoService` (redeem/status) with serializable tx, **atomic cap increment**, idempotency catch, IP hash + velocity.
3. `PromoHttpFunctions` (user) + admin promo endpoints (reuse `RequireAdminAsync` + audit).
4. `AccountService`: `promo` summary block + labels; **separately** free `QuotaLimit` 3→0 (config) + consistency migration; verify `ReserveAsync` quota source.
5. Next.js proxies (+ trusted IP/secret forwarding); `redeem-code-card` + `/app` branching; admin UI (Option A).
6. Copy changes + update both contract tests.
7. Extend `DeleteAccountAsync` for `PromoCodeRedemption`.
8. Tests per §15; banned-term grep; Worker-preview smoke; deploy.

**Echo in every Codex brief:** banned terms `humanizer|bypass|undetect|detector|evade`; no secrets in source (validate env at runtime); no `migrate reset`/force-reset/drops; don't touch `LAUNCH_CONFIRMED`/Stripe price/webhook secrets/DNS; no real charges; keep Worker vars ↔ Functions app settings in sync.
