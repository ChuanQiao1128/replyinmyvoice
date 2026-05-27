# A/B readout (eval-only, 22-case smoke; protocol-locked)

Judge deepseek-v4-pro; Pangram mean-window. n per variant varies with engine success.

## Step 0 — V0 reproducibility
V0 semantic facts 21/22, forbidden 0, meaning 0, send-ready 22/22; Pangram: median=98 p75=99 p90=99 %>=90=64% %>=95=59% (mean=80.9 ref)
(expected ~21/22 facts, 0 forbidden, median ~96 — compare.)

## Step 1 — semantic hard gate

| variant | desc | facts pass | material loss | minor loss | forbidden | meaning | send-ready | gate |
|---|---|---|---|---|---|---|---|---|
| v0 | baseline (current engine) | 21/22 | rewrite-draft-071 | 0 | 0 | 0 | 22/22 | V0 |
| v1 | short-skeleton-trim | 20/22 | rewrite-draft-071 | 1 | 0 | 0 | 22/22 | kill |
| v2 | no-default-greeting/signoff | 20/22 | rewrite-draft-071 | 1 | 0 | 1 | 21/22 | kill |
| v3 | facts-first routing | 22/22 | - | 0 | 0 | 0 | 22/22 | PASS |
| v4 | combined (v2+v3) | 22/22 | - | 0 | 1 | 0 | 21/22 | kill |

Survivors (Pangram-scored): v3

## Step 2/2b — Pangram on survivors + V0 (paired vs V0)

### v3 — facts-first routing
Output Pangram: median=96 p75=99 p90=99 %>=90=59% %>=95=50% (mean=67.7 ref)
| case | V0 | v3 | delta | hr V0 | hr v3 | facts |
|---|---|---|---|---|---|---|
| rewrite-draft-002 | 99 | 99 | +0 | Y | Y | same |
| rewrite-draft-003 | 99 | 98 | -1 | Y | Y | same |
| rewrite-draft-005 | 99 | 99 | +0 | Y | Y | same |
| rewrite-draft-006 | 59 | 59 | +0 | - | - | same |
| rewrite-draft-028 | 99 | 99 | +0 | Y | Y | same |
| rewrite-draft-041 | 99 | 99 | +0 | Y | Y | same |
| rewrite-draft-042 | 83 | 22 | -61 | - | - | same |
| rewrite-draft-045 | 99 | 99 | +0 | Y | Y | same |
| rewrite-draft-049 | 99 | 99 | +0 | Y | Y | same |
| rewrite-draft-061 | 99 | 99 | +0 | Y | Y | same |
| rewrite-draft-066 | 99 | 99 | +0 | Y | Y | same |
| rewrite-draft-071 | 90 | 94 | +4 | Y | Y | diff |
| rewrite-draft-074 | 46 | 78 | +32 | - | - | same |
| rewrite-draft-080 | 99 | 99 | +0 | Y | Y | same |
| aidc-201 | 86 | 6 | -80 | - | - | same |
| aidc-202 | 97 | 92 | -5 | Y | Y | same |
| aidc-203 | 99 | 1 | -98 | Y | - | same |
| aidc-204 | 1 | 34 | +33 | - | - | same |
| aidc-205 | 82 | 98 | +16 | - | Y | same |
| aidc-206 | 25 | 11 | -14 | - | - | same |
| aidc-207 | 24 | 6 | -18 | - | - | same |
| aidc-208 | 97 | 0 | -97 | Y | - | same |

**v3 paired:** improved(<=-10) 6 · worsened(>=+10) 3 · unchanged 13 · high-risk->non 2 · non->high-risk 1