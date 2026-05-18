#!/usr/bin/env python3
"""Scan a repo for common persistence risk signals."""

from __future__ import annotations

import argparse
from pathlib import Path


SKIP_DIRS = {
    ".git",
    ".next",
    ".open-next",
    ".wrangler",
    ".turbo",
    "node_modules",
    "bin",
    "obj",
    "dist",
    "coverage",
}

GENERATED_PATH_PARTS = {
    ("lib", "generated"),
    ("generated",),
}

PATTERNS = {
    "raw-sql": ["ExecuteSqlRaw", "$queryRaw", "$executeRaw"],
    "read-then-write": ["findFirst", "FirstOrDefaultAsync", "SingleOrDefaultAsync"],
    "transaction": ["transaction", "Transaction", "BeginTransaction"],
    "idempotency": ["idempot", "StripeEvent", "eventId", "Webhook"],
    "quota": ["quota", "Usage", "Reservation", "RewriteUsage"],
    "wall-clock": ["DateTime.Now", "new Date()", "Date.now()"],
    "todo": ["TODO", "FIXME"],
}

EXTENSIONS = {".ts", ".tsx", ".js", ".cs", ".prisma", ".sql"}


def has_generated_part(path: Path) -> bool:
    parts = path.parts
    for generated_parts in GENERATED_PATH_PARTS:
        width = len(generated_parts)
        if any(tuple(parts[index : index + width]) == generated_parts for index in range(len(parts) - width + 1)):
            return True
    return False


def should_skip(path: Path, include_generated: bool) -> bool:
    if any(part in SKIP_DIRS for part in path.parts):
        return True
    if not include_generated and has_generated_part(path):
        return True
    return False


def scan(root: Path, include_generated: bool) -> list[tuple[str, Path, int, str]]:
    findings: list[tuple[str, Path, int, str]] = []
    for path in root.rglob("*"):
        if should_skip(path, include_generated) or not path.is_file() or path.suffix not in EXTENSIONS:
            continue
        try:
            lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
        except OSError:
            continue
        for line_no, line in enumerate(lines, start=1):
            for category, needles in PATTERNS.items():
                if any(needle in line for needle in needles):
                    findings.append((category, path, line_no, line.strip()))
    return findings


def limited_findings(
    findings: list[tuple[str, Path, int, str]], limit: int
) -> list[tuple[str, Path, int, str]]:
    if limit <= 0:
        return findings
    return findings[:limit]


def main() -> None:
    parser = argparse.ArgumentParser(description="Scan for data module review signals.")
    parser.add_argument("root", nargs="?", default=".", help="Repository root")
    parser.add_argument("--limit", type=int, default=120, help="Maximum rows to print. Use 0 for no limit.")
    parser.add_argument(
        "--include-generated",
        action="store_true",
        help="Include generated source directories such as lib/generated.",
    )
    args = parser.parse_args()
    root = Path(args.root).resolve()
    findings = scan(root, include_generated=args.include_generated)

    print(f"# Data Risk Scan: {root}")
    print()
    if args.limit > 0 and len(findings) > args.limit:
        print(f"Showing first {args.limit} of {len(findings)} findings. Re-run with `--limit 0` for all rows.")
        print()
    print("| Category | File | Line | Signal |")
    print("| --- | --- | ---: | --- |")
    for category, path, line_no, line in limited_findings(findings, args.limit):
        rel = path.relative_to(root)
        safe_line = line.replace("|", "\\|")
        print(f"| {category} | `{rel}` | {line_no} | `{safe_line[:120]}` |")


if __name__ == "__main__":
    main()
