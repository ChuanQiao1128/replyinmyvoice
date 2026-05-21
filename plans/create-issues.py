#!/usr/bin/env python3
"""
One-shot helper to create the commercialization roadmap on GitHub.
- Creates 8 milestones (M0-Stabilize through M7-Launch) on ChuanQiao1128/replyinmyvoice
- Creates 6 detailed M0 issues from plans/issues/M0-*.md
- Creates 69 M1-M7 issues parsed from plans/issue-manifest.md
- Writes plans/issue-board.md (the supervisor state file)
- Writes plans/issue-creation-report.md (run summary)

Idempotent: existing milestones and issues with same title are skipped.

Usage:
    python3 plans/create-issues.py

Requirements: gh CLI authenticated for ChuanQiao1128/replyinmyvoice.
"""

from __future__ import annotations

import json
import re
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

REPO = "ChuanQiao1128/replyinmyvoice"
ROOT = Path(__file__).resolve().parent.parent  # /Users/qc/Desktop/CloudFlare
PLANS = ROOT / "plans"
ISSUES_DIR = PLANS / "issues"
MANIFESTS = [
    PLANS / "issue-manifest.md",
    PLANS / "issue-manifest-additions.md",
]
BOARD = PLANS / "issue-board.md"
REPORT = PLANS / "issue-creation-report.md"

MILESTONES = [
    ("M0-Stabilize", "Stabilize working tree; clean baseline for the rest of the roadmap"),
    ("M1-Entra", "Complete Clerk → Microsoft Entra External ID migration"),
    ("M2-Quality", "Rewrite quality gate — never return a worse-than-draft rewrite"),
    ("M2.5-Learning", "Continuous rewrite-quality improvement loop"),
    ("M3-V2", "Workspace V2 — 5 scenarios + 4 tones + 5000-char cap"),
    ("M4-Landing", "Landing + legal polish for live commercial launch"),
    ("M5-Telemetry", "Per-request cost telemetry + admin dashboard"),
    ("M6-Verify", "Production verification — Worker secrets, domain, smokes"),
    ("M7-Launch", "Launch day + growth — real-money smoke, analytics, support"),
    ("M8-API", "B2B API surface with tiered subscriptions"),
    ("M9-Distribution", "MCP server + Claude Code Skill for LLM-tool integration"),
]


def run(cmd: list[str], check: bool = True, capture: bool = True) -> subprocess.CompletedProcess:
    """Run a subprocess and return result. Raises on non-zero if check=True."""
    return subprocess.run(
        cmd,
        check=check,
        capture_output=capture,
        text=True,
    )


def gh_auth_check() -> None:
    """Verify gh CLI auth before doing anything."""
    try:
        result = run(["gh", "auth", "status"], check=False)
        if result.returncode != 0:
            print("ERROR: gh CLI is not authenticated. Run `gh auth login` first.")
            print(result.stderr)
            sys.exit(1)
        user = run(["gh", "api", "user", "--jq", ".login"]).stdout.strip()
        print(f"gh authenticated as: {user}")
    except FileNotFoundError:
        print("ERROR: gh CLI not found in PATH.")
        sys.exit(1)


def get_existing_milestones() -> dict[str, int]:
    """Return {title: number} for milestones already on the repo."""
    result = run(
        [
            "gh",
            "api",
            f"repos/{REPO}/milestones",
            "--paginate",
        ]
    )
    data = json.loads(result.stdout)
    return {m["title"]: m["number"] for m in data}


def create_milestone(title: str, description: str) -> int:
    """Create a milestone, return its number."""
    result = run(
        [
            "gh",
            "api",
            f"repos/{REPO}/milestones",
            "-f",
            f"title={title}",
            "-f",
            f"description={description}",
        ]
    )
    data = json.loads(result.stdout)
    return data["number"]


def ensure_milestones() -> dict[str, int]:
    """Create any missing milestones. Returns full {title: number} map."""
    existing = get_existing_milestones()
    print(f"Existing milestones on repo: {len(existing)}")
    for title, desc in MILESTONES:
        if title in existing:
            print(f"  reuse: {title} (#{existing[title]})")
            continue
        num = create_milestone(title, desc)
        existing[title] = num
        print(f"  created: {title} (#{num})")
    return existing


def get_existing_issue_titles() -> set[str]:
    """Return titles of all open + closed issues on the repo."""
    result = run(
        [
            "gh",
            "issue",
            "list",
            "--repo",
            REPO,
            "--state",
            "all",
            "--limit",
            "500",
            "--json",
            "title",
        ]
    )
    data = json.loads(result.stdout)
    return {item["title"] for item in data}


def extract_m0_title(file_path: Path) -> str:
    """First line of an M0 brief is `# <title>`."""
    first_line = file_path.read_text().splitlines()[0]
    return first_line.lstrip("#").strip()


def create_issue(title: str, body: str, milestone: str) -> str:
    """Create one issue. Returns the GitHub URL."""
    # Write body to a temp file because --body-file is safer than --body for long content
    tmp = ROOT / ".issue-body.tmp.md"
    tmp.write_text(body)
    try:
        result = run(
            [
                "gh",
                "issue",
                "create",
                "--repo",
                REPO,
                "--milestone",
                milestone,
                "--title",
                title,
                "--body-file",
                str(tmp),
            ]
        )
        url = result.stdout.strip().splitlines()[-1]  # gh prints the URL on the last line
        return url
    finally:
        if tmp.exists():
            tmp.unlink()


def parse_manifest(text: str) -> list[dict]:
    """
    Parse plans/issue-manifest.md.
    Returns list of {id, milestone, title, body} dicts in document order.
    """
    issues: list[dict] = []
    current_milestone: str | None = None

    # Split by lines for stateful parsing
    lines = text.splitlines()
    i = 0
    n = len(lines)
    while i < n:
        line = lines[i]

        # Milestone header
        m = re.match(r"^## Milestone:\s*(\S+)\s*$", line)
        if m:
            current_milestone = m.group(1)
            i += 1
            continue

        # Issue header
        m = re.match(r"^###\s+(M[0-9]+-\d+)\s*$", line)
        if m and current_milestone:
            issue_id = m.group(1)
            i += 1

            # Expect: title: ...
            title = ""
            while i < n:
                ln = lines[i]
                tm = re.match(r"^title:\s*(.+)$", ln)
                if tm:
                    title = tm.group(1).strip()
                    i += 1
                    break
                if ln.strip() == "":
                    i += 1
                    continue
                break

            # Expect: body:
            while i < n and not re.match(r"^body:\s*$", lines[i]):
                i += 1
            if i < n:
                i += 1  # past "body:" line

            # Collect `> ` lines until next `###` or `##` or `---` or EOF
            body_lines: list[str] = []
            while i < n:
                ln = lines[i]
                if ln.startswith("### ") or ln.startswith("## ") or ln.strip() == "---":
                    break
                if ln.startswith("> "):
                    body_lines.append(ln[2:])
                elif ln.strip() == ">":
                    body_lines.append("")
                elif ln.strip() == "":
                    # blank line outside `>` — break the body
                    break
                else:
                    # non-> non-blank line: stop the body
                    break
                i += 1

            body = "\n".join(body_lines).strip()
            if not body:
                body = "(See plans/commercialization-roadmap.md for context.)"
            footer = (
                f"\n\n---\nDetailed brief will be written at `plans/issues/{issue_id}.md` "
                f"when this milestone starts.\nSource roadmap: `plans/commercialization-roadmap.md`."
            )
            issues.append(
                {
                    "id": issue_id,
                    "milestone": current_milestone,
                    "title": title,
                    "body": body + footer,
                }
            )
            continue

        i += 1

    return issues


def main() -> int:
    started_at = datetime.now(timezone.utc)
    print(f"=== Roadmap issue creation @ {started_at.isoformat()} ===\n")

    # 1. Auth check
    gh_auth_check()

    # 2. Milestones
    print("\n--- Milestones ---")
    milestone_numbers = ensure_milestones()

    # 3. Existing issue titles (for dedupe)
    print("\n--- Existing issue scan ---")
    existing_titles = get_existing_issue_titles()
    print(f"Existing issues on repo: {len(existing_titles)}")

    created: list[dict] = []
    skipped: list[dict] = []
    errors: list[dict] = []

    # 4. M0 detailed issues (6 files)
    print("\n--- M0 issues (detailed briefs) ---")
    m0_files = sorted(ISSUES_DIR.glob("M0-*.md"))
    for fp in m0_files:
        title = extract_m0_title(fp)
        if title in existing_titles:
            print(f"  skip (dup): {title}")
            skipped.append({"id": fp.stem, "title": title, "reason": "duplicate"})
            continue
        body = fp.read_text()
        try:
            url = create_issue(title, body, "M0-Stabilize")
            created.append(
                {"id": fp.stem, "title": title, "milestone": "M0-Stabilize", "url": url}
            )
            existing_titles.add(title)
            print(f"  created: {fp.stem} -> {url}")
            time.sleep(0.5)  # gentle pacing to avoid abuse rate-limit
        except subprocess.CalledProcessError as e:
            errors.append({"id": fp.stem, "title": title, "error": e.stderr or str(e)})
            print(f"  ERROR: {fp.stem}: {e.stderr.strip() if e.stderr else e}")

    # 5. M1-M9 manifest issues (parse all manifest files in order)
    print("\n--- M1-M9 issues (manifest entries) ---")
    parsed: list[dict] = []
    for manifest_path in MANIFESTS:
        if not manifest_path.exists():
            print(f"  skip: {manifest_path} does not exist")
            continue
        text = manifest_path.read_text()
        entries = parse_manifest(text)
        print(f"  {manifest_path.name}: {len(entries)} entries")
        parsed.extend(entries)
    print(f"Parsed {len(parsed)} entries total")

    for entry in parsed:
        if entry["title"] in existing_titles:
            print(f"  skip (dup): {entry['id']} — {entry['title']}")
            skipped.append({"id": entry["id"], "title": entry["title"], "reason": "duplicate"})
            continue
        try:
            url = create_issue(entry["title"], entry["body"], entry["milestone"])
            created.append(
                {
                    "id": entry["id"],
                    "title": entry["title"],
                    "milestone": entry["milestone"],
                    "url": url,
                }
            )
            existing_titles.add(entry["title"])
            print(f"  created: {entry['id']} -> {url}")
            time.sleep(0.5)
        except subprocess.CalledProcessError as e:
            errors.append(
                {"id": entry["id"], "title": entry["title"], "error": e.stderr or str(e)}
            )
            print(f"  ERROR: {entry['id']}: {e.stderr.strip() if e.stderr else e}")

    # 6. Write issue board
    print("\n--- Writing issue-board.md ---")
    finished_at = datetime.now(timezone.utc)
    all_rows = created + skipped
    # Sort by milestone order then id
    milestone_order = {name: i for i, (name, _) in enumerate(MILESTONES)}
    all_rows.sort(key=lambda r: (milestone_order.get(r.get("milestone", "M0-Stabilize"), 99), r["id"]))

    board_lines = [
        "# Issue Board — Commercialization Roadmap",
        "",
        f"Last updated: {finished_at.isoformat()}",
        f"Total created this run: {len(created)}",
        f"Total skipped (duplicate): {len(skipped)}",
        f"Total errored: {len(errors)}",
        "",
        "## Supervisor loop",
        "Pick the next `pending` issue with the lowest M-number and lowest id-number.",
        "Update `status` as work progresses: `pending` → `in_progress` → `review` → `done` | `blocked`.",
        "",
        "## Issues",
        "",
        "| ID | Milestone | Title | GitHub | Status |",
        "|---|---|---|---|---|",
    ]
    for row in all_rows:
        url = row.get("url", "(skipped)")
        title = row["title"].replace("|", "\\|")
        board_lines.append(
            f"| {row['id']} | {row.get('milestone', '?')} | {title} | {url} | pending |"
        )

    if errors:
        board_lines.append("")
        board_lines.append("## Errors")
        for e in errors:
            board_lines.append(f"- **{e['id']}** ({e['title']}): {e['error']}")

    BOARD.write_text("\n".join(board_lines) + "\n")
    print(f"Wrote {BOARD}")

    # 7. Run report
    report_lines = [
        "# Issue Creation Report",
        "",
        f"Run started: {started_at.isoformat()}",
        f"Run finished: {finished_at.isoformat()}",
        f"Duration: {(finished_at - started_at).total_seconds():.1f}s",
        f"Repo: {REPO}",
        "",
        "## Milestones",
    ]
    for title, _ in MILESTONES:
        num = milestone_numbers.get(title, "?")
        report_lines.append(f"- {title} (#{num})")
    report_lines += [
        "",
        f"## Issues created: {len(created)}",
        f"## Issues skipped (duplicate title): {len(skipped)}",
        f"## Issues errored: {len(errors)}",
        "",
        f"Milestones page: https://github.com/{REPO}/milestones",
        f"Issues page: https://github.com/{REPO}/issues",
    ]
    if errors:
        report_lines.append("")
        report_lines.append("## Errors")
        for e in errors:
            report_lines.append(f"- **{e['id']}** ({e['title']}): {e['error']}")

    REPORT.write_text("\n".join(report_lines) + "\n")
    print(f"Wrote {REPORT}")

    print(f"\n=== Done in {(finished_at - started_at).total_seconds():.1f}s ===")
    print(f"  Created: {len(created)}")
    print(f"  Skipped: {len(skipped)}")
    print(f"  Errored: {len(errors)}")

    return 0 if not errors else 2


if __name__ == "__main__":
    sys.exit(main())
