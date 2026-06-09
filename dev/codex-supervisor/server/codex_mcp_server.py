#!/usr/bin/env python3
"""
Codex-as-MCP wrapper.

Exposes Codex CLI's non-interactive `codex exec` mode as an MCP server,
so Claude (Code or Desktop) can delegate coding tasks to Codex via a
single MCP tool call.

Run manually for sanity check:
    OPENAI_API_KEY=sk-... python codex_mcp_server.py
(It will sit waiting for stdio MCP traffic — Ctrl+C to exit.)

Install deps once:
    pip install --user "mcp[cli]"
"""

from __future__ import annotations

import os
import shutil
import subprocess
from pathlib import Path
from typing import Optional

from mcp.server.fastmcp import FastMCP

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

# Absolute path to the codex binary. Override with CODEX_BIN env var if codex
# is not on PATH for the MCP host (Claude Desktop in particular launches with
# a minimal PATH).
CODEX_BIN = os.environ.get("CODEX_BIN") or shutil.which("codex") or "codex"

# Default working directory when the caller does not specify one. The MCP
# host launches us with cwd = its own working dir, which is usually wrong.
DEFAULT_CWD = os.environ.get("CODEX_DEFAULT_CWD") or os.getcwd()

# Hard timeout for any single codex exec call (seconds). Codex can chew a
# while on real tasks, so default generously.
DEFAULT_TIMEOUT = int(os.environ.get("CODEX_TIMEOUT", "900"))

# Banned terms enforced by AGENTS.md in the CloudFlare project — we surface
# them to Codex on every delegation as a hard constraint reminder.
BANNED_TERMS = ["humanizer", "bypass", "undetect", "detector", "evade"]

mcp = FastMCP("codex-supervisor")


# ---------------------------------------------------------------------------
# Tools
# ---------------------------------------------------------------------------


@mcp.tool()
def codex_exec(
    task: str,
    working_dir: Optional[str] = None,
    extra_context_files: Optional[list[str]] = None,
    timeout_seconds: Optional[int] = None,
) -> str:
    """Delegate a coding task to Codex CLI in non-interactive mode.

    Codex will receive `task` as its prompt, run with `working_dir` as cwd
    (so it can read/write files relative to your repo), and return its
    stdout + stderr + exit code.

    Args:
        task: Self-contained task brief. MUST include goal, files to touch
            (absolute paths), constraints, and acceptance criteria. Codex
            has no memory of the calling conversation.
        working_dir: Absolute path of the repo Codex should operate in.
            Defaults to the wrapper's launch cwd (set via CODEX_DEFAULT_CWD).
        extra_context_files: Optional list of absolute paths whose contents
            should be inlined into the prompt as additional context. Use
            sparingly to avoid token bloat.
        timeout_seconds: Override the default per-call timeout.

    Returns:
        A string containing exit code, stdout, and stderr from Codex.
    """
    cwd = working_dir or DEFAULT_CWD
    if not Path(cwd).is_dir():
        return f"ERROR: working_dir does not exist: {cwd}"

    prompt_parts = [
        task,
        "",
        "# Hard constraints (enforced by repo policy):",
        f"- Banned terms in user-facing copy AND in lib/**: {', '.join(BANNED_TERMS)}",
        "- Never print or commit secrets from .env.local, .dev.vars, or globalapikey/.",
        "- Do not run deployment commands (git push, wrangler deploy, etc.).",
    ]

    if extra_context_files:
        prompt_parts.append("")
        prompt_parts.append("# Inlined context files:")
        for p in extra_context_files:
            path = Path(p)
            if not path.is_file():
                prompt_parts.append(f"## {p}\n(file not found — ignore)")
                continue
            try:
                content = path.read_text(errors="replace")
            except Exception as e:
                prompt_parts.append(f"## {p}\n(read error: {e})")
                continue
            # cap each file to keep the prompt sane
            if len(content) > 30_000:
                content = content[:30_000] + "\n... [truncated]"
            prompt_parts.append(f"## {p}\n```\n{content}\n```")

    full_prompt = "\n".join(prompt_parts)

    try:
        result = subprocess.run(
            [CODEX_BIN, "exec", full_prompt],
            cwd=cwd,
            capture_output=True,
            text=True,
            timeout=timeout_seconds or DEFAULT_TIMEOUT,
            env={**os.environ},
        )
    except FileNotFoundError:
        return (
            f"ERROR: codex binary not found at '{CODEX_BIN}'. "
            "Set CODEX_BIN env var to the absolute path of `codex`."
        )
    except subprocess.TimeoutExpired:
        return (
            f"ERROR: codex exec timed out after "
            f"{timeout_seconds or DEFAULT_TIMEOUT}s. "
            "Consider splitting the task or increasing CODEX_TIMEOUT."
        )

    return (
        f"exit_code={result.returncode}\n"
        f"working_dir={cwd}\n\n"
        f"--- STDOUT ---\n{result.stdout}\n\n"
        f"--- STDERR ---\n{result.stderr}"
    )


@mcp.tool()
def codex_version() -> str:
    """Return the installed codex CLI version. Useful as a connectivity check."""
    try:
        result = subprocess.run(
            [CODEX_BIN, "--version"],
            capture_output=True,
            text=True,
            timeout=10,
        )
        return (
            f"codex_bin={CODEX_BIN}\n"
            f"default_cwd={DEFAULT_CWD}\n"
            f"stdout={result.stdout.strip()}\n"
            f"stderr={result.stderr.strip()}"
        )
    except FileNotFoundError:
        return f"ERROR: codex binary not found at '{CODEX_BIN}'."


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    mcp.run()
