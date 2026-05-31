#!/usr/bin/env node

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const SAMPLE_AUTHORITY =
  "https://replyinmyvoice-test.ciamlogin.com/614ea821-0000-0000-0000-000000000000/v2.0";
const SAMPLE_API_SCOPE = "api://replyinmyvoice-api/access_as_user";
const SAMPLE_AUDIENCE = SAMPLE_API_SCOPE.replace(/\/[^/]+$/, "");
const SAMPLE_SCOPE = SAMPLE_API_SCOPE.split("/").at(-1);
const SAMPLE_EXP = 2_000_000_000;

function toBase64Url(value) {
  return Buffer.from(JSON.stringify(value), "utf8")
    .toString("base64")
    .replaceAll("+", "-")
    .replaceAll("/", "_")
    .replace(/=+$/u, "");
}

function fromBase64Url(value) {
  if (!/^[A-Za-z0-9_-]+$/u.test(value)) {
    throw new Error("JWT segment contains characters outside base64url.");
  }

  const padded = value.padEnd(value.length + ((4 - (value.length % 4)) % 4), "=");
  return Buffer.from(padded.replaceAll("-", "+").replaceAll("_", "/"), "base64").toString(
    "utf8",
  );
}

function parseJsonSegment(value, label) {
  try {
    return JSON.parse(fromBase64Url(value));
  } catch (error) {
    throw new Error(`Could not decode JWT ${label}: ${error.message}`);
  }
}

function decodeJwtForCompat(token) {
  const normalized = token.trim();
  const parts = normalized.split(".");

  if (parts.length !== 3 || parts.some((part) => part.length === 0)) {
    throw new Error("Expected a JWT with three non-empty dot-separated segments.");
  }

  return {
    header: parseJsonSegment(parts[0], "header"),
    payload: parseJsonSegment(parts[1], "payload"),
  };
}

function formatClaimValue(value) {
  if (Array.isArray(value)) {
    return value.join(" ");
  }

  if (value === undefined || value === null || value === "") {
    return "(missing)";
  }

  return String(value);
}

function formatExp(value) {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return formatClaimValue(value);
  }

  return `${value} (${new Date(value * 1000).toISOString()})`;
}

function formatDecodedClaims({ header, payload }) {
  const scopeClaimName = payload.scp === undefined && payload.scope !== undefined ? "scope" : "scp";
  const scopeValue = scopeClaimName === "scope" ? payload.scope : payload.scp;

  return [
    `alg: ${formatClaimValue(header.alg)}`,
    `aud: ${formatClaimValue(payload.aud)}`,
    `iss: ${formatClaimValue(payload.iss)}`,
    `${scopeClaimName}: ${formatClaimValue(scopeValue)}`,
    `exp: ${formatExp(payload.exp)}`,
  ].join("\n");
}

function buildSampleJwt() {
  const header = {
    alg: "RS256",
    typ: "JWT",
  };
  const payload = {
    aud: SAMPLE_AUDIENCE,
    iss: SAMPLE_AUTHORITY,
    scp: SAMPLE_SCOPE,
    exp: SAMPLE_EXP,
  };

  return `${toBase64Url(header)}.${toBase64Url(payload)}.sample-tail`;
}

function runSelfTest() {
  const sampleJwt = buildSampleJwt();
  const output = formatDecodedClaims(decodeJwtForCompat(sampleJwt));

  assert.match(output, new RegExp(escapeRegExp(SAMPLE_AUDIENCE), "u"));
  assert.match(output, new RegExp(escapeRegExp(SAMPLE_AUTHORITY), "u"));
  assert.match(output, new RegExp(`scp: ${escapeRegExp(SAMPLE_SCOPE)}`, "u"));
  assert.doesNotMatch(output, /eyJ[A-Za-z0-9_-]{40}/u);
  assert.doesNotMatch(output, /sample-tail/u);

  process.stdout.write(`${output}\n`);
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/gu, "\\$&");
}

function readTokenFromArgs(args) {
  const tokenFlagIndex = args.indexOf("--token");
  if (tokenFlagIndex !== -1) {
    const token = args[tokenFlagIndex + 1];
    if (!token || token.startsWith("--")) {
      throw new Error("Missing value after --token.");
    }
    return token;
  }

  if (!process.stdin.isTTY) {
    return readFileSync(0, "utf8").trim();
  }

  return "";
}

function printUsage() {
  console.error(`Usage:
  node scripts/verify-token-compat.mjs --token "<jwt>"
  printf "%s" "$ACCESS_TOKEN" | node scripts/verify-token-compat.mjs
  node scripts/verify-token-compat.mjs --self-test`);
}

function main() {
  const args = process.argv.slice(2);

  if (args.includes("--help") || args.includes("-h")) {
    printUsage();
    return;
  }

  if (args.includes("--self-test")) {
    runSelfTest();
    return;
  }

  const token = readTokenFromArgs(args);
  if (!token) {
    printUsage();
    process.exitCode = 2;
    return;
  }

  process.stdout.write(`${formatDecodedClaims(decodeJwtForCompat(token))}\n`);
}

try {
  main();
} catch (error) {
  console.error(`Error: ${error.message}`);
  process.exitCode = 1;
}
