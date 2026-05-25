# Single-Input Smoke 10 Remaining Issues

Date: 2026-05-25

Source report: `docs/eval-runs/single-input-smoke-10-pipeline-fix-v1.md`

The Step B smoke run reached the expansion floor, so these issues are recorded for
tracking rather than used as a reason to overfit the first 10 cases.

## Case rewrite-draft-002

- Exact failure: customer-usable pass failed because of a quality regression, not a
  fact or unsupported-claim failure. The rewrite preserved facts but placed the hard
  refund denial before the available no-cost replacement path, reducing the warmth and
  usefulness of the support flow.
- Classification: rewrite pipeline issue.
- Fix now or defer: defer. The likely general fix is support-flow ordering in candidate
  selection or review, but changing selection for one smoke case before seeing cases
  011-020 would risk overfitting.

## Case rewrite-draft-006

- Exact failure: the rewrite softened one dependency. It said the sender would confirm
  the selected time, but it did not preserve that confirmation happens after/as soon as
  Ren replies.
- Classification: locked-fact extractor issue plus rewrite pipeline issue.
- Fix now or defer: defer unless the same dependency-loss pattern repeats in dev 20.
  The generalizable fix would be to lock reply-dependent confirmation clauses, but this
  smoke result has no scheduling hallucination and no unsupported options, so expanding
  first is a better signal.

## Decision

No code or prompt patch is made for the remaining smoke-10 issues in this step. The
dev-20 run should determine whether support-flow ordering or reply-dependent
confirmation loss repeats outside the first 10 cases.
