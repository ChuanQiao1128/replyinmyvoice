# AI Draft Cleanup A/B — readout protocol (LOCKED before results, 2026-05-27)

Locked before the V0–V4 numbers are in, so the call isn't skewed by erratic per-output Pangram.
n=22 is a smoke signal — **direction only, never a release decision.**

## Step 0 — V0 reproducibility gates the whole run
Before analyzing any variant, confirm V0 (= current engine, baseline) reproduces the trustworthy
baseline:
- semantic facts pass **21/22**; forbidden **0**; meaning_changed **0**; send_ready **22/22**
- Pangram output **median ≈ 96**; high-risk (≥90) **≈ 15/22**

If V0 diverges materially → **STOP, do not analyze variants**; investigate eval harness / model
drift / prompt path / case selection first. (Engine is stochastic; small wobble is fine, a big
shift is a red flag.)

## Step 1 — semantic hard gate (BEFORE any Pangram)
Each variant must clear, on the 22, via the semantic verifier:
- facts pass **≥ V0**
- forbidden violations **= 0**
- meaning_changed **= 0**
- send_ready **= 22/22**
- **no NEW severe fact loss** (even at 21/22, if the missing fact is material, it fails)

List per variant the missing / contradicted / unverifiable facts, classified as:
- **minor over-generalization** (e.g. "damaged" for "cracked base motor housing")
- **material fact loss** (amount / date / id / name dropped or changed)
- **new unsupported claim**

Any material fact loss or new unsupported claim → `decision = kill` (do **not** Pangram-score it).

## Step 2 — Pangram only on gate survivors (+ V0 for reference)
Record median, p75, p90, %≥90, %≥95, and per-case delta vs V0. **mean is reference only.**
Do **not** draw a conclusion from a single case scoring 4 or 99 — per-output Pangram is erratic.

## Step 2b — paired case-level delta (per surviving variant vs V0)
With n=22 + erratic per-output Pangram, the aggregate can be moved by a few cases. So for each
gate survivor, compare to V0 on the **same case**:

`case_id · V0 Pangram · variant Pangram · delta · V0 high-risk? · variant high-risk? · facts same? · notes`

Summary per variant:
- Pangram improved cases (delta meaningfully < 0) · worsened cases · unchanged cases
- high-risk (≥90) → non-high-risk count
- non-high-risk → high-risk count

**A variant is directionally good only if it improves on the MAJORITY of cases (paired)** — not if
a couple of large drops pull the mean/median down while most cases are unchanged or worse. Paired
delta is more reliable than the aggregate at this n.

## Step 3 — main comparison table
`variant · description · facts pass · new material fact losses · forbidden · meaning changed ·
send-ready · avg output words · compression · generic opener count · closer/signoff count ·
forced-skeleton count · avg Sapling calls · final Sapling median · Pangram median · p90 · %≥90 ·
%≥95 · decision`

`decision ∈ { kill, needs larger eval, candidate }`

## Decision logic
- facts/send-ready hold **and** template/signoff clearly down **and** Pangram %≥90 down **and**
  median/p90 down → **candidate** → expand to 50–100.
- only outliers move the mean but median/p90 static → **not success.**
- Pangram unmoved but template/signoff down + facts stable → keep as a **naturalness improvement**;
  do **not** claim it solves AI-detection risk.

## Three valuable outcomes (any is useful)
1. a single lever shows directional improvement (facts hold, high-risk down) → expand that lever.
2. combined improves but single levers unclear → interaction effect; decompose before shipping.
3. **no Pangram movement anywhere** → small prompt/router tweaks are not the lever; AI-detection
   risk is mostly the model's generation distribution, not Hi/Best or routing. Valuable: stop
   spending on these levers.

## Do NOT
No merge, no deploy, no prod-default change, no shipping a winning variant, no expanding the eval
set until the 22-case gate + Pangram summary are in, and never feed Pangram back into a per-email
rewrite loop.
