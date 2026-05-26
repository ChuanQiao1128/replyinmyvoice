# AI Draft Cleanup baseline — semantic re-score (LLM judge)

Judged 22/22 cases (0 judge errors). Model: deepseek-v4-pro.

- **True facts pass (semantic): 21/22**  vs deterministic 17/22
- deterministic fact FALSE-NEGATIVES (said fail, really preserved): ['aidc-201', 'aidc-203', 'aidc-206', 'aidc-207', 'aidc-208']
- deterministic fact false-positives (said pass, really lost): ['rewrite-draft-071']
- **True forbidden violations (semantic): 0/22**  vs deterministic 2/22
- deterministic forbidden FALSE-POSITIVES (flagged, really clean): ['rewrite-draft-071', 'aidc-202']
- deterministic forbidden false-negatives (missed a real one): []
- meaning_changed: []
- send_ready (judge): 22/22

## Cases the judge says really lost/contradicted a fact (the ones that matter)
- **rewrite-draft-071**: missing — The package arrived on April 22 with a cracked blender base motor housing. — The rewrite says 'arrived damaged' but does not specify the nature of the damage (cracked blender base motor housing).

## Real forbidden violations (judge)