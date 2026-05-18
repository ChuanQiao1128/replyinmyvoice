#!/usr/bin/env python3
"""Print a resilience test matrix for a component."""

from __future__ import annotations

import argparse


DEFAULT_FAILURES = [
    ("timeout", "dependency does not answer before deadline"),
    ("transient-5xx", "dependency returns a retryable server error"),
    ("permanent-4xx", "dependency rejects the request permanently"),
    ("duplicate-event", "same webhook, queue message, or command is delivered twice"),
    ("partial-success", "state was persisted before a later step failed"),
    ("concurrent-requests", "two requests mutate the same quota or lifecycle state"),
    ("malformed-payload", "input is syntactically valid transport but invalid domain data"),
]


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate a markdown resilience matrix.")
    parser.add_argument("component", help="Component, route, service, or job name")
    args = parser.parse_args()

    print(f"# Resilience Matrix: {args.component}")
    print()
    print("| Failure mode | Scenario | Expected invariant | Suggested test |")
    print("| --- | --- | --- | --- |")
    for mode, scenario in DEFAULT_FAILURES:
        print(
            f"| `{mode}` | {scenario} | No duplicate side effects; user-visible state remains correct. | Add deterministic fake and assert final persisted state. |"
        )


if __name__ == "__main__":
    main()
