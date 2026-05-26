# Semantic re-score (C# eval tool; judge deepseek-v4-pro; prompt semverify-v1-2026-05-27)

| id | det_facts | sem_facts | det_forbid | sem_forbid | really-lost |
| --- | --- | --- | ---: | ---: | --- |
| rewrite-draft-002 | True | True | 0 | 0 |  |
| rewrite-draft-003 | True | True | 0 | 0 |  |
| rewrite-draft-005 | True | True | 0 | 0 |  |
| rewrite-draft-006 | True | True | 0 | 0 |  |
| rewrite-draft-028 | True | True | 0 | 0 |  |
| rewrite-draft-041 | True | True | 0 | 0 |  |
| rewrite-draft-042 | True | True | 0 | 0 |  |
| rewrite-draft-045 | True | True | 0 | 0 |  |
| rewrite-draft-049 | True | True | 0 | 0 |  |
| rewrite-draft-061 | True | True | 0 | 0 |  |
| rewrite-draft-066 | True | True | 0 | 0 |  |
| rewrite-draft-071 | True | False | 1 | 0 | missing:The package arrived on April 22 with a cracked blender base motor housing. |
| rewrite-draft-074 | True | True | 0 | 0 |  |
| rewrite-draft-080 | True | True | 0 | 0 |  |
| aidc-201 | False | True | 0 | 0 |  |
| aidc-202 | True | True | 2 | 0 |  |
| aidc-203 | False | True | 0 | 0 |  |
| aidc-204 | True | True | 0 | 0 |  |
| aidc-205 | True | True | 0 | 0 |  |
| aidc-206 | False | True | 0 | 0 |  |
| aidc-207 | False | True | 0 | 0 |  |
| aidc-208 | False | True | 0 | 0 |  |

SEMANTIC (C#): facts 21/22 (det 17/22); forbidden 0/22 (det 2)

- fact false-negatives (det fail, really pass): aidc-201, aidc-203, aidc-206, aidc-207, aidc-208
- fact false-positives (det pass, really lost): rewrite-draft-071
- forbidden false-positives (det flagged, really clean): rewrite-draft-071, aidc-202
- forbidden false-negatives (det missed a real one): 
