# Promo-Code Trial Specification (v4 — reviewed, sign-off-able)

> Replace the automatic "3 free rewrites on sign-up" with a **redeemable promo code**. Launch
> with **one shared universal code** (`ReplyAsHuman2026` = owner's test value; admin-settable),
> broadcast in advertising; build a commercial-grade, admin-managed, abuse-resistant,
> analytics-ready system underneath.

- **Status:** Design only — no source code written yet. Decisions **locked** + **owner-reviewed & sign-off-able** (2026-06-02). Launch quality is gated on the **5 checkpoints** in §18.
- **Branch:** `feat/promo-code-trial`
- **Author:** Claude Code (supervisor) — `system-spec-synthesis` (+ `state-machine-modeling`, `data-module-review`, `resilience-test-generation` lenses).
- **Changelog:** v1 design → v2 feasibility/threat-model/user-stories → v3 lock decisions + Turnstile verification → **v4 integrates implementation review: 4-phase rollout, fail-closed secrets, data CHECK constraints + DisplayCode, IP-velocity concurrency note (+ optional strict-count table), production-grade admin, Codex guardrails, 5 launch-gating checkpoints.**
- **Implementation:** delegated to Codex workers after go; the core call — *redemption = grant a `RewriteCredit{Source="PROMO"}`* (no new consumption path) — is the load-bearing low-risk choice.

---

## 0. Decisions Locked (owner-confirmed 2026-06-02)

| # | Topic | Decision |
|---|---|---|
| D1 | Code model | **One shared universal code string** (e.g. `ReplyAsHuman2026`), broadcast in ads; **each account redeems once** (`MaxRedemptionsPerUser=1`). NOT per-user-unique. *(Schema supports unique-per-user codes later — `MaxRedemptionsGlobal=1` each — no schema change needed then.)* |
| D2 | Free baseline | New users get **0** automatic rewrites (`GetUsagePlan` free quota `3 → 0`). |
| D3 | Grant = credit | Valid redemption inserts `RewriteCredit{Source="PROMO", AmountGranted=3, ExpiresAt=redeemedAt+90d}` — reuses existing consumption/paywall path unchanged. |
| D4 | Credit TTL | **90 days** from redemption. |
| D5 | Code expiry | `ValidUntil = 2026-08-31 23:59:59 NZ` (`+12:00` = `2026-08-31T11:59:59Z`). Two independent clocks. |
| D6 | Global cap | **Optional** per code (`MaxRedemptionsGlobal` nullable); Nth+1 → "code reached its limit". |
| D7 | Per-user cap | `MaxRedemptionsPerUser = 1`. |
| D8 | Admin console | **Minimal `/admin/promo-codes` UI** (create/list/disable/stats), promo-only scope. Built as a **production feature**, not a throwaway tool. |
| D9 | Human verification | **Cloudflare Turnstile on signup + redeem**, verified at the proxy. Keys verified (§0.2); dev uses CF test keys. |
| D10 | Disposable-email block | **Block temp-mail domains at signup** via bundled public blocklist (server-side). |
| D11 | IP abuse defense | Hash client IP at redeem; **hard-block ≥5/IP/24h, flag (log-only) from the 2nd**; configurable; DB-backed in C#. |
| D12 | Proxy→Functions IP trust | New **`PROMO_PROXY_SHARED_SECRET`**; C# trusts forwarded IP only with it. **Fail-closed** (§8.1). |
| D13 | Redeem flow | **Login first → redeem in `/app`**. |
| D14 | Paid users | **Allowed** to redeem (overflow). |
| D15 | Existing users | **Zero** the free baseline for everyone (≈no live users). |
| D16 | Stats KPIs | redemptions · distinct users · **activation rate (used ≥1)** · daily curve · IP-cluster flags. |
| D17 | Admin allowlist | `ADMIN_EMAILS` includes `chuanqiao1128@gmail.com` (confirmed). |

### 0.2 Turnstile key verification (tested 2026-06-02)
| Key | Result |
|---|---|
| Site `0x4AAAAAADdY3Xy1e6vEJU8E` (public) | ✅ valid; Playwright render gave `110200` *domain-not-allowed* (real, recognized key) → **domain-locked to `replyinmyvoice.com`**, localhost not allowed. |
| `TURNSTILE_SECRET_KEY` (secret, `.env.local`) | ✅ valid; `siteverify` probe → `invalid-input-response` (secret accepted, dummy token rejected). Value never printed. |
| Dev/local | Use CF official **test keys**: site `1x00000000000000000000AA` / secret `1x0000000000000000000000000000000AA`. Real keys prod-only. |

### 0.3 Review-driven refinements (v4) — what the owner review changed
1. **4-phase rollout** (§15): backend+redeem first (baseline stays 3) → concurrency/security tests → baseline 3→0 as its own checkpoint → UI last. So a UI bug can't destabilize verified backend.
2. **Free-baseline cutover** gets an explicit 5-point consistency checkpoint (§16.1) — the top regression risk.
3. **Global cap** = DB **atomic conditional update** only; never app-layer check-then-write (§6).
4. **IP velocity** is documented as an **approximate** defense (check-then-insert has a concurrency window); optional **`PromoIpVelocityBucket`** atomic-count table for strict enforcement (§6, §8.2).
5. **`PROMO_PROXY_SHARED_SECRET` and Turnstile secret are fail-closed**, not silent-degrade (§8.1, §8.3) — a missing secret must break the feature loudly, not quietly disable defenses.
6. **Data CHECK constraints** + `Code` (normalized) / `DisplayCode` (original) split (§6).
7. **Admin UI is production-grade** — all auth/empty/error/duplicate/disable states + audit every mutation (§7).
8. **Codex implementation guardrails** ("do-not" list) + the **5 launch-gating checkpoints** (§18).

---

## 0.5 Owner Setup & Prerequisites — your plate vs. mine

**Done ✅:** Turnstile widget; Site Key; `TURNSTILE_SECRET_KEY` in `.env.local` (verified); `ADMIN_EMAILS`.
**Owner's remaining manual tasks before dev:** *none.* Prod secrets set at deploy via existing `wrangler`/`az` tokens.

| Var | Type | By | Lives in |
|---|---|---|---|
| `NEXT_PUBLIC_TURNSTILE_SITE_KEY` = `0x4AAAAAADdY3Xy1e6vEJU8E` | public | ✅ owner | `.env.local` + Worker vars (prod); dev = CF test site key |
| `TURNSTILE_SECRET_KEY` | secret | ✅ owner | `.env.local` + Worker secret (prod); dev = CF test secret |
| `PROMO_PROXY_SHARED_SECRET` | secret | **I generate** | Worker secret + Functions app setting |
| `PROMO_IP_HASH_SALT` | secret | **I generate** | Functions app setting |
| `PROMO_IP_VELOCITY_MAX_24H` = `5` / `PROMO_IP_VELOCITY_FLAG_FROM` = `2` | config | default | Functions app setting |
| `FREE_BASELINE_REWRITES` = `0` | config | default | Functions app setting |
| `ADMIN_EMAILS`, `NEXT_PUBLIC_AZURE_API_BASE_URL` | config | ✅ exist | — |

---

## 1. Feasibility (verified 2026-06-02)
No blocker. Grant + consumption machinery exists; net-new = promo layer, admin UI (none today — admin is API-only), durable abuse defense. Admin auth (`ADMIN_EMAILS`), audit (`AdminAuditLog`), serializable-tx (`StripeEventService.ExecuteInTransactionAsync`), and the credit-grant shape (`AdminService.GrantCreditsAsync`) all already exist to mirror.

## 2. TL;DR
Prod already grants/consumes `RewriteCredit` (PURCHASE/ADMIN); `/api/me` sums credits into `remaining` with per-source `ExpiresInDays`. So: (1) free baseline `3→0` + consistency migration; (2) add promo layer (2 tables, redeem service, endpoints, admin UI, redeem card, copy, Turnstile, disposable-email, IP defense); (3) **redemption = grant a `PROMO` credit** → consumption/paywall/402/quota-race/Stripe unchanged.

## 3. Requirements
Per-code: credits, `GrantTtlDays` (90), `ValidFrom`/`ValidUntil` (NZ), `MaxRedemptionsPerUser` (1), `MaxRedemptionsGlobal` (nullable), `IsActive` (immediate kill). Code model = one shared universal (D1). Two clocks (D5). Goal = select for genuine intent (limits in §8.5).

## 4. Current System (grounded)
Prod = C#/.NET Azure Functions + Azure SQL (EF Core) behind Cloudflare Worker (OpenNext Next.js). TS `lib/` + `lib/generated/prisma/**` dead.
- Free "3" = `AccountService.GetUsagePlan()` → `("free","free:lifetime",3)` — a `UsagePeriod.QuotaLimit`, **not** a credit. **`GetOrCreateAccountSummaryAsync` computes `periodRemaining` from the code constant, not the persisted row** — central to the §16.1 checkpoint.
- `/api/me` → `remaining = periodRemaining + creditRemaining`; `Sources[]` per credit; `Exhausted=remaining<=0`.
- Consumption: `QuotaService.ReserveAsync` + `FindUsableCreditAsync` (period first, then credits soonest-expiring-first). Unchanged.
- Admin: `AdminAccess.RequireAdminAsync` (`ADMIN_EMAILS`); `AdminHttpFunctions` API-only, no `app/` page.
- Rate-limit precedent `lib/auth-rate-limit.ts`: per-IP/email buckets, `clientIpFromRequest` reads `x-forwarded-for`/`cf-connecting-ip` — **in-memory per-isolate, ephemeral**.
- Proxy `app/api/me/route.ts` forwards **only** the bearer — no secret, no IP. Identity = Entra `oid` → `AppUser.ExternalAuthUserId`. Migrations auto-apply on merge to `main`.

## 5. Architecture
```
signup (Turnstile + disposable-email check) + verify email → AppUser{Inactive}
GET /api/me → free quota 0; promo.hasRedeemed=false → /app "Redeem your code" card
POST /api/promo/redeem {code, turnstileToken}
  proxy: same-origin → verify Turnstile(secret, cf-ip) → forward {bearer, X-Client-IP, X-Proxy-Secret}
  C# RedeemAsync: normalize → IP velocity(DB) → Serializable tx:
     atomic ++RedemptionCount(cap guard) → RewriteCredit{PROMO,3,+90d} → PromoCodeRedemption{unique(codeId,userId), RedeemIpHash}
GET /api/me → remaining=3 → /app workspace → POST /api/rewrite consumes PROMO credit (UNCHANGED)
exhausted → /app buy-paywall (promo.hasRedeemed=true)
```

## 6. Data Model (`data-module-review`)
EF conventions: `Guid Id`, `Guid RowVersion` token, `DateTimeOffset`, string enums, indexes in `OnModelCreating`. Migration `_AddPromoCodes`; no reset/force-reset/drops.

### `PromoCode`
```csharp
public sealed class PromoCode {
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }            // NORMALIZED (trim+UPPER+strip space/dash) — UNIQUE; used for matching
    public string? DisplayCode { get; set; }             // original as the admin typed it — for display only
    public string? Description { get; set; }
    public PromoCodeKind Kind { get; set; } = PromoCodeKind.TrialCredits;
    public int CreditsGranted { get; set; }              // 3
    public int GrantTtlDays { get; set; } = 90;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public int? MaxRedemptionsGlobal { get; set; }       // null = uncapped
    public int MaxRedemptionsPerUser { get; set; } = 1;
    public int RedemptionCount { get; set; }             // denormalized; cap guard + fast admin list
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid RowVersion { get; set; } = Guid.NewGuid();
    public ICollection<PromoCodeRedemption> Redemptions { get; } = new List<PromoCodeRedemption>();
}
public enum PromoCodeKind { TrialCredits }
```
Indexes/config: `HasIndex(Code).IsUnique()`; `Code`/`DisplayCode` max 40; `Description` max 200; `Kind` conv-string 40; `RowVersion` token.

**DB CHECK constraints** (defense-in-depth at the schema level; enforce in migration via `HasCheckConstraint` / raw SQL):
```text
CreditsGranted > 0
GrantTtlDays   > 0
MaxRedemptionsPerUser >= 1
MaxRedemptionsGlobal IS NULL OR MaxRedemptionsGlobal > 0
ValidUntil > ValidFrom
RedemptionCount >= 0
```
(Service-layer validation also rejects these with friendly messages; the CHECK is the last line.)

### `PromoCodeRedemption` (analytics source of truth)
```csharp
public sealed class PromoCodeRedemption {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PromoCodeId { get; set; }  public PromoCode? PromoCode { get; set; }
    public Guid UserId { get; set; }       public AppUser? User { get; set; }
    public Guid RewriteCreditId { get; set; }
    public int CreditsGranted { get; set; }
    public string CodeSnapshot { get; set; } = "";       // normalized code as redeemed (audit)
    public string? RedeemIpHash { get; set; }            // salted SHA-256(IP); NEVER raw IP
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

### Global-cap correctness (D6 — atomic, never check-then-write)
Inside the redeem tx, single atomic statement (race-proof even at READ COMMITTED):
```sql
UPDATE PromoCodes SET RedemptionCount=RedemptionCount+1, UpdatedAt=@now, RowVersion=@new
WHERE Id=@id AND IsActive=1 AND @now BETWEEN ValidFrom AND ValidUntil
  AND (MaxRedemptionsGlobal IS NULL OR RedemptionCount < MaxRedemptionsGlobal);
-- rows affected == 0 ⇒ disabled/outside-window/cap-reached → re-read to choose message
```

### Optional `PromoIpVelocityBucket` (strict IP counting — see §8.2)
The default IP velocity check is **approximate** (check-then-insert window). If strict enforcement is wanted, add an atomic-count table keyed by `(IpHash, WindowStart)`:
```csharp
public sealed class PromoIpVelocityBucket {
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string IpHash { get; set; }          // salted
    public DateTimeOffset WindowStart { get; set; }      // e.g. start of the rolling/hourly bucket
    public int Count { get; set; }
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
// HasIndex(new {IpHash, WindowStart}).IsUnique(); atomic UPSERT ++Count, reject when over threshold.
```
**Launch recommendation:** ship the approximate check (cheap, good enough given Turnstile + per-user + global cap); add the bucket only if data shows IP farming slipping the window.

### Account deletion & analytics
Extend `AccountService.DeleteAccountAsync` to null `RedeemIpHash` (keep the row → totals stay accurate). Activation query joins `RewriteCreditId → RewriteCredit.AmountConsumed` (redeemers who used ≥1 = genuine-need signal).

---

## 7. Admin Console (D8 — production feature, not a throwaway tool)
Auth: `AdminAccess.RequireAdminAsync` (`ADMIN_EMAILS`). **Audit every mutation** (`AdminAuditLog` with actor `oid`+email, `promoCodeId`, action, and a changed-fields summary) for create/edit/disable/enable.

Endpoints (extend `AdminService` + new `PromoAdminHttpFunctions`): `POST /api/admin/promo-codes` (create; validate unique normalized code, CHECK-mirrored numeric rules, `validUntil>validFrom`), `GET /api/admin/promo-codes` (+`redemptionCount`, derived status), `GET /api/admin/promo-codes/{id}` (stats), `PATCH …/{id}` (edit validUntil/caps/description), `POST …/{id}/disable|enable`.

**UI `app/admin/promo-codes/page.tsx` must handle production states, not happy-path only:**
```text
non-admin            → clear 403 / no-permission view
not signed in        → redirect to /sign-in?redirectTo=/admin/promo-codes
list                 → loading / error / empty states
create duplicate code→ explicit field error (not a 500)
create invalid nums  → field-level validation messages
disable              → reflects immediately; redeem then fails at once
stats                → show IP-hash CLUSTERS only — NEVER raw IPs
```

---

## 8. Security — Threat Model & Defense-in-Depth

### 8.1 IP capture + proxy trust (fail-closed)
- Capture `cf-connecting-ip` at the Worker proxy; forward to C# with `X-Client-IP` + `X-RIMV-Proxy-Secret`.
- **Fail-closed (D12):** C# trusts a forwarded IP **only** when `PROMO_PROXY_SHARED_SECRET` matches. **If the secret is unset/mismatched in production, do NOT silently skip IP defense** — treat it as a misconfiguration: the redeem endpoint should **fail closed** (reject with a server-config error) rather than quietly disabling abuse protection. A missing secret must be loud, not invisible. *(Pre-launch hard gate.)*
- Store only salted SHA-256(IP) (`PROMO_IP_HASH_SALT`) in `RedeemIpHash`; raw IP never stored or logged.

### 8.2 Velocity (D11 — durable, in C#; approximate by default)
- Redeem path counts `redemptions WHERE RedeemIpHash=@h AND RedeemedAt>now-24h`; **hard-block ≥5, flag (log, don't block) ≥2**; configurable.
- **Known concurrency limit:** this is check-then-insert, so simultaneous requests from one IP can each read `count<5` before any insert — an **approximate** cap, not a strict limiter. Acceptable at launch because Turnstile + per-user unique index + global cap already bound abuse. For strict enforcement, use the optional `PromoIpVelocityBucket` atomic UPSERT (§6). The in-memory `lib/auth-rate-limit.ts` is only a cheap first layer.
- Rationale for 5/flag-2 (not 1): shared IPs / mobile **CGNAT** (common in NZ) make a hard `1` block legitimate users; a hard `1` also doesn't stop IP-rotating attackers.

### 8.3 Human + email defenses (D9, D10 — fail-closed)
- **Turnstile on signup + redeem**, verified at the proxy (`siteverify` + client IP). **Env-strict & fail-closed:** dev defaults to CF test keys; **prod requires real `NEXT_PUBLIC_TURNSTILE_SITE_KEY` + `TURNSTILE_SECRET_KEY`** — if the secret is missing/invalid in prod, signup/redeem **fail closed** (do not bypass the check). Never use the prod (domain-locked) key on localhost/preview (would always error 110200).
- **Disposable-email block at signup:** bundled public disposable-domain blocklist (~thousands), server-side, periodic refresh.

### 8.4 Threats (summary)
T1 multi-account farm → email-verify + disposable-block + Turnstile + IP velocity + global cap + activation monitoring (bounded by cap). T2 brute force → generic `invalid_code` + limit + Turnstile. T3 leak → cap + ValidUntil + instant disable; per-user codes as escalation. T4 bots → Turnstile both. T5 race/replay → idempotent + unique index + atomic cap + Serializable. T6 delete+re-signup → retain `RedeemIpHash`. T7 XFF spoof → proxy-secret fail-closed. T8 admin abuse → allowlist + audit.

### 8.5 Honest limits & upgrade path
Universal code will leak; free-email signups enable bounded multi-account farming. Real escalation = per-user unique codes (`MaxRedemptionsGlobal=1`), same schema. Launch universal, watch redemption/activation, escalate if abused.

### 8.6 Hygiene
No banned terms in identifiers/copy (`ReplyAsHuman2026` is grep-clean). No secrets in source; code value in DB; never log code values, secrets, Turnstile tokens, or raw IPs.

---

## 9. State Machines
**`PromoCode`** (derived): `[Pending]→[Active]→{[Expired] | [Exhausted] | [Disabled]⇄[Active]}`. Redeemable iff `IsActive ∧ ValidFrom≤now≤ValidUntil ∧ (MaxGlobal==null ∨ RedemptionCount<MaxGlobal)`. Invariant `RedemptionCount==COUNT(Applied)`.
**`PromoCodeRedemption`** (per user×code): `(none)→[Applied]→[Reversed]`; second Applied blocked by unique index; every Applied references a real credit created in the same tx.

## 10. Redeem Algorithm (idempotent, race-safe, IP-aware)
```
[proxy] same-origin → verify Turnstile(token, secret, cf-ip); fail → 403 invalid_captcha
        forward Authorization + X-Client-IP(cf-connecting-ip) + X-RIMV-Proxy-Secret
[C#] RedeemAsync(extUserId, email, rawCode, trustedIp?, now):
  if PROD and proxy-secret missing/mismatch → fail closed (server_config error)   // §8.1
  code=Normalize(rawCode); if !FormatValid → InvalidCode
  user=GetOrCreateUser(); ipHash = trustedIp? HashIp : null
  if ipHash && Recent(ipHash, 24h) >= MAX(5) → IpVelocityBlocked         // flag from 2
  ExecuteInTransactionAsync(Serializable):
    pc=PromoCodes.Single(Code==code); if pc==null||!pc.IsActive → InvalidCode
    if now<pc.ValidFrom → InvalidCode; if now>pc.ValidUntil → Expired
    if Redemptions.Any(pc.Id,user.Id) → AlreadyRedeemed                  // idempotent, no new grant
    affected = ExecuteUpdate(++RedemptionCount WHERE cap predicate)      // §6 atomic
    if affected==0 → CapReachedOrInactive
    Add RewriteCredit{PROMO, pc.CreditsGranted, now, now+pc.GrantTtlDays}; Add Redemption{...}
    try SaveChanges/commit; catch unique(codeId,userId) → rollback; AlreadyRedeemed
  return Success(amount, expiresAt)
```

## 11. User Stories
**Admin:** create / disable(immediate) / stats(activation) / edit / duplicate-rejected / list-with-status. **User:** new→redeem card / redeem→+3 / redeem-twice→already / expired / invalid / cap-reached(1001st) / TTL(day91 expired→paywall) / paid-user-allowed / signed-out→sign-in / mobile / **signup-Turnstile(bot→blocked)** / **signup-disposable-email→rejected**. **Abuser:** multi-account(bounded) / brute(limited) / race(no double grant).

## 12. Risk Register (top hotspots)
free-baseline mismatch (Med/Med → §16.1 checkpoint) · global-cap over-issue (Low/Med → atomic) · IP spoofable/unobtained (Med/High → proxy-secret fail-closed) · IP-velocity window (Low/Med → approximate; bucket if needed) · multi-account farm (Med/Med → layered) · copy breaks contract tests (→ update both) · prod Turnstile key on localhost (→ dev test keys, fail-closed).

## 13. API Contracts
**`POST /api/promo/redeem`** `{code, turnstileToken}` → `200 {creditsGranted, totalRemaining, expiresAt, alreadyRedeemed}`; errors `401 / 400 / 403 invalid_captcha / 422 invalid_code / 422 code_expired / 409 already_redeemed / 409 code_exhausted / 429 ip_velocity / 500 server_config|server_error`. **Enumeration-resistant:** redeem returns generic `invalid_code` for not-found/inactive/not-yet-valid; **only the admin page shows precise status.**
**`/api/me`** adds `"promo": { hasRedeemed, eligible, trialRemaining, trialExpiresAt }` → drives `/app`: no-redeem→card · has-credit→workspace · used-up→paywall · paid→redeemable without interrupting paid flow.
**Signup** (Entra-native): add Turnstile verify + disposable-domain rejection server-side before account creation.

## 14. Frontend & Backend Change List
**Backend:** PromoCode/PromoCodeRedemption (+optional bucket) entities; DbContext + migration `_AddPromoCodes` (+ CHECK constraints); `PromoService` (redeem/status, HashIp, velocity, atomic cap); `AccountService` (free 3→0 config + `promo` block + `PROMO`→"Trial rewrites" label + data migration + extend DeleteAccount); admin promo methods + `PromoAdminHttpFunctions` (audit); `PromoHttpFunctions`; signup Turnstile + disposable-email check; §0.5 env.
**Frontend:** proxies `app/api/promo/redeem` (+status) with Turnstile verify + IP/secret forward; `redeem-code-card` + `/app` branching; Turnstile widgets on signup + redeem; `app/admin/promo-codes/page.tsx`; copy (hero/closing-cta/pricing-v2/pricing page/footer/developers/auth/app quota label); update `tests/unit/pricing-auth-visual-system.test.ts` + `workspace-copy.test.ts`.

---

## 15. Rollout Plan (4 phases — review-driven order)

**Phase 1 — Backend schema + redeem/admin APIs (no UX change).** Add tables, migration (+CHECK), `PromoService`, admin endpoints, `POST /api/promo/redeem`, `/api/me` `promo` block. **Free baseline stays 3.** Create a test code, verify the PROMO credit lands in the existing consumption path. Merge → migration applies.

**Phase 2 — Concurrency & security tests** (must pass before any UX change):
```text
same user double-click redeem → exactly one credit
global cap=1, N concurrent users → exactly one success
expired code / disabled code → no credit granted
missing Turnstile token → proxy rejects (invalid_captcha)
missing/mismatched proxy secret → C# fails closed (no untrusted IP)
same IP, many accounts → blocked at 5 (flagged from 2)
```

**Phase 3 — Free baseline 3→0 (its own PR/checkpoint).** Change `GetUsagePlan` (config `FREE_BASELINE_REWRITES`), run the consistency migration (`UsagePeriods` free rows → 0, per D15), verify `/api/me` and `ReserveAsync` agree (§16.1).

**Phase 4 — User UI + admin UI + copy.** `redeem-code-card` + `/app` branching, Turnstile widgets (signup + redeem), `app/admin/promo-codes` page, copy changes + contract tests. Playwright desktop/mobile.

**Then:** Worker-preview smoke of the full loop → deploy (push `main` → CI → `cf:deploy` + `dotnet ef database update`). Rollback = `wrangler rollback`; instant code disable via `IsActive=false`. **Guardrails:** don't touch `LAUNCH_CONFIRMED`/Stripe/DNS; no real charges; keep Worker vars ↔ Functions settings in sync.

---

## 16. Verification Plan

### 16.1 Free-baseline cutover checkpoint (top regression risk — Phase 3 gate)
First confirm in code **which value `ReserveAsync` trusts** (the `GetUsagePlan` constant vs. the persisted `UsagePeriod.QuotaLimit`); if it trusts the persisted row, the data migration is the source of truth. Then assert:
```text
new user            /api/me remaining = 0
existing free user  /api/me remaining = 0   (after migration)
ReserveAsync        free period no longer allows 3
PROMO credit present → remaining = promoRemaining
PROMO used up        → paywall appears correctly
```

### 16.2 Backend (`dotnet-backend-testing`)
redeem happy/idempotent/concurrent; validity gates + `ValidUntil` boundary; **global-cap exactness under parallel load**; consumption 3→0→paywall; IP velocity block(5)/flag(2); Turnstile-fail rejection; **proxy-secret-missing → fail closed**; disposable-email rejection; account-erase covers redemptions; admin CRUD + audit + duplicate-code error.

### 16.3 Frontend (`ui-browser-testing`)
new-user redeem card; success → "3 trial · expires in N days"; invalid/expired/already errors; exhaustion → paywall; signup Turnstile present; admin page states (403/empty/error/duplicate/disable-immediate/no-raw-IP); desktop+mobile screenshots; copy contract tests green.

### 16.4 Gates
`npm run test` + `dotnet test` green; banned-term grep clean; Worker-preview smoke incl. Turnstile + IP forwarding.

---

## 17. Resolved Decisions
All resolved 2026-06-02 (see §0). No open product decisions remain.

---

## 18. Implementation Checkpoints & Codex Guardrails

### Checkpoints (sequenced per §15)
1. Entities (+optional bucket) + DbContext config + migration `_AddPromoCodes` (+ CHECK constraints).
2. `PromoService` — serializable tx, **atomic** cap, idempotency catch, IP hash + velocity, **proxy-secret fail-closed**.
3. `PromoHttpFunctions` + admin promo endpoints (reuse `RequireAdminAsync`, audit every mutation).
4. `AccountService` `promo` block + labels; **(Phase 3)** free `QuotaLimit` 3→0 + consistency migration; verify §16.1.
5. Signup: Turnstile verify + disposable-email block (locate exact Entra-native insertion point at build).
6. Next.js proxies (Turnstile verify + IP/secret forward); `redeem-code-card` + `/app` branching; Turnstile widgets; admin UI (production states).
7. Copy + update both contract tests. 8. Extend `DeleteAccountAsync`. 9. Tests per §16; grep; preview smoke; deploy.

### 5 launch-gating checkpoints (must be test-locked before prod)
```text
1. free quota cutover  (display == /api/me == ReserveAsync == DB; §16.1)
2. global cap race      (atomic; exactly N under load)
3. proxy trusted IP     (fail-closed when secret missing/mismatched)
4. Turnstile env        (dev test keys; prod fail-closed on missing secret)
5. admin auth / audit   (403 for non-admin; every mutation audited)
```

### Codex "do-not" guardrails (echo in every brief)
```text
DO NOT add a separate trial-consumption path (redemption = grant a RewriteCredit{PROMO}).
DO NOT check the global cap then insert in app code — use the atomic conditional UPDATE.
DO NOT store raw IP — only salted hashes.
DO NOT log code values, secrets, Turnstile tokens, or raw IPs.
DO NOT let a missing/mismatched proxy secret or Turnstile secret silently pass — fail closed.
DO NOT use migrate reset / force-reset / drop tables.
DO NOT change Stripe, DNS, or LAUNCH_CONFIRMED; no real charges.
DO NOT use banned terms: humanizer | bypass | undetect | detector | evade.
Also: no secrets in source (validate env at runtime); keep Worker vars ↔ Functions app settings in sync.
```
