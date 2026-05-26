# AI Draft Cleanup baseline — Pangram snapshot (offline, 1 score/text)

Release-level AI-detection-risk baseline of the CURRENT engine. One Pangram score per text; no feedback, no best-of-N, no per-email target.

## Output distribution (the baseline to beat)
median=96 p75=99 p90=99 >=90=15/22 >=50=16/22 <25=6/22

## Draft distribution (for reference)
median=99 p75=99 p90=99 >=90=21/22 >=50=22/22 <25=0/22

## Did the rewrite lower detection risk? mean draft=97.4 -> mean output=71.4 | outputs lower than their draft: 12/22

## Output distribution by router strategy
- FactsFirstReconstruct (1): median=95 p75=95 p90=95 >=90=1/1 >=50=1/1 <25=0/1
- FullStructureRewrite (1): median=95 p75=95 p90=95 >=90=1/1 >=50=1/1 <25=0/1
- MinimalPolish (2): median=4 p75=4 p90=4 >=90=0/2 >=50=0/2 <25=2/2
- QuoteListSafe (3): median=99 p75=99 p90=99 >=90=3/3 >=50=3/3 <25=0/3
- SupportPolicyOptions (15): median=96 p75=99 p90=99 >=90=10/15 >=50=11/15 <25=4/15

## Output distribution by input length
- short <=35 (5): median=4 p75=99 p90=99 >=90=2/5 >=50=2/5 <25=3/5
- mid 36-119 (5): median=95 p75=99 p90=99 >=90=3/5 >=50=4/5 <25=1/5
- long >=120 (12): median=98 p75=99 p90=99 >=90=10/12 >=50=10/12 <25=2/12

## per-case (draft_pg -> out_pg)
| id | strategy | in_wc | out_wc | draft_pg | out_pg |
|---|---|---|---|---|---|
| rewrite-draft-002 | QuoteListSafe | 118 | 119 | 99 | 99 |
| rewrite-draft-003 | SupportPolicyOptions | 136 | 134 | 99 | 96 |
| rewrite-draft-005 | SupportPolicyOptions | 113 | 106 | 99 | 99 |
| rewrite-draft-006 | SupportPolicyOptions | 61 | 62 | 78 | 59 |
| rewrite-draft-028 | SupportPolicyOptions | 154 | 95 | 99 | 22 |
| rewrite-draft-041 | SupportPolicyOptions | 332 | 266 | 99 | 99 |
| rewrite-draft-042 | SupportPolicyOptions | 278 | 228 | 99 | 22 |
| rewrite-draft-045 | SupportPolicyOptions | 295 | 296 | 99 | 99 |
| rewrite-draft-049 | SupportPolicyOptions | 257 | 227 | 99 | 99 |
| rewrite-draft-061 | QuoteListSafe | 202 | 187 | 99 | 99 |
| rewrite-draft-066 | QuoteListSafe | 215 | 178 | 99 | 99 |
| rewrite-draft-071 | SupportPolicyOptions | 200 | 104 | 99 | 92 |
| rewrite-draft-074 | SupportPolicyOptions | 231 | 196 | 99 | 91 |
| rewrite-draft-080 | SupportPolicyOptions | 239 | 199 | 99 | 99 |
| aidc-201 | SupportPolicyOptions | 33 | 17 | 99 | 99 |
| aidc-202 | SupportPolicyOptions | 32 | 25 | 91 | 99 |
| aidc-203 | SupportPolicyOptions | 30 | 11 | 99 | 1 |
| aidc-204 | SupportPolicyOptions | 87 | 20 | 99 | 1 |
| aidc-205 | FactsFirstReconstruct | 86 | 42 | 99 | 95 |
| aidc-206 | FullStructureRewrite | 131 | 31 | 99 | 95 |
| aidc-207 | MinimalPolish | 31 | 16 | 96 | 4 |
| aidc-208 | MinimalPolish | 34 | 18 | 95 | 3 |