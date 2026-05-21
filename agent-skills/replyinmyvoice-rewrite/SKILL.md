---
name: replyinmyvoice-rewrite
description: Use when the user wants to soften AI-style cadence in a draft email, message, or reply — phrasings such as "make this sound more natural", "lower the AI tone of this email", "run a naturalness check on this draft", "rewrite this draft so it reads like me", or "polish this so it sounds human". The skill calls the @replyinmyvoice/mcp-server tools to rewrite drafts and report a naturalness signal.
---

# Reply In My Voice — Rewrite Skill

Reply In My Voice rewrites drafts so they read naturally and reports a naturalness signal score. This skill routes such requests to the local `@replyinmyvoice/mcp-server` MCP server.

## When To Use

Trigger this skill when the user asks to:

- Rewrite a draft so it sounds more natural.
- Lower an AI-like cadence in their writing.
- Run a naturalness check on text.
- Polish an email, reply, or message before sending.
- Pick a scenario-appropriate rewrite (apology, follow-up, decline, etc.).

Do NOT trigger for general copy-editing, translation, summarization, or unrelated writing tasks — those are not what this skill is for.

## Prerequisites

1. `@replyinmyvoice/mcp-server` is installed and configured in the MCP client (Claude Code, Cursor, Continue.dev, or Codex). Install with:

   ```bash
   npx @replyinmyvoice/mcp-server
   ```

2. The user has set `REPLY_IN_MY_VOICE_API_KEY` in their MCP server environment. This is generated at `https://replyinmyvoice.com/app/api-keys`.

If either is missing, ask the user to set them up before continuing.

## Workflow

1. Listen for intent. The user pastes or describes a draft and asks for a more natural version, or asks for a naturalness check on existing text.
2. Pick the right tool from the MCP server:
   - `rewrite_email({ draft, scenario?, tone?, context? })` — returns a rewritten draft plus a naturalness signal.
   - `analyze_signal({ text })` — returns the naturalness signal only, no rewrite.
   - `list_scenarios()` — returns the supported scenarios when the user is unsure which to pick.
3. Default arguments. If the user did not specify a scenario or tone, omit them — the server will use its defaults. If the user mentioned a domain context (recipient, deadline, prior thread), pass it as `context`.
4. Present the result. Show the rewritten draft first. Then show the naturalness signal score with a one-line interpretation. Note any tradeoffs the server flagged (e.g., tone shift, length change).
5. Offer one follow-up at most. Examples: "Want me to try a more formal tone?", "Want a shorter version?". Do not loop unless the user asks.

## Examples

User: "Polish this so it sounds more natural: [draft]"
Assistant: calls `rewrite_email({ draft })`, returns rewrite + signal.

User: "Is this email going to read as AI-written?"
Assistant: calls `analyze_signal({ text })`, returns signal interpretation.

User: "What scenarios do you support?"
Assistant: calls `list_scenarios()`, lists them with one-line descriptions.

## Output Style

- Show the rewritten draft in a fenced block so the user can copy cleanly.
- Show the naturalness signal as a single line: `Signal: <score> (<interpretation>)`.
- Keep commentary short — the user wants the rewrite, not a critique.

## Constraints

- Never rewrite anything the user did not explicitly paste or reference.
- If `rewrite_email` returns an error or a low-confidence result, surface that plainly — do not paper over it.
- Do not invent a naturalness score; only report what the server returns.
