#!/usr/bin/env python3
"""Build a Claude Code planning handoff brief from repo context."""

from __future__ import annotations

import argparse
from pathlib import Path


SKIP_DIRS = {
    ".git",
    ".next",
    ".open-next",
    ".wrangler",
    "node_modules",
    "globalapikey",
    "bin",
    "obj",
    "dist",
}

IMPORTANT_NAMES = {
    "AGENTS.md",
    "README.md",
    "package.json",
    "wrangler.jsonc",
    "open-next.config.ts",
    "next.config.ts",
    "schema.prisma",
    "local-env.md",
}


def should_skip(path: Path) -> bool:
    return any(part in SKIP_DIRS for part in path.parts)


def collect_files(root: Path, limit: int) -> list[Path]:
    files: list[Path] = []
    for path in sorted(root.rglob("*")):
        if should_skip(path) or not path.is_file():
            continue
        if path.name.startswith("."):
            continue
        if path.name in IMPORTANT_NAMES or any(
            segment in {"app", "lib", "prisma", "docs", "backend-dotnet", "tests"}
            for segment in path.relative_to(root).parts
        ):
            files.append(path.relative_to(root))
        if len(files) >= limit:
            break
    return files


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate a Claude Code handoff brief.")
    parser.add_argument("objective", help="Planning objective")
    parser.add_argument("--root", default=".", help="Repository root")
    parser.add_argument("--limit", type=int, default=80, help="Maximum relevant files")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    files = collect_files(root, args.limit)

    print("# Claude Code Planning Handoff")
    print()
    print("## Objective")
    print()
    print(args.objective)
    print()
    print("## Repo Context")
    print()
    print(f"- Root: `{root}`")
    print("- Secrets were not read or printed by this script.")
    print()
    print("## Relevant Files")
    print()
    for file_path in files:
        print(f"- `{file_path}`")
    print()
    print("## Requested Claude Code Output")
    print()
    print("- Architecture plan")
    print("- Implementation phases")
    print("- Risk list")
    print("- Verification checklist")
    print()
    print("## Prompt To Paste Into Claude Code")
    print()
    print("```text")
    print(f"Use claude-heavy-planning-handoff to plan: {args.objective}")
    print("Read the listed project files first. Do not print secrets. Return a phased implementation plan with verification gates.")
    print("```")


if __name__ == "__main__":
    main()
