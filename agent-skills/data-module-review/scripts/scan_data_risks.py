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
    "node_modules",
    "bin",
    "obj",
    "dist",
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


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIRS for part in path.parts)


def scan(root: Path) -> list[tuple[str, Path, int, str]]:
    findings: list[tuple[str, Path, int, str]] = []
    for path in root.rglob("*"):
        if should_skip(path) or not path.is_file() or path.suffix not in EXTENSIONS:
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


def main() -> None:
    parser = argparse.ArgumentParser(description="Scan for data module review signals.")
    parser.add_argument("root", nargs="?", default=".", help="Repository root")
    args = parser.parse_args()
    root = Path(args.root).resolve()

    print(f"# Data Risk Scan: {root}")
    print()
    print("| Category | File | Line | Signal |")
    print("| --- | --- | ---: | --- |")
    for category, path, line_no, line in scan(root):
        rel = path.relative_to(root)
        safe_line = line.replace("|", "\\|")
        print(f"| {category} | `{rel}` | {line_no} | `{safe_line[:120]}` |")


if __name__ == "__main__":
    main()
