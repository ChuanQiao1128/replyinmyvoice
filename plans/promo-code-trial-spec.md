# Promo-Code Trial Specification (v3 — decisions locked)

> Replace the automatic "3 free rewrites on sign-up" with a **redeemable promo code**. Launch
> with **one shared universal code** (`ReplyAsHuman2026` = owner's test value; admin-settable),
> broadcast in advertising; build a commercial-grade, admin-managed, abuse-resistant,
> analytics-ready system underneath.

- **Status:** Design only — no source code written yet. All product/architecture decisions below are **locked** (owner-confirmed 2026-06-02).
- **Branch:** `feat/promo-code-trial`
- **Author:** Claude Code (supervisor) — `system-spec-synthesis` (+ `state-machine-modeling`, `data-module-review`, `resilience-test-generation` lenses).
- **Changelog:** v1 design → v2 feasibility/threat-model/user-stories → **v3 locks decisions + records Turnstile key verification + owner-setup checklist.**
- **Implementation:** delegated to Codex workers after this doc is signed off.

---

## 0. Decisions Locked (owner-confirmed 2026-06-02)

| # | Topic | Decision |
|---|---|---|
| D1 | Code model | **One shared universal code string** (e.g. `ReplyAsHuman2026`), broadcast in ads; **each account redeems once** (`MaxRedemptionsPerUser=1`). NOT per-user-unique strings. *(Schema still supports unique-per-user codes later for influencer/referral campaigns — `MaxRedemptionsGlobal=1` each — no schema change needed then.)* |
| D2 | Free baseline | New signed-in users get **0** automatic rewrites (`GetUsagePlan` free quota `3 → 0`). Trial rewrites come only from redeeming a code. |
| D3 | Grant = credit | A valid redemption inserts `RewriteCredit{Source="PROMO", AmountGranted=3, ExpiresAt=redeemedAt+90d}` — reuses the existing consumption/paywall path unchanged. |
| D4 | Credit TTL | **90 days** from redemption (`GrantTtlDays=90`). |
| D5 | Code expiry | `ValidUntil = 2026-08-31 23:59:59 NZ` (`+12:00` NZST = `2026-08-31T11:59:59Z`). Two independent clocks: code window vs. 90-day credit. |
| D6 | Global cap | **Optional** per code (`MaxRedemptionsGlobal` nullable). The Nth+1 redemption (e.g. 1001st) is rejected with "code reached its limit". |
| D7 | Per-user cap | `MaxRedemptionsPerUser = 1` (one redemption per account per code). |
| D8 | Admin console | **Build a minimal admin UI** (`/admin/promo-codes`): create / list / disable / per-code stats. Scope = **promo codes only** for now (don't port other admin surfaces yet). |
| D9 | Human verification | **Cloudflare Turnstile on BOTH signup and redeem.** Verified at the Worker/proxy layer. Keys verified working (§0.2). Local dev uses Cloudflare official test keys. |
| D10 | Disposable-email block | **Block disposable/temp-mail domains at signup** via a bundled public blocklist (server-side). |
| D11 | IP abuse defense | Capture client IP (hashed) at redeem; **hard-block at 5 redemptions/IP/24h, flag (log-only) from the 2nd**; configurable. DB-backed in C# (not the ephemeral in-memory limiter). Reasoning: shared-IP / mobile CGNAT false positives. |
| D12 | Proxy→Functions IP trust | New **proxy shared secret** so C# can trust the forwarded client IP (today the proxy forwards only the bearer; no secret, no IP). Guards against `X-Forwarded-For` spoofing. |
| D13 | Redeem flow | **Login first → redeem in `/app`** (no pre-signup landing code field). |
| D14 | Paid users | **Allowed** to redeem (credits sit as overflow; harmless at 90/mo). |
| D15 | Existing users | **Zero** the free baseline for everyone (≈no live users today). |
| D16 | Stats KPIs | Total redemptions · distinct users · **activation rate (redeemers who used ≥1)** · daily curve · IP-cluster abuse flags. |
| D17 | Admin allowlist | `ADMIN_EMAILS` includes `chuanqiao1128@gmail.com` (confirmed present in `.env.local`). |

### 0.1 Open items still needing owner action (small)
- **None blocking design.** Only the production secrets get set at deploy time (auto via `wrangler`/`az`). The owner's one manual task — the Turnstile secret — is **done** (§0.2).

### 0.2 Turnstile key verification (tested 2026-06-02)
| Key | Where | Result |
|---|---|---|
| Site key `0x4AAAAAADdY3Xy1e6vEJU8E` | public; frontend | ✅ **Valid & recognized.** Rendered via Playwright on `localhost`; returned client error `110200` = *domain not allowed* (a **real, recognized** key — not `110100` invalid-key). It is **domain-locked to `replyinmyvoice.com`**; `localhost` is not allowed → use CF test keys for dev. |
| `TURNSTILE_SECRET_KEY` | secret; `.env.local` | ✅ **Valid & recognized.** `siteverify` probe returned `invalid-input-response` (CF accepted the secret, rejected only a dummy token). Length 35. Value never printed. |
| Dev/local | both | Use Cloudflare official **test keys**: site `1x00000000000000000000AA` (always passes) / secret `1x0000000000000000000000000000000AA`. Real keys are prod-only. |

---

## 0.5 Owner Setup & Prerequisites — your plate vs. mine

**Already done by owner ✅:** Turnstile widget created; Site Key provided; `TURNSTILE_SECRET_KEY` in `.env.local` (verified); `ADMIN_EMAILS=chuanqiao1128@gmail.com`.

**Owner's remaining manual tasks:** *none required before development.* (Optional, for prod later: nothing — production secrets are set during deploy via existing `wrangler`/`az` tokens.)

**Environment variables** (names only for secrets; I never print/commit secret values):
| Var | Type | Provided by | Lives in |
|---|---|---|---|
| `NEXT_PUBLIC_TURNSTILE_SITE_KEY` = `0x4AAAAAADdY3Xy1e6vEJU8E` | public | ✅ owner | `.env.local` + Worker vars (prod). Dev: CF test site key. |
| `TURNSTILE_SECRET_KEY` | secret | ✅ owner (in `.env.local`) | `.env.local` + Worker secret (prod). Dev: CF test secret. |
| `PROMO_PROXY_SHARED_SECRET` | secret | **I generate** | Worker secret + Functions app setting |
| `PROMO_IP_HASH_SALT` | secret | **I generate** | Functions app setting |
| `PROMO_IP_VELOCITY_MAX_24H` = `5` | config | default | Functions app setting |
| `PROMO_IP_VELOCITY_FLAG_FROM` = `2` | config | default | Functions app setting |
| `FREE_BASELINE_REWRITES` = `0` | config | default | Functions app setting |
| `ADMIN_EMAILS` | config | ✅ owner | `.env.local` + prod (present) |
| `NEXT_PUBLIC_AZURE_API_BASE_URL` | config | ✅ exists | — |

**My plate:** everything else — tables, migrations, redeem service, endpoints, admin page, Turnstile wiring, disposable-email list, IP defense, copy, tests, deploy.

---

## 1. Feasibility & Gap Assessment (verified against live code 2026-06-02)

Verdict: **no blocker.** The grant + consumption machinery already exists; the net-new work is
the promo layer, the admin UI (none exists today), and durable abuse defense.

| Requirement | Today | Gap | Effort |
|---|---|---|---|
| Per-code config (credits/validity/caps) | `RewriteCredit` grants exist; no per-code config | `PromoCode` table | M |
| Per-person + global caps | ❌ | unique `(codeId,userId)` + atomic global counter | S |
| Admin create/disable | Admin auth (`ADMIN_EMAILS`) + audit + grant pattern exist; **NO admin web UI in `app/`** (API-only today) | endpoints S; **UI M** |
| Disable → immediate | pattern | `IsActive=false` checked per redeem | S |
| 90-day TTL / two clocks | `AdminCreditExpiryDays=90` precedent | per-code `GrantTtlDays` | S |
| Trace IP / anti multi-account | `cf-connecting-ip` readable at Worker (`lib/auth-rate-limit.ts`); **in-memory/ephemeral**; C# can't see client IP unless forwarded; forwarded XFF spoofable | proxy-secret + DB velocity + Turnstile + disposable-email | M–L |
| Turnstile | not wired; keys verified | widget on signup+redeem; proxy verify | M |

---

## 2. TL;DR (load-bearing idea)

Production already grants & consumes "rewrite credits": `RewriteCredit` rows (Source =
`PURCHASE`/`ADMIN`) are consumed by `QuotaService` once period quota is used up; `/api/me`
already sums them into `remaining` with a per-source "expires in N days" breakdown. So:
1. **Stop auto-granting** — free baseline `3 → 0` + a one-time data migration so display==enforcement.
2. **Add a promo layer** — 2 tables, a redeem service, a user endpoint, admin endpoints + minimal UI, a `/app` redeem card, copy changes, Turnstile, disposable-email + IP defense.
3. **Redemption = grant a credit** (`Source="PROMO"`) — consumption/paywall/402/quota-race/Stripe **unchanged**.

Risk concentrates in: (a) free-baseline=0 cutover, (b) redeem idempotency/race + global-cap race, (c) multi-account abuse — all addressed below.

---

## 3. Requirements (consolidated)

### 3.1 Per-code config (admin-set)
Credits, `GrantTtlDays` (90), `ValidFrom`/`ValidUntil` (NZ), `MaxRedemptionsPerUser` (1),
`MaxRedemptionsGlobal` (nullable), `IsActive` (immediate kill switch).

### 3.2 Code model (D1) — locked
**One shared universal code string**, advertised broadly, each account redeems once. Resolved
the earlier wording mix-up: this is *not* per-user-unique strings (those remain a future option
on the same schema).

### 3.3 Two clocks (D5) — locked
Redeem window (until Aug 31 NZ) is independent of the granted credit's 90-day life. Redeeming
Aug 20 → 3 rewrites valid ~90 days (≈ Nov 18); code expiry doesn't retroactively kill granted credits.

### 3.4 Goal: select for genuine intent
Filter = removing frictionless auto-free + signup/email-verify + disposable-email block +
Turnstile + one-redemption-per-identity + controlled distribution. Honest limits in §8.4.

---

## 4. Current System (grounded; verified 2026-06-02)

Prod = **C#/.NET Azure Functions + Azure SQL (EF Core)** behind **Cloudflare Worker (OpenNext
Next.js)**. TS `lib/` rewrite pipeline + `lib/generated/prisma/**` are dead Slice-7 code.

| Concern | Live fact |
|---|---|
| Free "3" | `AccountService.GetUsagePlan()` → `("free","free:lifetime",3)` — a `UsagePeriod.QuotaLimit`, not a credit. |
| `/api/me` | `AccountService.GetOrCreateAccountSummaryAsync()` → `remaining = periodRemaining + creditRemaining`; `Sources[]` per credit w/ `ExpiresInDays`; `Exhausted = remaining<=0`. |
| Grant pattern | `AdminService.GrantCreditsAsync()` → `RewriteCredit{Source="ADMIN", ExpiresAt=now+90d}` + `AdminAuditLog`. |
| Consumption | `QuotaService.ReserveAsync()` + `FindUsableCreditAsync()` (period first, then credits, soonest-expiring first). **Unchanged.** |
| Serializable tx | `StripeEventService.ExecuteInTransactionAsync()`. |
| Admin auth | `AdminAccess.RequireAdminAsync` → allow if Entra `oid` or `email` ∈ `ADMIN_EMAILS` (comma-sep, case-insensitive). |
| Admin endpoints | `AdminHttpFunctions.cs` (ping/users/stats/credits/suspension/refund). **API-only — no `app/` admin page.** |
| Rate-limit precedent | `lib/auth-rate-limit.ts`: per-email+per-IP buckets, `clientIpFromRequest()` reads `x-forwarded-for` then `cf-connecting-ip`. **In-memory per-isolate → ephemeral.** |
| Proxy→C# | `app/api/me/route.ts` forwards **only** `Authorization: Bearer <token>` — **no shared secret, no IP forwarded** today. |
| Identity | Entra `oid` → `AppUser.ExternalAuthUserId` (unique); PK `Guid Id`. |
| Migrations | auto-applied on merge to `main` (`dotnet ef database update` on Azure SQL). |
| Copy tests | `tests/unit/pricing-auth-visual-system.test.ts`, `tests/unit/workspace-copy.test.ts` (gate `cf:deploy`). |

No existing promo/coupon code; C# `Referral` is vestigial.

---

## 5. Proposed Architecture

```
sign up (Turnstile + disposable-email check) + verify email ──▶ AppUser{Inactive}
        │
GET /api/me ── GetUsagePlan()=("free","free:lifetime",0)  ← 3→0
        │      remaining=0; promo.hasRedeemed=false
        ▼
/app "Redeem your code" card  ← NEW empty state (not buy-paywall)
        │
POST /api/promo/redeem {code, turnstileToken}
        │  proxy: same-origin → verify Turnstile (secret+IP) → forward {bearer, X-Client-IP, X-Proxy-Secret}
        │  C# PromoService.RedeemAsync(): normalize → IP velocity (DB) → Serializable tx:
        │     atomic ++RedemptionCount (cap guard) → insert RewriteCredit{PROMO,3,+90d}
        │     → insert PromoCodeRedemption{unique(codeId,userId), RedeemIpHash}
        ▼
GET /api/me ── remaining=3; Sources=[…,{PROMO, ExpiresInDays≈90}]
        ▼
/app workspace ── POST /api/rewrite consumes PROMO credit (UNCHANGED)
        ▼
exhausted ── /app buy-paywall (promo.hasRedeemed=true)
```

---

## 6. Data Model (`data-module-review`)

EF conventions: `Guid Id`, `Guid RowVersion` concurrency token, `DateTimeOffset`, string enums
via `HasConversion<string>`+`HasMaxLength`, indexes in `OnModelCreating`. Migration
`_AddPromoCodes`; no reset/force-reset/drops.

### `PromoCode`
```csharp
public sealed class PromoCode {
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }            // normalized trim+UPPER+strip space/dash; UNIQUE
    public string? Description { get; set; }
    public PromoCodeKind Kind { get; set; } = PromoCodeKind.TrialCredits;
    public int CreditsGranted { get; set; }              // 3
    public int GrantTtlDays { get; set; } = 90;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }       // 2026-08-31T23:59:59+12:00
    public int? MaxRedemptionsGlobal { get; set; }       // null = uncapped
    public int MaxRedemptionsPerUser { get; set; } = 1;
    public int RedemptionCount { get; set; }             // denormalized; cap guard + fast stats
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();
    public ICollection<PromoCodeRedemption> Redemptions { get; } = new List<PromoCodeRedemption>();
}
public enum PromoCodeKind { TrialCredits }
```
`HasIndex(Code).IsUnique()`; `Code` max 40; `Description` max 200; `Kind` conv-string max 40; `RowVersion` token.

### `PromoCodeRedemption` (analytics source of truth)
```csharp
public sealed class PromoCodeRedemption {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PromoCodeId { get; set; }  public PromoCode? PromoCode { get; set; }
    public Guid UserId { get; set; }       public AppUser? User { get; set; }
    public Guid RewriteCreditId { get; set; }
    public int CreditsGranted { get; set; }
    public string CodeSnapshot { get; set; } = "";
    public string? RedeemIpHash { get; set; }            // salted SHA-256(IP); never raw IP
    public PromoCodeRedemptionStatus Status { get; set; } = PromoCodeRedemptionStatus.Applied;
    public DateTimeOffset RedeemedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReversedAt { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
public enum PromoCodeRedemptionStatus { Applied, Reversed }
```
- **`HasIndex(new {PromoCodeId, UserId}).IsUnique()`** — one-per-user + redeem race backstop.
- `HasIndex(PromoCodeId)`, `HasIndex(UserId)`, `HasIndex(RedeemIpHash)`, `HasIndex(RedeemedAt)`.
- FK `PromoCodeId→PromoCode` Restrict; `UserId→AppUser` Cascade; `RewriteCreditId` plain indexed Guid (no cascade FK — avoids SQL Server multiple-cascade-path).
- `RedeemIpHash` max 128 nullable.

### Global-cap correctness (D6 "1001st" guarantee)
Atomic conditional increment (race-proof even at READ COMMITTED), inside the redeem tx:
```sql
UPDATE PromoCodes SET RedemptionCount=RedemptionCount+1, UpdatedAt=@now, RowVersion=@new
WHERE Id=@id AND IsActive=1 AND @now BETWEEN ValidFrom AND ValidUntil
  AND (MaxRedemptionsGlobal IS NULL OR RedemptionCount < MaxRedemptionsGlobal);
-- rows affected == 0 ⇒ disabled/outside-window/cap-reached → re-read to pick the message
```

### Account deletion
Extend `AccountService.DeleteAccountAsync` to also handle `PromoCodeRedemption`: null `RedeemIpHash`, keep the row (totals stay accurate).

### Analytics (D16)
```sql
-- redeemers + window
SELECT COUNT(*) redemptions, MIN(RedeemedAt) first, MAX(RedeemedAt) last
FROM PromoCodeRedemptions WHERE PromoCodeId=@id AND Status='Applied';
-- activation (genuine-need signal): redeemers who used ≥1 granted rewrite
SELECT SUM(CASE WHEN rc.AmountConsumed>0 THEN 1 ELSE 0 END) activated, COUNT(*) total
FROM PromoCodeRedemptions r JOIN RewriteCredits rc ON rc.Id=r.RewriteCreditId
WHERE r.PromoCodeId=@id AND r.Status='Applied';
-- abuse: same IP hash across many accounts
SELECT RedeemIpHash, COUNT(*) n FROM PromoCodeRedemptions
WHERE PromoCodeId=@id AND RedeemIpHash IS NOT NULL GROUP BY RedeemIpHash HAVING COUNT(*)>1;
```

---

## 7. Admin Console (D8 — minimal UI, promo-only scope)

**Auth:** reuse `AdminAccess.RequireAdminAsync` (`ADMIN_EMAILS`). Mutations write `AdminAuditLog`.

**Endpoints** (extend `AdminService` + new `PromoAdminHttpFunctions`):
| Method/Route | Purpose |
|---|---|
| `POST /api/admin/promo-codes` | Create `{code, description, creditsGranted, grantTtlDays, validFrom, validUntil, maxRedemptionsGlobal?, maxRedemptionsPerUser?}` (validate unique code, sane numbers, `validUntil>validFrom`) + audit. |
| `GET /api/admin/promo-codes` | List + `redemptionCount` + derived status. |
| `GET /api/admin/promo-codes/{id}` | Detail + stats (redemptions, distinct users, daily curve, activation rate, top IP-hash clusters). |
| `PATCH /api/admin/promo-codes/{id}` | Edit validUntil/caps/description (audited). |
| `POST /api/admin/promo-codes/{id}/disable` `/enable` | Flip `IsActive` — immediate. |

**UI:** new `app/admin/promo-codes/page.tsx` — table + "New code" form + disable toggle +
per-code stats. 403 for non-admins. Scope = promo only.

---

## 8. Security — Threat Model & Defense-in-Depth

### 8.1 IP capture architecture (makes "trace IP" actually work)
- **Capture at the Worker/proxy** (`app/api/promo/redeem/route.ts`): read `cf-connecting-ip` (trustworthy — CF sets it), forward to C# as a header.
- **Spoofing guard (D12):** Functions is internet-reachable + anonymous-JWT, so a direct caller could forge `X-Forwarded-For`. **C# trusts the forwarded IP only when accompanied by `PROMO_PROXY_SHARED_SECRET`** (new secret, set on Worker + Functions). *(No such secret exists today — proxy forwards only the bearer.)*
- **Storage:** C# hashes IP (salted SHA-256, `PROMO_IP_HASH_SALT`) → `PromoCodeRedemption.RedeemIpHash`. Raw IP never stored/logged.

### 8.2 Velocity (D11 — durable, in C#)
- Authoritative check in the redeem path: `COUNT(redemptions WHERE RedeemIpHash=@h AND RedeemedAt>now-24h)`. **Hard-block at ≥5; flag (log, don't block) from ≥2.** Configurable (`PROMO_IP_VELOCITY_MAX_24H`, `PROMO_IP_VELOCITY_FLAG_FROM`).
- Rationale: shared IPs (households, offices, schools, **mobile CGNAT** — common in NZ) make a hard `1` block legitimate users. A hard `1` also doesn't stop IP-rotating attackers. The in-memory `lib/auth-rate-limit.ts` is ephemeral → only a cheap first layer.

### 8.3 Human + email defenses (D9, D10)
- **Turnstile on signup + redeem**, verified at the proxy (`siteverify` with `TURNSTILE_SECRET_KEY` + client IP). Dev uses CF test keys.
- **Disposable-email block at signup:** bundled public disposable-domain blocklist (~thousands), checked server-side in the signup path; periodic refresh. (Kills the cheap fake-email farm.)

### 8.4 Threats
| # | Threat | Mitigation | Residual |
|---|---|---|---|
| T1 | One human, many accounts | email-verify + **disposable-email block** + **Turnstile** + IP velocity + global cap + activation monitoring | bounded by global cap |
| T2 | Brute-force code guessing | generic `invalid_code` + redeem rate limit + Turnstile | low |
| T3 | Universal code leaks | expected; bounded by global cap + `ValidUntil` + instant disable; per-user codes as escalation | accepted |
| T4 | Bots auto signup+redeem | **Turnstile** (both) + IP velocity | low |
| T5 | Replay / double-grant race | idempotent + unique `(codeId,userId)` + atomic cap + Serializable | negligible |
| T6 | Delete + re-signup farm | retain `RedeemIpHash` (PII nulled) → velocity still catches bursts | low–med |
| T7 | IP header spoofing | `PROMO_PROXY_SHARED_SECRET`-guarded IP header | low (must implement) |
| T8 | Admin abuse | `ADMIN_EMAILS` + audit log | low |

### 8.5 Honest limits & upgrade path
A universal code **will leak**; free email signups enable bounded multi-account farming. Real
escalation: **per-user unique codes** (`MaxRedemptionsGlobal=1` each), same schema. Launch
universal, watch redemption/activation data, escalate only if abused.

### 8.6 Hygiene
No banned terms (`humanizer|bypass|undetect|detector|evade`) in identifiers/copy. `ReplyAsHuman2026`
does not trip the grep (usable). No secrets in source; code value lives in DB; never log code values or raw IPs.

---

## 9. State Machines (`state-machine-modeling`)

**`PromoCode`** (derived states):
```
[Pending] now<ValidFrom → [Active] ┬ now>ValidUntil → [Expired]
                                    ├ RedemptionCount≥MaxGlobal → [Exhausted]
                                    └ IsActive=false → [Disabled] ⇄ enable → [Active]
```
Redeemable iff `IsActive ∧ ValidFrom≤now≤ValidUntil ∧ (MaxGlobal==null ∨ RedemptionCount<MaxGlobal)`. Invariant: `RedemptionCount==COUNT(Applied)`.

**`PromoCodeRedemption`** (per user×code):
```
(none) ─success→ [Applied] ─admin clawback→ [Reversed]
   ▲ second Applied for same (code,user) → blocked by unique index
```
Invariants: ≤1 row per `(codeId,userId)`; every `Applied` references a real `RewriteCredit` created in the same tx.

---

## 10. Redeem Algorithm (idempotent, race-safe, IP-aware)

```
[proxy] same-origin → verify Turnstile(token, secret, cf-connecting-ip); fail → 403/invalid_captcha
        forward to C# with Authorization + X-Client-IP(cf-connecting-ip) + X-Proxy-Secret
[C#] RedeemAsync(extUserId, email, rawCode, trustedIp?, now):
  code = Normalize(rawCode); if !FormatValid: return InvalidCode
  user = GetOrCreateUser(...)
  ipHash = trustedIp? HashIp(trustedIp) : null
  if ipHash!=null && Recent(ipHash, now-24h) >= MAX(5): return IpVelocityBlocked   // flag from 2
  ExecuteInTransactionAsync(Serializable):
    pc = PromoCodes.Single(Code==code)
    if pc==null || !pc.IsActive: return InvalidCode
    if now<pc.ValidFrom: return InvalidCode
    if now>pc.ValidUntil: return Expired
    if Redemptions.Any(PromoCodeId==pc.Id && UserId==user.Id): return AlreadyRedeemed  // idempotent
    affected = ExecuteUpdate(++RedemptionCount WHERE cap predicate)
    if affected==0: return CapReachedOrInactive
    credit = RewriteCredit{UserId, "PROMO", pc.CreditsGranted, 0, now, now+pc.GrantTtlDays}
    Add(credit); Add(Redemption{..RewriteCreditId=credit.Id, CodeSnapshot=code, RedeemIpHash=ipHash, Applied, now})
    try SaveChanges/commit
    catch unique(codeId,userId) violation: rollback; return AlreadyRedeemed
  return Success(credit.AmountGranted, credit.ExpiresAt)
```

### Resilience matrix
| Scenario | Expected |
|---|---|
| Same user, 2 concurrent redeems | one Applied + one credit; other → AlreadyRedeemed |
| Replay | 409 already_redeemed, balance unchanged |
| `ValidUntil` boundary | `≤` ok; `>` → expired (single `now`/request) |
| Global cap N, N+5 concurrent | exactly N applied; overflow → code_exhausted |
| Grant then rewrite | consumes PROMO credit (soonest-expiring first) |
| Engine fail on trial rewrite | no-charge releases reservation; credit not consumed |
| Same IP, many accounts | ≥5 blocked; ≥2 flagged+logged |
| Spoofed XFF direct to Functions | ignored unless proxy-secret valid |
| Missing/blocked Turnstile | proxy rejects before C# (invalid_captcha) |

---

## 11. User Stories

**Admin:** A1 create code · A2 disable (immediate) · A3 view stats (incl. activation rate) · A4 edit validUntil/cap · A5 duplicate-code rejected · A6 list with derived status.

**User:** U1 new user → redeem card (not paywall) · U2 redeem valid → +3 · U3 redeem twice → already-redeemed · U4 expired → message · U5 invalid → message · U6 cap reached (1001st) → message · U7 TTL: redeem Aug 20, day89=2 left, day91 expired → paywall · U8 paid user redeems (allowed, overflow) · U10 signed-out CTA → `/sign-in?redirectTo=/app` · U11 mobile responsive · **U12 signup w/ Turnstile** (bot/blank token → blocked) · **U13 signup w/ disposable email → rejected with guidance**.

**Abuser:** T1 multi-account farm (slowed/bounded) · T2 brute force (generic + limit) · T5 race/replay (no double grant).

---

## 12. Risk Register
| Risk | L | I | Mitigation | Owner action |
|---|---|---|---|---|
| Free-baseline=0 display/enforcement mismatch | Med | Med | constant→0 + `UPDATE UsagePeriods SET QuotaLimit=0 WHERE PeriodKey='free:lifetime'`; verify which value `ReserveAsync` trusts | confirm grandfather (≈none) |
| Global-cap over-issue | Low | Med | atomic conditional `++` | — |
| IP unobtainable/spoofable at C# | Med | High | proxy forwards `cf-connecting-ip` via shared-secret header | (auto at deploy) |
| Multi-account farming | Med | Med | disposable-email + Turnstile + velocity + cap + monitoring | watch data |
| Copy change breaks contract tests | High-if-missed | Med | update both tests in same PR | — |
| Turnstile prod widget domain-locked | Low | Low | dev uses CF test keys; prod uses real key on replyinmyvoice.com | — |

---

## 13. API Contracts

**`POST /api/promo/redeem`** (auth; proxy verifies Turnstile + forwards trusted IP)
```jsonc
// req: { "code": "ReplyAsHuman2026", "turnstileToken": "<token>" }
// 200: { creditsGranted:3, totalRemaining:3, expiresAt:"…Z", alreadyRedeemed:false }
// errors: 401 unauthorized · 400 invalid_request · 403 invalid_captcha · 422 invalid_code ·
//   422 code_expired · 409 already_redeemed · 409 code_exhausted · 429 too_many_attempts/ip_velocity · 500
```
**`/api/me` extension:** `"promo": { hasRedeemed, eligible, trialRemaining, trialExpiresAt }` (drives `/app` empty-state).

**Signup** (existing Entra-native path): add Turnstile token verification + disposable-email-domain rejection server-side before account creation.

---

## 14. Frontend & Backend Change List

### Backend (C#)
1. Domain: `PromoCode.cs`, `PromoCodeRedemption.cs` (+ enums).
2. Infra: `AppDbContext` DbSets + config + migration `_AddPromoCodes`.
3. `PromoService.cs`: redeem/status, `HashIp`, IP-velocity query, atomic cap update.
4. `AccountService`: free quota 3→0 (config `FREE_BASELINE_REWRITES`) + `promo` summary block + friendly source labels (`PROMO`→"Trial rewrites") + data migration + extend `DeleteAccountAsync`.
5. Admin: `AdminService` promo methods + `PromoAdminHttpFunctions` (+ audit).
6. `PromoHttpFunctions.cs`: `POST promo/redeem` (+ `GET promo/status`); read trusted IP header.
7. Disposable-email check + Turnstile-token verification in the **signup** path (locate exact insertion point at build time — Entra-native).
8. Config/secrets: the §0.5 env vars.

### Frontend (Next.js)
9. Proxies: `app/api/promo/redeem/route.ts` (+ status) — same-origin, Turnstile verify, IP+secret forward.
10. `components/app/redeem-code-card.tsx` + `/app` empty-state branching on `promo.hasRedeemed` + PROMO label.
11. **Turnstile widget** on signup form + redeem card (`NEXT_PUBLIC_TURNSTILE_SITE_KEY`).
12. Admin UI: `app/admin/promo-codes/page.tsx`.
13. Copy (de-emphasize "free", emphasize "redeem"): `components/landing/hero.tsx:6`, `…/closing-cta.tsx:28`, `…/pricing-v2.tsx`, `app/pricing/page.tsx`, `components/site-footer.tsx:68`, `app/developers/page.tsx:245`, `components/auth/google-oauth-card.tsx:45`, `app/app/page.tsx` quota label.
14. Tests: update `tests/unit/pricing-auth-visual-system.test.ts` + `tests/unit/workspace-copy.test.ts`.

---

## 15. Rollout Plan
1. Schema + service + endpoints + admin (free baseline still 3; no user-visible change). Merge → migration. Create `ReplyAsHuman2026` via admin (validate e2e).
2. Free baseline 3→0 + consistency migration (`UsagePeriods` free rows; zero existing per D15).
3. User UI (redeem card + `/app` branching + Turnstile widget + copy + tests) — Playwright verify desktop/mobile.
4. Admin UI.
5. Worker-preview smoke: redeem→rewrite→paywall + invalid/expired/already/cap + Turnstile + disposable-email + IP forwarding; banned-term grep clean.
6. Deploy (push `main` → CI → `cf:deploy` + `dotnet ef database update`). Rollback = `wrangler rollback`; instant code disable via `IsActive=false`.

**Guardrails:** don't touch `LAUNCH_CONFIRMED`/Stripe price/webhook secrets/DNS; Stripe stays in configured mode; no real charges; keep Worker vars ↔ Functions app settings in sync.

---

## 16. Verification Plan
- **Backend (`dotnet-backend-testing`):** redeem happy/idempotent/concurrent; validity gates; global-cap exactness under load; consumption 3→0→paywall; new-user remaining=0; IP-velocity block/flag; Turnstile-fail rejection; disposable-email rejection; account-erase covers redemptions; admin CRUD + audit.
- **Frontend (`ui-browser-testing`):** new-user redeem card; success → workspace "3 trial · expires in N days"; invalid/expired/already errors; exhaustion → paywall; signup Turnstile present; admin page; desktop+mobile screenshots; copy contract tests green.
- **Gates:** `npm run test` + `dotnet test` green; banned-term grep clean; Worker-preview smoke (incl. Turnstile + IP forwarding).

---

## 17. Resolved Decisions (was "Open Questions")
All resolved 2026-06-02 — see §0. Code model = shared universal (D1); admin UI = build minimal (D8);
Turnstile on signup+redeem, keys verified, dev test keys (D9, §0.2); disposable-email block (D10);
IP velocity 5-hard/2-flag (D11); proxy shared secret (D12); login-first redeem (D13); TTL 90d (D4);
expiry Aug 31 NZ (D5); global cap optional (D6); paid users allowed (D14); existing users zeroed (D15);
stats KPIs (D16); `ADMIN_EMAILS` confirmed (D17). **No open product decisions remain.**

---

## 18. Implementation Checkpoints (Codex brief)
1. Entities + DbContext config + migration `_AddPromoCodes`.
2. `PromoService` (redeem/status) — serializable tx, atomic cap, idempotency catch, IP hash + velocity.
3. `PromoHttpFunctions` + admin promo endpoints (reuse `RequireAdminAsync` + audit).
4. `AccountService`: `promo` block + labels; free `QuotaLimit` 3→0 (config) + consistency migration; verify `ReserveAsync` quota source.
5. Signup path: Turnstile verify + disposable-email block.
6. Next.js proxies (Turnstile verify + IP/secret forward); `redeem-code-card` + `/app` branching; Turnstile widgets; admin UI.
7. Copy changes + update both contract tests.
8. Extend `DeleteAccountAsync` for redemptions.
9. Tests per §16; banned-term grep; Worker-preview smoke; deploy.

**Echo in every Codex brief:** banned terms `humanizer|bypass|undetect|detector|evade`; no secrets in source (validate env at runtime); no `migrate reset`/force-reset/drops; don't touch `LAUNCH_CONFIRMED`/Stripe price/webhook secrets/DNS; no real charges; keep Worker vars ↔ Functions app settings in sync.
