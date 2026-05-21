#!/usr/bin/env python3
"""
Self-contained roadmap issue creation via direct GitHub REST API (no gh CLI).
Reads PAT from .env.local (any of GH_TOKEN / GITHUB_TOKEN / GITHUB_PAT).
Creates 8 milestones + ~113 issues on ChuanQiao1128/replyinmyvoice.
Idempotent: skips milestones/issues whose title already exists.
Writes plans/issue-board.md + plans/issue-creation-report.md.

Usage: python3 plans/create-issues-direct.py
"""

from __future__ import annotations

import json
import os
import re
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

REPO = "ChuanQiao1128/replyinmyvoice"
API_BASE = "https://api.github.com"
ROOT = Path(__file__).resolve().parent.parent
PLANS = ROOT / "plans"
ISSUES_DIR = PLANS / "issues"
MANIFESTS = [PLANS / "issue-manifest.md", PLANS / "issue-manifest-additions.md"]
BOARD = PLANS / "issue-board.md"
REPORT = PLANS / "issue-creation-report.md"
ENV_FILE = ROOT / ".env.local"

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


def load_pat() -> str:
    """Load PAT from env or .env.local. Tries GH_TOKEN, GITHUB_TOKEN, GITHUB_PAT."""
    for env_name in ("GH_TOKEN", "GITHUB_TOKEN", "GITHUB_PAT"):
        v = os.environ.get(env_name)
        if v and v.strip():
            return v.strip()
    if not ENV_FILE.exists():
        sys.exit("ERROR: no PAT in env and .env.local does not exist")
    candidates = ("GH_TOKEN", "GITHUB_TOKEN", "GITHUB_PAT")
    for line in ENV_FILE.read_text().splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        name, _, value = line.partition("=")
        name = name.strip()
        value = value.strip().strip('"').strip("'")
        if name in candidates and value:
            return value
    sys.exit("ERROR: no GH_TOKEN/GITHUB_TOKEN/GITHUB_PAT found in .env.local")


PAT = ""  # set in main()


def api_request(method: str, path: str, body: dict | None = None, retries: int = 3) -> dict | list:
    """
    Make a GitHub REST API request via curl (system curl has CA certs, urllib doesn't on stock macOS).
    Returns parsed JSON dict/list (or {} on empty body).
    """
    url = f"{API_BASE}{path}"
    cmd = [
        "curl", "-sS", "-X", method,
        "-H", f"Authorization: Bearer {PAT}",
        "-H", "Accept: application/vnd.github+json",
        "-H", "X-GitHub-Api-Version: 2022-11-28",
        "-H", "User-Agent: replyinmyvoice-roadmap-creator/1.0",
        "-w", "\n%{http_code}",  # append HTTP status as last line
        url,
    ]
    if body is not None:
        cmd.extend(["-H", "Content-Type: application/json", "-d", json.dumps(body)])

    last_err = None
    for attempt in range(retries):
        result = subprocess.run(cmd, capture_output=True, text=True)
        if result.returncode != 0:
            last_err = f"curl exit {result.returncode}: {result.stderr.strip()}"
            wait = 2 ** attempt + 1
            print(f"  retry after {wait}s (curl error): {result.stderr.strip()[:120]}")
            time.sleep(wait)
            continue
        out = result.stdout
        # Last non-empty line is the HTTP status code (because of -w)
        lines = out.rstrip("\n").split("\n")
        if len(lines) < 1:
            last_err = "empty curl response"
            time.sleep(2 ** attempt + 1)
            continue
        try:
            status = int(lines[-1])
        except ValueError:
            last_err = f"could not parse status from '{lines[-1]}'"
            time.sleep(2 ** attempt + 1)
            continue
        body_text = "\n".join(lines[:-1])

        if 200 <= status < 300:
            if not body_text.strip():
                return {}
            try:
                return json.loads(body_text)
            except json.JSONDecodeError as e:
                raise RuntimeError(f"JSON parse failed on {method} {path}: {e} | body={body_text[:200]}")
        if status == 422:
            raise RuntimeError(f"GitHub 422 on {method} {path}: {body_text[:300]}")
        if status in (429, 502, 503, 504):
            wait = 2 ** attempt + 1
            print(f"  retry after {wait}s (HTTP {status}): {body_text[:100]}")
            time.sleep(wait)
            continue
        raise RuntimeError(f"GitHub {status} on {method} {path}: {body_text[:300]}")
    raise RuntimeError(f"All retries failed for {method} {path}: {last_err}")


def auth_check() -> None:
    try:
        user = api_request("GET", "/user")
    except RuntimeError as e:
        sys.exit(f"ERROR: PAT auth failed: {e}")
    login = user.get("login", "?") if isinstance(user, dict) else "?"
    print(f"PAT authenticated as: {login}")


def list_milestones() -> dict[str, int]:
    """Return {title: number} for all milestones."""
    result = {}
    page = 1
    while True:
        items = api_request("GET", f"/repos/{REPO}/milestones?state=all&per_page=100&page={page}")
        if not items:
            break
        for m in items:
            result[m["title"]] = m["number"]
        if len(items) < 100:
            break
        page += 1
    return result


def create_milestone(title: str, description: str) -> int:
    m = api_request("POST", f"/repos/{REPO}/milestones",
                    body={"title": title, "description": description})
    return m["number"]


def list_issue_titles() -> set[str]:
    """All open + closed issue titles."""
    titles = set()
    page = 1
    while True:
        items = api_request("GET",
                            f"/repos/{REPO}/issues?state=all&per_page=100&page={page}")
        if not items:
            break
        for it in items:
            # /issues includes pull requests; skip them
            if "pull_request" in it:
                continue
            titles.add(it["title"])
        if len(items) < 100:
            break
        page += 1
    return titles


def create_issue(title: str, body: str, milestone_number: int) -> str:
    """Create one issue. Returns html_url."""
    payload = {"title": title, "body": body, "milestone": milestone_number}
    issue = api_request("POST", f"/repos/{REPO}/issues", body=payload)
    return issue["html_url"]


def extract_m0_title(path: Path) -> str:
    return path.read_text().splitlines()[0].lstrip("#").strip()


def parse_manifest(text: str) -> list[dict]:
    """Parse plans/issue-manifest*.md. Returns ordered list of issue dicts."""
    issues: list[dict] = []
    current_ms: str | None = None
    lines = text.splitlines()
    i, n = 0, len(lines)
    while i < n:
        line = lines[i]
        m = re.match(r"^## Milestone:\s*(\S+)\s*$", line)
        if m:
            current_ms = m.group(1)
            i += 1
            continue
        m = re.match(r"^###\s+(M[0-9]+(?:\.[0-9]+)?-\d+)\s*$", line)
        if m and current_ms:
            issue_id = m.group(1)
            i += 1
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
            while i < n and not re.match(r"^body:\s*$", lines[i]):
                i += 1
            if i < n:
                i += 1
            body_lines = []
            while i < n:
                ln = lines[i]
                if ln.startswith("### ") or ln.startswith("## ") or ln.strip() == "---":
                    break
                if ln.startswith("> "):
                    body_lines.append(ln[2:])
                elif ln.strip() == ">":
                    body_lines.append("")
                elif ln.strip() == "":
                    break
                else:
                    break
                i += 1
            body = "\n".join(body_lines).strip() or "(See plans/commercialization-roadmap.md)"
            footer = (f"\n\n---\nDetailed brief will be written at "
                      f"`plans/issues/{issue_id}.md` when this milestone starts.\n"
                      f"Source roadmap: `plans/commercialization-roadmap.md`.")
            issues.append({"id": issue_id, "milestone": current_ms, "title": title,
                           "body": body + footer})
            continue
        i += 1
    return issues


def main() -> int:
    global PAT
    started_at = datetime.now(timezone.utc)
    print(f"=== Roadmap creation @ {started_at.isoformat()} ===")
    PAT = load_pat()
    auth_check()

    print("\n--- Milestones ---")
    existing_ms = list_milestones()
    print(f"existing milestones: {len(existing_ms)}")
    milestone_numbers = dict(existing_ms)
    for title, desc in MILESTONES:
        if title in milestone_numbers:
            print(f"  reuse: {title} (#{milestone_numbers[title]})")
            continue
        num = create_milestone(title, desc)
        milestone_numbers[title] = num
        print(f"  created: {title} (#{num})")
        time.sleep(0.3)

    print("\n--- Existing issues scan ---")
    existing_titles = list_issue_titles()
    print(f"existing issue titles on repo: {len(existing_titles)}")

    created, skipped, errors = [], [], []

    print("\n--- M0 detailed issues ---")
    for fp in sorted(ISSUES_DIR.glob("M0-*.md")):
        title = extract_m0_title(fp)
        if title in existing_titles:
            print(f"  skip dup: {title}")
            skipped.append({"id": fp.stem, "title": title, "milestone": "M0-Stabilize"})
            continue
        try:
            url = create_issue(title, fp.read_text(), milestone_numbers["M0-Stabilize"])
            created.append({"id": fp.stem, "title": title, "milestone": "M0-Stabilize", "url": url})
            existing_titles.add(title)
            print(f"  created: {fp.stem} -> {url}")
            time.sleep(0.4)
        except Exception as e:
            errors.append({"id": fp.stem, "title": title, "error": str(e)})
            print(f"  ERROR: {fp.stem}: {e}")

    print("\n--- M1-M9 manifest issues ---")
    parsed = []
    for mf in MANIFESTS:
        if mf.exists():
            entries = parse_manifest(mf.read_text())
            print(f"  {mf.name}: {len(entries)} entries")
            parsed.extend(entries)
    print(f"total parsed: {len(parsed)}")

    for entry in parsed:
        if entry["title"] in existing_titles:
            print(f"  skip dup: {entry['id']} — {entry['title'][:60]}")
            skipped.append(entry)
            continue
        ms_num = milestone_numbers.get(entry["milestone"])
        if ms_num is None:
            errors.append({**entry, "error": f"milestone {entry['milestone']} not found"})
            print(f"  ERROR: {entry['id']}: milestone {entry['milestone']} not found")
            continue
        try:
            url = create_issue(entry["title"], entry["body"], ms_num)
            created.append({**entry, "url": url})
            existing_titles.add(entry["title"])
            print(f"  created: {entry['id']} -> {url}")
            time.sleep(0.4)
        except Exception as e:
            errors.append({**entry, "error": str(e)})
            print(f"  ERROR: {entry['id']}: {e}")

    # Write issue board
    finished_at = datetime.now(timezone.utc)
    all_rows = created + skipped
    ms_order = {name: i for i, (name, _) in enumerate(MILESTONES)}
    all_rows.sort(key=lambda r: (ms_order.get(r.get("milestone", "?"), 99), r["id"]))

    lines = [
        "# Issue Board — Commercialization Roadmap",
        "",
        f"Last updated: {finished_at.isoformat()}",
        f"Created this run: {len(created)} | Skipped (dup): {len(skipped)} | Errored: {len(errors)}",
        "",
        "## Supervisor loop",
        "Pick next `pending` with lowest M-number, lowest id. Update status: pending → in_progress → review → done | blocked.",
        "",
        "| ID | Milestone | Title | GitHub | Status |",
        "|---|---|---|---|---|",
    ]
    for row in all_rows:
        url = row.get("url", "(dup)")
        title_safe = row["title"].replace("|", "\\|")
        lines.append(f"| {row['id']} | {row.get('milestone','?')} | {title_safe} | {url} | pending |")
    if errors:
        lines += ["", "## Errors"]
        for e in errors:
            lines.append(f"- **{e['id']}** ({e.get('title','?')}): {e['error']}")
    BOARD.write_text("\n".join(lines) + "\n")
    print(f"\nWrote {BOARD}")

    # Run report
    rep = [
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
        n = milestone_numbers.get(title, "?")
        rep.append(f"- {title} (#{n})")
    rep += [
        "",
        f"## Created: {len(created)}",
        f"## Skipped (duplicate): {len(skipped)}",
        f"## Errored: {len(errors)}",
        "",
        f"Milestones: https://github.com/{REPO}/milestones",
        f"Issues:     https://github.com/{REPO}/issues",
    ]
    if errors:
        rep += ["", "## Errors"]
        for e in errors:
            rep.append(f"- {e['id']} ({e.get('title','?')}): {e['error']}")
    REPORT.write_text("\n".join(rep) + "\n")
    print(f"Wrote {REPORT}")
    print(f"\n=== Done in {(finished_at - started_at).total_seconds():.1f}s ===")
    print(f"  Created: {len(created)}  Skipped: {len(skipped)}  Errored: {len(errors)}")
    return 0 if not errors else 2


if __name__ == "__main__":
    sys.exit(main())
