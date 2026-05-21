#!/usr/bin/env node
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, rmSync, statSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(HERE, "..");
const SKILL_DIR = resolve(REPO_ROOT, "agent-skills/replyinmyvoice-rewrite");
const SKILL_FILE = resolve(SKILL_DIR, "SKILL.md");
const DIST_DIR = resolve(REPO_ROOT, "dist");
const OUT_FILE = resolve(DIST_DIR, "replyinmyvoice-rewrite.skill");

function fail(msg) {
  console.error("package-skill: " + msg);
  process.exit(1);
}

if (!existsSync(SKILL_DIR) || !statSync(SKILL_DIR).isDirectory()) fail("skill directory missing");
if (!existsSync(SKILL_FILE)) fail("SKILL.md missing");

mkdirSync(DIST_DIR, { recursive: true });
if (existsSync(OUT_FILE)) rmSync(OUT_FILE);

const skillParent = dirname(SKILL_DIR);
const skillBase = SKILL_DIR.slice(skillParent.length + 1);
execFileSync("zip", ["-rq", OUT_FILE, skillBase], { cwd: skillParent, stdio: "inherit" });
console.log("package-skill: wrote " + OUT_FILE + " (" + statSync(OUT_FILE).size + " bytes)");
