# Translation round-trip RESEARCH pilot (Youdao NMT) — plan

**Status:** DRAFT, awaiting the 108-case v3 faithfulness readout before implement+run (owner 2026-05-27).
**Nature:** research only. NOT wired into the rewrite engine, NOT a production path, NOT a large run.

## Core strategy decision (owner Q 2026-05-27: translate the rewrite, or translate the input?)
**Translate the REWRITE, not the raw input (= "option 1").** Principle: the fact-preserving rewrite
(extract facts → DeepSeek writes) is the **load-bearing** step and defines the fact ground truth; the
Youdao round-trip is a **cosmetic** perturbation that goes LAST and is optional + reversible.
- Load-bearing first (lock facts in an English rewrite), cosmetic last (translate to perturb).
- **Fact gate runs on the FINAL (post-translation) text**; if the round-trip drifts a fact, **fall back to
  the untranslated rewrite** → the result is never worse than baseline.
- For the (deferred) detection hypothesis to even have a chance, the FINAL text must be NMT output
  (else it keeps the LLM fingerprint). Option 1's final text = `NMT(English rewrite)` → satisfies that
  AND preserves facts.
- Translating the raw input first ("option 2") is **only a cheap probe**, not a strategy: it gambles
  facts on Youdao before we lock them and doesn't strip AI filler.

## Iron rules (both options)
1. The fact **ledger is always extracted from the ORIGINAL English draft** — translation never touches the ledger.
2. The semantic **fact gate always runs on the FINAL output**.
3. If translation causes fact drift / meaning hardening → **fall back to the untranslated rewrite**.

## Owner constraints (2026-05-27)
- Provider locked: **Youdao** (有道). No Azure / DeepL.
- **Round 1 = SEMANTIC ONLY.** Answer first: does the round-trip cause **fact drift / meaning hardening /
  amount-date-promise changes**. **No Pangram** in round 1.
- Pangram/GPTZero stay **paused**; detection scoring is a separate, owner-gated round 2.
- Budget caps: `YOUDAO_MAX_CALLS=40`, `PANGRAM_MAX_CALLS=0` (Pangram off).
- Secrets read from `.env.local`, **never printed/committed/logged**. Rotate if a real value leaked.

## Secrets / env (compat read; present names in **bold**)
- Key:    `YOUDAO_APP_KEY` || **`AppID`** || `YouDao_API_KEY`
- Secret: `YOUDAO_APP_SECRET` || **`AppSecret`**
- URL:    `YOUDAO_API_URL` (default `https://openapi.youdao.com/api`)

## Youdao text-translation API (signType v3)
- `POST https://openapi.youdao.com/api` (form-encoded, UTF-8, JSON response).
- Params: `q, from, to, appKey, salt, sign, signType=v3, curtime`.
- `sign = sha256(appKey + INPUT + salt + curtime + appSecret)` (hex), where **INPUT truncation**:
  `len(q)<=20 → INPUT=q`; else `INPUT = q[:10] + str(len(q)) + q[-10:]` (len = #characters; classic gotcha).
- Codes: English `en`, Simplified Chinese `zh-CHS`. EN→CN `from=en to=zh-CHS`; CN→EN `from=zh-CHS to=en`.
- Success `errorCode=="0"`; text in `translation` (array → join).
- **Single-query max 5000 chars** → guard at **4500**: segment on paragraph then sentence; never hard-send.

## Variants (revised — option 1 primary)
| variant | pipeline | LLM? | Youdao calls |
|---|---|---|---|
| **T0** (control / floor) | English facts-first rewrite = current **v3 engine output** (read from 108-run v3 JSON); NO translation | yes (EN) | 0 |
| **TA** (option 1, **PRIMARY**) | **T0 rewrite → Youdao en→zh-CHS→en**; gate on FINAL; **fall back to T0 if facts drift** | yes + NMT | 2 |
| **T1** (option 2, naive probe) | raw draft → Youdao en→zh-CHS→en (no rewrite) | no | 2 |
| **TB** (optional) | DeepSeek concise **Chinese** facts-first (from original ledger) → Youdao zh-CHS→en | yes (CN) + NMT | 1 |

- TB's Chinese step = concise + facts-first, NOT "polish the Chinese to sound fancier".
- **No T4** (translate → LLM-polish → ...) — re-adds the LLM fingerprint; excluded.
- The owner's question is answered by **TA (translate the rewrite) vs T1 (translate the input)**, with T0 as the no-translation floor. TB only if we want "rewrite-in-Chinese" as an extra comparison.

## Budget math
Core **T0 + TA + T1 = 4 Youdao/case**. **8 cases → 32 calls** ≤ 40 (headroom for segmentation).
Adding TB → 5/case → 8 cases = 40 (at cap). Default: **drop TB, run T0/TA/T1 on 8 cases** unless owner
wants TB (then raise the cap or cut to ~6 cases). DeepSeek (judge + TB/T3-style rewrites) ≈ 40–60 cheap
calls (~NZ$0.5–1). Pangram = **0**.

## Case selection (8; FINALIZE after the 108-readout)
Cover the spread + **prioritize the cases where v3 actually lost facts in the 108-run** (most informative):
short scheduling, refund/billing, support, sales, long high-fact-density, the ~99-stuck ones, heavy filler.
Provisional: `aidc-201` (short filler), `aidc-202` (refund-timing canary "should appear in 3-5 days"),
`aidc-203` ($120.00), `rewrite-draft-045` (sales, ~99), `rewrite-draft-061` (support, ~99),
`rewrite-draft-080` (~99), `rewrite-draft-100` (long, 11 must_keep), + 1 swap-in from the readout's
real-fact-loss list.

## Round-1 gate + deliverable (semantic only; NO Pangram)
Every variant output → DeepSeek semantic verifier (same judge as `ab_analyze_v3_100.py`):
`facts / forbidden / meaning_changed / send_ready`, missing/contradicted facts classified material vs minor,
plus a focused **fact-drift / meaning-hardening / promise-change** column (e.g. "should see → will receive",
amount/date altered). Facts-fail → excluded from any later detection scoring. For TA, also record whether it
fell back to T0.
Direct answer produced: **does TA (rewrite→translate) keep facts ≈ T0 while T1 (translate input) loses them?**

## Round-2 (owner-gated, only if round-1 facts are stable)
Owner decides whether to spend **10–20 Pangram calls** on gate survivors only, one shot per output, paired
delta vs T0 — never fed back into rewriting, never a per-email target.

## Success criteria (to justify any further detection work)
1. TA facts pass **≈ T0** (round-trip introduces no new material loss; fallback keeps it ≥ T0)
2. forbidden = 0, meaning_changed = 0
3. send-ready not below T0
4. **no promise-hardening** (conditional/uncertain language preserved)

If TA can't hold facts vs T0, or T1 shows translation alone destroys facts → **stop the translation track**
before spending anything on detection.

## Honest prior
May help on short / heavy-filler / simple emails; likely **fails** on long / structured / quote / refund /
billing / support-policy / high-fact-density (translationese + still-high detector scores). Biggest risk is
**semantic hardening + fact blur**, not cost — hence semantic gate FIRST, detection never first.

## Run result — Round-1 + owner-gated Round-2 (2026-05-27, 10 cases, Pangram ON)

Owner authorized Pangram (round-2) live this session, so round-1 (semantic) and round-2 (Pangram) ran together.
T0 (production baseline = engine **v0**, internal Sapling gate, no translation) vs TA (mask → Youdao en→zh-CHS→en →
unmask → semantic gate → fallback to T0) on the Manus-spec 10 cases (001/002/003/004/008/011/021/029/036/041).
Eval-only code: `backend-dotnet/tools/ReplyInMyVoice.Eval/TranslationPilot.cs` (`TA_PILOT=1`), never wired to prod.
Report: `docs/rewrite-eval-results/20260527-033703-ta-translation-pilot.{md,json}`.

Rerun:
`TA_PILOT=1 WRITING_SIGNAL_PROVIDER=sapling PANGRAM_MAX_CALLS=24 YOUDAO_MAX_CALLS=40 EVAL_MODE=smoke EVAL_CASE_IDS="rewrite-draft-001,rewrite-draft-002,rewrite-draft-003,rewrite-draft-004,rewrite-draft-008,rewrite-draft-011,rewrite-draft-021,rewrite-draft-029,rewrite-draft-036,rewrite-draft-041" dotnet run --project backend-dotnet/tools/ReplyInMyVoice.Eval`

Numbers:
- T0 output 10/10; sentinel survival **8/10** (021, 029 broke → fallback); TA gate-pass **7/10**; fallback 3 (2 sentinel, 1 fact-drift on a record id that T0 didn't reproduce verbatim so masking missed it).
- **Pangram: of 7 gate-passing TA, 6 dropped, 1 unchanged, 0 rose. Mean Δ −76, median −88 (T0 ~86–99 → TA ~0–14).**
- Calls: Youdao 20, Pangram 17, DeepSeek 28. Spend ~cents.

Verdict — **detection drops hugely, but it is NOT a quality win; do NOT advance TA to product:**
1. The drop is the NMT laundering effect — Pangram keys on the LLM surface fingerprint, which translation scrambles. Case 002 scored Pangram **0** while carrying meaning errors ("saucer"→"tea tray", "create the goods"); case 036 stayed **99** while its phone/time/date line was scrambled into nonsense. The score is decoupled from writing quality → this confirms detector unreliability, not TA superiority.
2. **Gate-pass ≠ send-ready.** The semantic judge passes when masked fact-tokens are present but is lenient on translationese, non-anchor noun drift (jar lid→bottle cap, dish rack→plate rack), and destroyed sentence structure (036). Every passing TA reads like machine translation ("The best" sign-off, awkward prepositions).
3. Against the Manus §13 bar: Pangram-win ✓ (6/7), sentinel ✓ (8/10, at threshold), but gate-failure ✗ (3 > 2) and human-readability ✗ (obvious translationese). Net: fails the quality bar.

Decision: **stop the translation track** (matches the 2026-05-27 halt on detection-chasing). If any lever is worth more work it is identifier-retention in the English rewrite (the 001 record-id and 021/029 id/sentinel drift), NOT translation. TB/T1 not run — TA already answers the question.

## Round-2 result — T3 native-repair (2026-05-27, same 10 cases, Pangram ON)

Design (owner's round-2 doc): stop letting Youdao be the final author. TA_raw = perturbation draft only;
**T3 = DeepSeek native-English repair of TA_raw, anchored to the ORIGINAL draft + a ProtectedTermLedger**
(ledger anchors + a DeepSeek span proposer for business nouns/status/action, each verified as an exact
substring of the source), gated by **facts + boundary + protected-term presence + a SendabilityGate**
(translationese / garble / wrong-agent / broken sign-off); one **T3b** minimal repair allowed. Code:
`backend-dotnet/tools/ReplyInMyVoice.Eval/TranslationPilotV2.cs` (`T3_PILOT=1`). Report:
`docs/rewrite-eval-results/20260527-040630-t3-translation-pilot.{md,json}`.

Numbers: T3 gate-pass **7/10** (all send-ready, 0 needed T3b); 3 fallback (all sentinel_broken: 021/029/036).
**Pangram of the 7: lower 1 (by −1 pt = noise), equal 4 (99→99), higher 2 (001 +8, 003 79→99 +20); mean Δ +4.**
**"Lower AND send-ready" = 1/10.** Calls: Youdao 20, Pangram 17, DeepSeek 24.

Verdict — **the two rounds together close the translation track:**
- The round-2 quality fixes WORK: the ProtectedTermLedger preserved business nouns (002 "saucer" no longer "tea tray"), and all 7 T3 are genuinely send-ready native English, often near-identical to T0.
- **That is exactly why detection-lowering fails:** repairing TA_raw to fluent, fact-anchored English makes DeepSeek converge back to ~T0, so Pangram returns to ~99 (003 even *rose* 79→99 — cleaner prose reads as MORE AI-like). Raw-TA's −76 lived entirely in the unreadable MT surface.
- **Detection-drop and readability are mutually exclusive here.** raw TA = low Pangram + unreadable; T3 = readable + ~T0 Pangram. There is no "both".

Decision: **close the translation track (both raw-TA and T3-repair).** Strongest possible confirmation of the 2026-05-27 halt on detection-chasing. Keep-worthy spinoffs, independent of detection: the **ProtectedTermLedger** and **SendabilityGate** are genuine eval-quality tools (they caught business-noun drift + translationese the fact-only judge missed) — fold into the rewrite-quality gate if useful. Product promise stays "natural/concise, facts preserved", never "detector-safe".

## Round-3 result — T4 selective-patch (2026-05-27, same 10 cases, Pangram ON)

Design (owner's round-3 doc): keep TA_raw's MT surface, do NOT full-repair; apply only budgeted span
patches (DeepSeek proposes find/replace, the PROGRAM applies them; budget ≤8 patches, ≤12% chars replaced,
≤30% new-writing, no 2-sentence patch; over budget → fallback, never full-repair). Protection-forward
masking + deterministic cleanup + a 3-tier gate (sendable / minor-awkward-but-sendable / unsendable). Code:
`backend-dotnet/tools/ReplyInMyVoice.Eval/TranslationPilotV3.cs` (`T4_PILOT=1`). Report:
`docs/rewrite-eval-results/20260527-042732-t4-translation-pilot.{md,json}`.

Result: **0/10 T4 accepted (0 even generated as a final candidate).** 6/10 fell back on sentinel_broken;
the other 4 had intact sentinels but blew the patch budget — making TA_raw sendable needed **40–75% of the
text replaced** (003 44%, 008 40%, 011 50%, 004 75%), far over the 12% cap. (Protection-forward masking was
dialed back from 15→3 extra spans mid-validation because masking more spans broke every sentinel.) Calls:
Youdao 20, Pangram 10 (only T0 scored — no T4 survived to measure), DeepSeek 14.

Verdict — **T4 has no sweet spot; MT damage is too pervasive to patch.** Below ~12% replacement the text
stays unsendable; the 40–75% actually required to fix it IS a rewrite (= T3, Pangram back to ~T0). No patch
budget yields lower-Pangram AND send-ready.

## Final verdict — translation track CLOSED (all 3 rounds, 2026-05-27)

| round | mechanism | Pangram vs T0 | readable? | lower+sendable |
|---|---|---|---|---|
| 1 raw-TA | Youdao is the final author | **−76** (6/7) | no — translationese + meaning drift | — (not readable) |
| 2 T3 | full DeepSeek native-repair | **+4** (1/7, −1 pt) | yes (≈T0) | 1/10 (noise) |
| 3 T4 | ≤12% selective patch | n/a (0 accepted) | — (needs 40–75% to be sendable) | **0/10** |

The detection drop is **inseparable from the unreadable raw-MT surface**: any repair that makes the text
send-ready also restores the LLM signal. **Stop all translation work for detection** — strongest possible
confirmation of the 2026-05-27 detection halt. Keep `ProtectedTermLedger` + `SendabilityGate` + `PatchApplier`
as eval-quality tools if useful; the real fidelity lever remains identifier/noun retention in the English
rewrite, not translation.

## Round-5 result — R5 fact-only repair (2026-05-27, same 10 cases, Pangram ON)

Design (owner's round-5 doc): keep TA_raw's MT surface, patch ONLY fact/boundary drift (DeepSeek locates drift
+ proposes fact patches; the PROGRAM applies them, replacements copied from Original/T0/ledger), under a
RELAXED gate — Q1 fact-safe + Q2 understandable required, Q3 native-send-ready RECORDED ONLY (translationese
accepted). Budget §7: ≤12 patches, fact-char ≤25%, style/new ≤5%, ≤1 full-sentence. Code:
`backend-dotnet/tools/ReplyInMyVoice.Eval/TranslationPilotV4.cs` (`R5_FACT_REPAIR_PILOT=1`). Report:
`docs/rewrite-eval-results/20260527-051059-r5a-fact-repair-pilot.{md,json}`. (First run hit a drift-call JSON
truncation bug on 3 cases; fixed token budget 2000→4000 + tolerant JSON extraction, re-ran.)

Result vs the §11 bar: **Q1 fact-safe 2/10 (need ≥8) ✗ · Q2 understandable 2/10 (≥8) ✗ · only 2/10 accepted (Pangram-win needs ≥6/10) ✗ · Q3 native 0/10.** 8/10 fell back: patch-budget exceeded ×6 (heavy drift needs >25% fact patches or >1 full-sentence replacement = effectively a rewrite), sentinel-broken ×2. The 2 accepted (002 99→0, 041 99→79; mean Δ −60) are **fact-safe but translationese** — e.g. "the replacement were marked", "I'm going to queue up to exchange cups", "I am unable to get a full refund" (agent-error), "reached your photo intact". Calls: Youdao 20, Pangram 12, DeepSeek 12.

Verdict — **R5 confirms the wall, doesn't beat it.** Accepting translationese lets only 2/10 through (the rest need so much fact-patching they exceed budget = a rewrite), and those 2 are non-native by construction (Q3 0/10) — low Pangram *because* they read like MT. The relaxed bar just relabels degraded MT as "acceptable".

## FINAL VERDICT — detection track CLOSED (5 rounds + diagnostic, 2026-05-27)

| round | mechanism | best Pangram vs T0 | readable / native? | usable wins |
|---|---|---|---|---|
| 1 raw-TA | Youdao = final author | −76 (6/7) | ✗ translationese + drift | 0 |
| 2 T3 | full DeepSeek native repair | +4 | ✓ but ≈T0 | 0 (≈T0) |
| 3 T4 | ≤12% selective patch | n/a (0 accepted) | — needs 40–75% to send | 0/10 |
| 4 diagnostic | original draft vs T0 | original ~99 = T0 | — inputs already ~99 | premise dead |
| 5 R5 | fact-only patch, accept translationese | −60 (2/2) | ✗ Q3 native 0/10 | 2/10, both non-native |

**One law across all five:** under the hard constraints (no human, draft + warm tone only, facts/boundaries safe),
**a low Pangram reading ⟺ a non-native machine-translation surface.** Every transform that yields clean, native,
send-ready English lands at ~T0 (~99); the only way down is to ship text that does not read like a person — which
is off-brand for "Reply In My Voice" and is the detection-chasing halted 2026-05-27. **Stop the detection track.**
Keep the eval-quality byproducts (`ProtectedTermLedger`, `SendabilityGate`, `SourceOverlapGate`, `PatchApplier`,
`FactDriftRepairer`) for the rewrite-quality system if useful; the real fidelity lever is identifier/noun retention
in the rewrite, not translation.

## Round-6 result — external rewrite engine A/B (Manus API), 2026-05-27, 10 cases, Pangram ON

Different variable: swap the ENGINE. T0 vs EX1 = Manus API v2 task (`agent_profile=manus-1.6-lite`), one-shot, same
draft/tone/ledgers, same gates (semantic Fact + Boundary + Forbidden + Understandability hard; ProtectedTerm + 
NativeSendReady recorded — ProtectedTerm demoted from hard after it false-flagged a legitimate "confirm"→"decide"
paraphrase). Pluggable `IExternalRewriteProvider` (Manus + generic HTTP). Code:
`backend-dotnet/tools/ReplyInMyVoice.Eval/ExternalRewritePilot.cs` (`EXTERNAL_REWRITE_AB_PILOT=1`). Manus API contract
verified live (task.create → poll task.listMessages → structured_output_result). 10/10 tasks succeeded, latency
median 45s / p90 58s. Report: `docs/rewrite-eval-results/20260527-054138-external-rewrite-ab.{md,json}`.

Results vs the §10 bar:
- **Quality (PASS, strong):** EX1 hard-gate pass **10/10** (T0 9/10), native-send-ready **10/10** (T0 9/10), 0 fact failures, 0 forbidden. quality-winner EX1 1 / tie 9 / T0 0. Manus — even the lite profile — is at least as fact-safe + natural as the C# baseline.
- **Detection (FAIL):** Pangram of 9 pairs — lower 4 (small, −2..−18), higher 1 (003 +53, mostly a T0 outlier-low run at 24), equal 4 (99→99). **mean T0 90 → EX1 94, mean Δ +3, median Δ 0.** Pangram-win 4/9 (<5), median 0 (>−10) → fails detection criteria.

Verdict — **Manus is a quality-competitive rewrite ENGINE, NOT a detection win.** The five-round law holds again: clean
native English ⟹ ~high Pangram (EX1 ~94 ≈ T0 ~90). Per §15 this is a legitimate QUALITY-track outcome, not a detection
scheme. If the goal is rewrite quality (faithfulness/naturalness) rather than detection, Manus is the first external
option that matches/edges the C# baseline and is worth a larger (24–100-case) eval + real cost/latency assessment.
Caveats: lite profile; ~45s/case latency = async-only (too slow for inline sync); paid per task; can be slightly more
template-y (added "I hope you're having a good week" on 001). **Detection track stays CLOSED; round-6 confirms it from a
new angle (a genuinely different, competent engine still can't drop Pangram on send-ready output).**

## Round-7 result — Chinese-intermediate polish (2026-05-27, 10 cases, Pangram ON — CORRECTED)

R7: LLM polishes the CHINESE middle state; Youdao stays the final English author; English end gets only a tight ≤8%
entity patch. Code `TranslationPilotV6.cs` (`R7_CN_INTERMEDIATE_PILOT=1`); bracket-free sentinel `QZAN000QZ` added to
`AnchorMasker` (`[[A0]]` didn't survive the DeepSeek Chinese pass). Report
`docs/rewrite-eval-results/20260527-072439-r7-cn-intermediate-pilot.{md,json}`.
**Correction:** an earlier note claimed R7 "stays ~99" — that was never measured and wrongly carried over the R8A
(no-translation) number. R7's back-translation Pangram, now measured for every case, is LOW.

**The back-translation IS low Pangram:** 001 98→7, 002 99→2, 003 93→10, 004 99→4, 008 99→7, 011 97→15 — **7/9 dropped
to 2–15**. The 3 that stayed high (029 97, 036 98, 041 83) are the heavily-masked cases: many fact-anchors restored as
verbatim native English dilute the MT-ness, so protecting facts via masking actually *raises* the score back.

**But it is not usable: R7A hard-gate pass 1/10** (only 041). 8/10 fail `english_patch_over_budget` (20–43%): making the
low-Pangram MT text fact-safe needs 20–43% English rewriting, over the 8% budget. **The drop and the fact-damage are the
SAME Youdao mangling — coupled:** the cases that drop most (002→2, 004→4; needing 23–29% fixes) are exactly the ones that
break facts most; the only gate-pass (041, 2% patch) is the one that barely dropped (83) and is translationese. R7A≈R7C.

Verdict — R7 confirms translation lowers Pangram dramatically (as raw-TA did), but **the Pangram drop and fact-drift /
translationese are inseparable — produced by the same MT-mangling.** You cannot get a big drop without big fact damage;
keeping facts (more verbatim English) raises the score back. No operating point has both low Pangram and fact-safe quality.

## Round-8 result — R8A controlled-international register (2026-05-27, 10 cases, Pangram ON)

R8 reframe: redefine the detection-mode target from native-send-ready to "controlled international English"
(professional, sendable, slight non-native rhythm OK). R8A (decisive cheap branch): DeepSeek generates this register
DIRECTLY — NO translation. New `ProfessionalInternationalEnglishGate` (professional/understandable/sendable, not native;
fails garble/agent-error/object-drift/severe-grammar). Code `R8LayeredPilot.cs` (`R8_LAYERED_PILOT=1`). Report
`docs/rewrite-eval-results/20260527-070122-r8a-controlled-intl-pilot.{md,json}`.

Result: R8A hard-gate pass **10/10**, professional-international **10/10** (quality fine, fact-safe). BUT Pangram: lower 1,
higher 4, equal 4; **mean T0 91 → R8A 99, mean Δ +8, median 0** — R8A is if anything slightly WORSE than T0. **Decisively:
even the 2 cases R8A came out genuinely non-native (native_like=false, e.g. 003) still scored Pangram 99.** So the register
reframe does NOT lower Pangram even when the text really is less-native — the detector keys on the LLM generation
fingerprint, which survives the register change. R8B/R8C not built: they only re-add the translation tradeoff (R8B =
R7-with-relaxed-gate; R8C segment-splice keeps fact/boundary sentences at ~99 so the windowed mean barely moves). §10
detection bar fails (Pangram-win 1/9, median 0). Useful non-detection byproduct: the controlled-international generator +
`ProfessionalInternationalEnglishGate` are a usable STYLE option if the product ever wants a deliberately less-polished,
more-direct voice — a voice choice, not a detection lever.

## FINAL — detection track CLOSED across 8 rounds (2026-05-27)

| round | lever | best Pangram vs T0 | usable? |
|---|---|---|---|
| 1 raw-TA | translation, MT is final author | −76 | no — unreadable |
| 2 T3 | translation + full DeepSeek repair | +4 | ≈T0 |
| 3 T4 | translation + ≤12% selective patch | 0/10 viable | no |
| 4 diagnostic | original draft vs T0 | originals ~99 | premise dead |
| 5 R5 | fact-only patch, accept translationese | −60 (2/10) | translationese only |
| 6 Manus | external rewrite engine | +3 | quality-competitive, not a detection win |
| 7 R7 | Chinese-intermediate polish | back-trans −82..−97 (7/9), but 1/10 gate-pass | no — drop ⟺ fact-drift coupled |
| 8 R8A | controlled-international register (no translation) | +8 | register doesn't move Pangram |

**One law, eight confirmations:** a low Pangram reading requires actual non-native MT-artifact text; every fact-safe,
professional, sendable output — any register, any engine, any location of the LLM polish — lands at ~T0 (~99), because the
detector keys on the LLM generation fingerprint that survives all of these transforms. **R7 (corrected) makes the mechanism
sharpest: the Pangram drop and the fact-drift/translationese are the SAME MT-mangling — coupled, not separable. Translation
DOES lower Pangram dramatically (−82..−97), but only by damaging facts in equal measure; preserving facts (verbatim English)
raises the score straight back. There is no operating point with both a low reading and fact-safe send-ready quality.**
**Detection track CLOSED.** Productive
direction = the quality track (fact-safety, business-noun protection, sendability, user-voice); product promise "natural /
concise / facts preserved", never "detector-safe". Reusable eval-quality byproducts: `ProtectedTermLedger`, `SendabilityGate`,
`FactDriftRepairer`, `PatchApplier`, `ProfessionalInternationalEnglishGate`, the pluggable `IExternalRewriteProvider` (Manus),
and the controlled-international generator (as a voice/style option).

## Round-10 result — DCR dual-channel (2026-05-27, 10 cases, Pangram ON)

DCR: keep fact/boundary/(next-step) sentences as native T0; route only low-risk context/warmth/closing sentences through a
Youdao round-trip (texture channel) and splice back — perturb only safe sentences to dilute the windowed Pangram mean.
DCR-A = texture only; DCR-B = + next_step. DeepSeek only classifies sentences. Code `R10DualChannelPilot.cs`
(`DUAL_CHANNEL_TRANSLATION_PILOT=1`). Report `docs/rewrite-eval-results/20260527-081412-dcr-dual-channel-pilot.{md,json}`.

Surface result: DCR-A hard-gate 9/10, avg texture perturbation 18% sentences / 10% chars; Pangram **lower 4, equal 5,
higher 0; mean T0 97 → 74, mean Δ −22, median 0**. Looks like a partial win — but it **dissolves under inspection**:
- **011 (−65):** DCR-B text is ~identical to T0 (one clause changed) → not content-driven → **Pangram noise**.
- **001 (−27):** one sentence reworded (front office→front hall) → mostly noise + minor drift.
- **003 (−36) / 021 (−74):** real perturbation, but with **fact/object drift the lenient gate missed** — "$31.50 seat credit"→"letter of credit" (003); "ceramic planter"→"flowerpot" (021). Same drop⟺damage coupling at sentence granularity.
- 5/10 no effect; median 0. **Fails §9 bar** (win 4/10 < 5; median 0 > −10).

**Two findings:** (1) partial/sentence-level perturbation does not reliably move a DOCUMENT-level detector — you need the
majority of the text to be MT (raw-TA/R7 whole-text → ~2–15; DCR ~16% → ~99). (2) **Pangram is too noisy to trust small
single-shot deltas:** in this session the SAME case's T0 scored 003: 24/51/93/94 and 021: 23/78/99 — ±50–70 swings on ~identical
content. You cannot optimize against a metric this unstable; a −20..−60 delta is inside the noise floor. The lenient semantic
judge also missed object/term drift (letter-of-credit, flowerpot), so "fact-safe 9/10" is overstated.

## FINAL (updated) — detection track CLOSED across 9 distinct mechanisms (2026-05-27)

Mechanisms tried: (1) translation-as-final-author, (2) full LLM repair, (3) small selective patch, (4) input-preserving
diagnostic, (5) fact-only patch, (6) external engine (Manus), (7) Chinese-intermediate polish, (8) controlled-international
register, (9) dual-channel sentence dilution. **All converge.** Two independent reasons the track is closed:
- **Coupling:** a low Pangram reading and fact-safe/send-ready quality are produced/destroyed by the SAME MT-mangling — translation drops the score (−82..−97) but only by damaging facts in equal measure; clean native/professional output (any register/engine/polish-location) sits at ~T0 (~99).
- **Metric instability:** single-shot Pangram swings ±50–70 on identical content, so even an apparent drop is not trustworthy or shippable.

**Stop the detection track.** Quality-track byproducts worth keeping (independent of detection): the gate suite
(`ProtectedTermLedger`, `SendabilityGate`, `ProfessionalInternationalEnglishGate`, `FactDriftRepairer`, `PatchApplier`),
the Manus `IExternalRewriteProvider` (quality-competitive engine), and the sentence risk classifier. Note: the semantic
judge needs hardening on object/term drift before it's trusted as a quality gate (it missed letter-of-credit / flowerpot).

## Youdao LLM-translation probe (2026-05-27, `YOUDAO_LLM_PROBE=1`, `YoudaoLlmProbe.cs`)

Tried Youdao's **large-model** translation API (`POST /proxy/http/llm-trans`, SSE, Pro 14B) instead of the NMT used in
rounds 1-7. Report: `docs/rewrite-eval-results/20260527-095552-youdao-llm-probe.md`. **Correction to the earlier
"translation snaps back to ~99" claim — that was too absolute.** The LLM translator is far more fluent than NMT and the
outcome is INCONSISTENT, not uniform. Of the 5 cases Pangram could measure (it rate-limited on the other 5):
- **001: T0 97 → 28**, and on manual check the text is **materially fact-safe** (Maya / FieldTrip-4A-09 / $12 / April 2 /
  the boundary all held; only "folder basket"→"folder", "front office"→"front desk") **AND fluent** — the single closest
  thing to a "win" in the whole investigation.
- 002 → 92, 004 → 99, 008 → 95 (still firmly AI); 003 35→33 (T0 already a noise-low); 041 failed facts (Celestine→Celeste).

So 4/5 stayed AI and one (041) drifted facts, but **001 is a real fluent + fact-safe + low (28) outlier** the "clean⟺~99"
generalization does not explain. **Not yet a reliable method:** (a) 1/5; (b) 28 is a single Pangram shot and the metric
swings ±50 on identical text (003 T0 = 35 here vs 93-99 elsewhere) → could be a lucky-low; (c) Pangram rate-limited
mid-run, so reproducibility is unverified. **Open thread (honest):** once Pangram budget/rate recovers, re-score 001's
exact text 3-5× + more cases — if 001 reliably lands low while fact-safe, understand *why 001*; if it bounces, it was
noise. This does not establish fact-safe reliable detection-lowering (4/5 stayed AI), but it is NOT dismissable as
obviously-broken, and the earlier uniform "~99" framing was wrong.

## Cross-detector check (GPTZero, 2026-05-27, `GPTZERO_PROBE=1`, `GptzeroProbe.cs`) — resolves the 001 lead

Pangram credits ran out (401), so the owner supplied a GPTZero key; scored 3 fixed texts on GPTZero (`POST
api.gptzero.me/v2/predict/text`). **The Pangram "wins" do NOT transfer:**
- 002 clean rewrite: Pangram 100% AI → GPTZero **100% AI** (agree).
- 002 broken translationese (Pangram **100% Human**): GPTZero **100% AI**.
- 001 Youdao-LLM (Pangram **28**): GPTZero **100% AI**.

So **001@28 was Pangram-specific** (a quirk/noise of one detector), not genuine less-detectability — the open thread is
resolved (effectively noise/overfit). This adds a THIRD independent reason the detection track is dead, alongside (1) the
drop⟺fact-damage coupling and (2) Pangram's ±50 metric noise: **(3) cross-detector non-transfer** — even an apparent win
against one detector is whack-a-mole; a different detector (GPTZero) flags all of it as AI, correctly, because the text
genuinely is AI-generated. (Op note: GPTZero's error payload echoes the api key; the probe was hardened to never print
response bodies, and the first GPTZero key — which 403'd "no owner" and got echoed — was rotated by the owner.)
