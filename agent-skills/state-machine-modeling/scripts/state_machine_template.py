#!/usr/bin/env python3
"""Generate a state machine modeling template."""

from __future__ import annotations

import argparse


def main() -> None:
    parser = argparse.ArgumentParser(description="Print a state machine markdown template.")
    parser.add_argument("entity", help="Lifecycle entity name")
    args = parser.parse_args()

    print(f"# State Machine: {args.entity}")
    print()
    print("## States")
    print()
    print("| State | Meaning | Persisted field/value | Terminal? |")
    print("| --- | --- | --- | --- |")
    print("|  |  |  |  |")
    print()
    print("## Events")
    print()
    print("| Event | Source | Required data |")
    print("| --- | --- | --- |")
    print("|  |  |  |")
    print()
    print("## Transitions")
    print()
    print("| From | Event | To | Side effects | Reject when | Tests |")
    print("| --- | --- | --- | --- | --- | --- |")
    print("|  |  |  |  |  |  |")
    print()
    print("## Invariants")
    print()
    print("- ")
    print()
    print("## Illegal Transitions")
    print()
    print("- ")


if __name__ == "__main__":
    main()
