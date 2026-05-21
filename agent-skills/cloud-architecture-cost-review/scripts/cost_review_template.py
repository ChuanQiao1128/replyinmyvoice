#!/usr/bin/env python3
"""Print a concise cloud architecture cost review template."""

from __future__ import annotations

import argparse


def main() -> None:
    parser = argparse.ArgumentParser(description="Create a cloud architecture cost review skeleton.")
    parser.add_argument("topic", nargs="?", default="Proposed cloud architecture")
    args = parser.parse_args()

    print(f"# Architecture Cost Review: {args.topic}")
    print()
    print("## Goal")
    print("- ")
    print()
    print("## Usage Assumption")
    print("- Expected traffic / request volume:")
    print("- MVP, demo, production, or migration context:")
    print("- Latency / cold-start tolerance:")
    print()
    print("## Runtime Requirements")
    print("- HTTP/API behavior:")
    print("- Worker / queue / timer behavior:")
    print("- Database / storage needs:")
    print("- Auth, webhooks, or external provider needs:")
    print()
    print("## Options Compared")
    print("| Option | Fit | Fixed cost risk | Variable cost risk | Operational complexity | Verdict |")
    print("| --- | --- | --- | --- | --- | --- |")
    print("| Cheapest viable option |  |  |  |  |  |")
    print("| Current/proposed option |  |  |  |  |  |")
    print("| More robust option |  |  |  |  |  |")
    print()
    print("## Recommendation")
    print("- Recommended option:")
    print("- Why it fits:")
    print("- What would make this recommendation change:")
    print()
    print("## Rejected Options")
    print("- ")
    print()
    print("## Approval Gates")
    print("- Paid resource actions requiring user approval:")
    print("- Current pricing sources to verify before quoting exact numbers:")
    print()
    print("## Verification Needed")
    print("- Local tests:")
    print("- Cloud smoke tests:")
    print("- Cost telemetry or budget checks:")
    print()
    print("## Limitations")
    print("- ")


if __name__ == "__main__":
    main()
