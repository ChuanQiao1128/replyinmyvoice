import type { RewriteRequestInput } from "./validation";
import type { TonePreset } from "./rewrite-presets";
import { tonePresetToTone } from "./rewrite-presets";

export type RewriteEmailEvalMode = "smoke" | "focused" | "full";
export type RewriteDraftSourceType = "email" | "message" | "note" | "announcement";

export type RewriteEmailEvalCasePlanRow = {
  caseNumber: number;
  id: string;
  category: string;
  sourceType: RewriteDraftSourceType;
  wordBand: string;
  primaryFailureMode: string;
  secondaryFailureMode: string;
  mustIncludeFactTypes: string[];
  riskTags: string[];
  complexity: string;
  tonePreset: TonePreset;
};

export type RewriteEmailEvalCase = {
  caseNumber: number;
  title: string;
  id: string;
  category: string;
  sourceType: RewriteDraftSourceType;
  riskTags: string[];
  tonePreset: TonePreset;
  inputWordCountBand: string;
  inputDraft: string;
  whatHappened: string;
  mustKeep: string[];
  mustNotClaim: string[];
  rewriteQualityTargets: string;
  expectedRewriteChallenges: string;
};

const REQUIRED_INLINE_FIELDS = [
  "id",
  "category",
  "source_type",
  "tone_preset",
  "risk_tags",
  "input_word_count_band",
] as const;

const REQUIRED_TEXT_SECTIONS = [
  "input_draft",
  "what_actually_happened",
  "rewrite_quality_targets",
  "expected_rewrite_challenges",
] as const;

const REQUIRED_LIST_SECTIONS = ["must_keep", "must_not_claim"] as const;

function readField(
  fields: Map<string, string>,
  field: (typeof REQUIRED_INLINE_FIELDS)[number],
  caseNumber: number,
) {
  const value = fields.get(field);
  if (!value) {
    throw new Error(`Case ${caseNumber} is missing ${field}.`);
  }

  return value;
}

function parseSections(body: string, caseNumber: number) {
  const sectionPattern = /^#### ([a-z_]+)\s*$/gm;
  const headers = [...body.matchAll(sectionPattern)];
  const sections = new Map<string, string>();

  for (const [index, header] of headers.entries()) {
    const name = header[1];
    const contentStart = (header.index ?? 0) + header[0].length;
    const contentEnd =
      index + 1 < headers.length ? headers[index + 1].index ?? body.length : body.length;
    sections.set(name, body.slice(contentStart, contentEnd).trim());
  }

  for (const section of [...REQUIRED_TEXT_SECTIONS, ...REQUIRED_LIST_SECTIONS]) {
    if (!sections.get(section)) {
      throw new Error(`Case ${caseNumber} is missing ${section}.`);
    }
  }

  return sections;
}

function readSection(
  sections: Map<string, string>,
  section: (typeof REQUIRED_TEXT_SECTIONS)[number],
  caseNumber: number,
) {
  const value = sections.get(section);
  if (!value) {
    throw new Error(`Case ${caseNumber} is missing ${section}.`);
  }

  return value;
}

function readListSection(
  sections: Map<string, string>,
  section: (typeof REQUIRED_LIST_SECTIONS)[number],
  caseNumber: number,
) {
  const value = sections.get(section);
  if (!value) {
    throw new Error(`Case ${caseNumber} is missing ${section}.`);
  }

  const items = value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.startsWith("- "))
    .map((line) => line.slice(2).trim())
    .filter(Boolean);

  if (items.length === 0) {
    throw new Error(`Case ${caseNumber} has an empty ${section} list.`);
  }

  return items;
}

const bannedSubstrings = [
  "det" + "ector " + "by" + "pass",
  "det" + "ector-" + "by" + "pass",
  "un" + "detect" + "able",
  "human" + "izer",
  "score hacking",
  "score-hacking",
  "by" + "pass " + "det" + "ector",
  "eva" + "de " + "det" + "ector",
];

function wordCount(value: string) {
  return value.trim().split(/\s+/).filter(Boolean).length;
}

function parseSourceType(value: string, caseNumber: number): RewriteDraftSourceType {
  if (
    value === "email" ||
    value === "message" ||
    value === "note" ||
    value === "announcement"
  ) {
    return value;
  }

  throw new Error(`Case ${caseNumber} has unsupported source_type: ${value}.`);
}

function parseTonePreset(value: string, caseNumber: number): TonePreset {
  if (/^warm$/i.test(value)) {
    return "Warm";
  }

  throw new Error(`Case ${caseNumber} must use tone_preset: warm.`);
}

function assertNoBannedSubstrings(value: string, label: string) {
  const normalized = value.toLowerCase();
  const found = bannedSubstrings.find((term) => normalized.includes(term));
  if (found) {
    throw new Error(`${label} contains banned positioning term: ${found}.`);
  }
}

function assertSequentialCaseNumbers(cases: Array<{ caseNumber: number }>) {
  cases.forEach((sample, index) => {
    const expected = index + 1;
    if (sample.caseNumber !== expected) {
      throw new Error(
        `Materialized case numbers must be sequential; expected ${expected}, found ${sample.caseNumber}.`,
      );
    }
  });
}

function assertCaseLists(sample: RewriteEmailEvalCase) {
  if (sample.mustKeep.length < 6 || sample.mustKeep.length > 12) {
    throw new Error(
      `Case ${sample.caseNumber} must_keep must contain 6-12 bullets; found ${sample.mustKeep.length}.`,
    );
  }

  if (sample.mustNotClaim.length < 2 || sample.mustNotClaim.length > 5) {
    throw new Error(
      `Case ${sample.caseNumber} must_not_claim must contain 2-5 bullets; found ${sample.mustNotClaim.length}.`,
    );
  }

  for (const claim of sample.mustNotClaim) {
    if (!/^Do not\b/.test(claim)) {
      throw new Error(
        `Case ${sample.caseNumber} must_not_claim entries must start with "Do not".`,
      );
    }
  }
}

function assertInputDraftWordCount(sample: RewriteEmailEvalCase) {
  const count = wordCount(sample.inputDraft);
  if (count < 40 || count > 400) {
    throw new Error(
      `Case ${sample.caseNumber} input_draft must be 40-400 words; found ${count}.`,
    );
  }
}

export function parseRewriteEmailEvalCases(
  markdown: string,
): RewriteEmailEvalCase[] {
  const headerPattern = /^### Case (\d{3}) - (.+)$/gm;
  const headers = [...markdown.matchAll(headerPattern)];

  const cases = headers.map((header, index) => {
    const caseNumber = Number(header[1]);
    const title = header[2].trim();
    const bodyStart = (header.index ?? 0) + header[0].length;
    const bodyEnd =
      index + 1 < headers.length ? headers[index + 1].index ?? markdown.length : markdown.length;
    const body = markdown.slice(bodyStart, bodyEnd);
    const fields = new Map<string, string>();
    const sections = parseSections(body, caseNumber);

    for (const line of body.split(/\r?\n/)) {
      const match = line.match(/^- ([a-z_]+):\s*(.*)$/);
      if (match) {
        fields.set(match[1], match[2].trim());
      }
    }

    for (const field of REQUIRED_INLINE_FIELDS) {
      readField(fields, field, caseNumber);
    }

    const category = readField(fields, "category", caseNumber);
    const inputDraft = readSection(sections, "input_draft", caseNumber);
    const tonePreset = parseTonePreset(
      readField(fields, "tone_preset", caseNumber),
      caseNumber,
    );

    const sample: RewriteEmailEvalCase = {
      caseNumber,
      title,
      id: readField(fields, "id", caseNumber),
      category,
      sourceType: parseSourceType(readField(fields, "source_type", caseNumber), caseNumber),
      riskTags: readField(fields, "risk_tags", caseNumber)
        .split(",")
        .map((tag) => tag.trim())
        .filter(Boolean),
      tonePreset,
      inputWordCountBand: readField(fields, "input_word_count_band", caseNumber),
      inputDraft,
      whatHappened: readSection(sections, "what_actually_happened", caseNumber),
      mustKeep: readListSection(sections, "must_keep", caseNumber),
      mustNotClaim: readListSection(sections, "must_not_claim", caseNumber),
      rewriteQualityTargets: readSection(
        sections,
        "rewrite_quality_targets",
        caseNumber,
      ),
      expectedRewriteChallenges: readSection(
        sections,
        "expected_rewrite_challenges",
        caseNumber,
      ),
    };

    assertCaseLists(sample);
    assertInputDraftWordCount(sample);
    assertNoBannedSubstrings(body, `Case ${caseNumber}`);

    return sample;
  });

  assertSequentialCaseNumbers(cases);
  return cases;
}

export function selectRewriteEmailEvalCases(
  cases: RewriteEmailEvalCase[],
  mode: RewriteEmailEvalMode,
) {
  const limit = mode === "smoke" ? 10 : mode === "focused" ? 40 : 100;

  return cases.slice(0, Math.min(limit, cases.length));
}

function parseMarkdownTableRow(line: string) {
  const trimmed = line.trim();
  if (!trimmed.startsWith("|") || !trimmed.endsWith("|")) {
    return null;
  }

  const cells = trimmed
    .slice(1, -1)
    .split("|")
    .map((cell) => cell.trim());

  if (cells.every((cell) => /^:?-{3,}:?$/.test(cell))) {
    return null;
  }

  return cells;
}

function splitCommaList(value: string) {
  return value
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

export function parseRewriteEmailEvalCasePlan(
  markdown: string,
): RewriteEmailEvalCasePlanRow[] {
  const rows: RewriteEmailEvalCasePlanRow[] = [];
  let headers: string[] | null = null;

  for (const line of markdown.split(/\r?\n/)) {
    const cells = parseMarkdownTableRow(line);
    if (!cells) {
      continue;
    }

    if (cells[0] === "case") {
      headers = cells;
      continue;
    }

    if (!headers || cells.length !== headers.length || !/^\d{3}$/.test(cells[0])) {
      continue;
    }

    const row = new Map(headers.map((header, index) => [header, cells[index] ?? ""]));
    const caseNumber = Number(row.get("case"));
    const tonePreset = parseTonePreset(row.get("tone_preset") ?? "", caseNumber);

    rows.push({
      caseNumber,
      id: row.get("id") ?? "",
      category: row.get("category") ?? "",
      sourceType: parseSourceType(row.get("source_type") ?? "", caseNumber),
      wordBand: row.get("word_band") ?? "",
      primaryFailureMode: row.get("primary_failure_mode") ?? "",
      secondaryFailureMode: row.get("secondary_failure_mode") ?? "",
      mustIncludeFactTypes: splitCommaList(row.get("must_include_fact_types") ?? ""),
      riskTags: splitCommaList(row.get("risk_tags") ?? ""),
      complexity: row.get("complexity") ?? "",
      tonePreset,
    });
  }

  if (rows.length !== 100) {
    throw new Error(`Case plan must contain exactly 100 rows; found ${rows.length}.`);
  }

  rows.forEach((row, index) => {
    const expectedCaseNumber = index + 1;
    const expectedId = `rewrite-draft-${String(expectedCaseNumber).padStart(3, "0")}`;
    if (row.caseNumber !== expectedCaseNumber || row.id !== expectedId) {
      throw new Error(
        `Case plan row ${index + 1} must be ${expectedId}; found ${row.id}.`,
      );
    }
  });

  return rows;
}

export function validateRewriteEmailEvalCorpus(markdown: string) {
  const plan = parseRewriteEmailEvalCasePlan(markdown);
  const cases = parseRewriteEmailEvalCases(markdown);
  const plannedIds = new Set(plan.map((row) => row.id));

  for (const sample of cases) {
    if (!plannedIds.has(sample.id)) {
      throw new Error(`Materialized case ${sample.id} is not present in the case plan.`);
    }
  }

  assertNoBannedSubstrings(markdown, "Corpus");

  return {
    planRows: plan.length,
    materializedCases: cases.length,
  };
}

export function rewriteEmailEvalCaseToRequestInput(
  sample: RewriteEmailEvalCase,
): RewriteRequestInput {
  return {
    scenario: "General reply",
    messageToReplyTo: "",
    roughDraftReply: sample.inputDraft,
    audience: "",
    purpose: "",
    whatHappened: "",
    factsToPreserve: "",
    tone: tonePresetToTone(sample.tonePreset),
    tonePreset: sample.tonePreset,
  };
}
