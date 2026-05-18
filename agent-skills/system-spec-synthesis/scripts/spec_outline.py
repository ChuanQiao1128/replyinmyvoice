#!/usr/bin/env python3
"""Generate a system specification outline for agent planning."""

from __future__ import annotations

import argparse
from datetime import date


SECTIONS = [
    "Context",
    "Goals",
    "Non-Goals",
    "Current System",
    "Proposed Architecture",
    "Data Model",
    "API and Job Contracts",
    "State and Error Handling",
    "Security and Privacy",
    "Rollout Plan",
    "Verification Plan",
    "Open Questions",
]


def main() -> None:
    parser = argparse.ArgumentParser(description="Print a system spec markdown skeleton.")
    parser.add_argument("title", help="Feature or system name")
    parser.add_argument(
        "--owner",
        default="Codex/Claude Code",
        help="Responsible implementation agent or team",
    )
    args = parser.parse_args()

    print(f"# {args.title} Specification")
    print()
    print(f"- Date: {date.today().isoformat()}")
    print(f"- Owner: {args.owner}")
    print("- Status: Draft")
    for section in SECTIONS:
        print()
        print(f"## {section}")
        print()
        print("- ")


if __name__ == "__main__":
    main()
