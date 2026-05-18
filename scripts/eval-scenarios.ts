import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";

import { createRewritePlan } from "../lib/rewrite-diagnosis";
import { rewriteWithOptimization } from "../lib/rewrite";
import type { ScenarioOption, TonePreset } from "../lib/rewrite-presets";
import { tonePresetToTone } from "../lib/rewrite-presets";
import { rewriteRequestSchema } from "../lib/validation";

type EvalCase = {
  id: string;
  scenario: ScenarioOption;
  tonePreset: TonePreset;
  messageToReplyTo: string;
  roughDraftReply: string;
  expectedFacts: string[];
};

async function loadEnvLocal() {
  const envPath = path.join(process.cwd(), ".env.local");
  let content = "";

  try {
    content = await readFile(envPath, "utf8");
  } catch {
    return;
  }

  for (const line of content.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) {
      continue;
    }
    const match = trimmed.match(/^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/);
    if (!match) {
      continue;
    }
    const [, key, rawValue] = match;
    if (process.env[key]) {
      continue;
    }
    const value = rawValue
      .replace(/^['"]|['"]$/g, "")
      .replace(/\\n/g, "\n");
    process.env[key] = value;
  }
}

function points(value: number | null) {
  return value === null ? "unavailable" : `${value}%`;
}

function normalize(value: string) {
  return value.toLowerCase().replace(/\s+/g, " ").trim();
}

function factCheck(text: string, facts: string[]) {
  const normalized = normalize(text);
  const missing = facts.filter((fact) => !normalized.includes(normalize(fact)));

  return {
    passed: missing.length === 0,
    missing,
  };
}

const cases: EvalCase[] = [
  {
    id: "blank-01-partner-update",
    scenario: "Blank / custom",
    tonePreset: "Professional",
    messageToReplyTo: "",
    roughDraftReply:
      "I am writing to provide an update regarding the partner onboarding packet. The revised document has now been completed and is available for your review. Please note that section three contains the updated pricing language, and section five includes the implementation timeline that was discussed during the call on May 12. Kindly review the attached document and provide any feedback at your earliest convenience so that we may proceed accordingly.",
    expectedFacts: ["section three", "section five", "May 12"],
  },
  {
    id: "blank-02-community-note",
    scenario: "Blank / custom",
    tonePreset: "Warm",
    messageToReplyTo: "",
    roughDraftReply:
      "This message is to inform families that the Thursday workshop will be moved to Room 204 due to maintenance in the library. The start time remains 6:30pm, and the session will still cover scholarship forms, supporting documents, and the application timeline. We apologize for any inconvenience this change may cause and appreciate your understanding regarding the matter.",
    expectedFacts: ["Thursday", "Room 204", "6:30pm"],
  },
  {
    id: "blank-03-internal-note",
    scenario: "Blank / custom",
    tonePreset: "Concise",
    messageToReplyTo: "",
    roughDraftReply:
      "The purpose of this note is to summarize the current status of the vendor review. Vendor A has provided the revised security questionnaire, Vendor B is still waiting on legal approval, and Vendor C has requested an extension until Friday. Based on the current timeline, I recommend that we do not make a final decision until all three responses are available for comparison.",
    expectedFacts: ["Vendor A", "Vendor B", "Vendor C", "Friday"],
  },
  {
    id: "reply-01-teacher-extension",
    scenario: "Email or message reply",
    tonePreset: "Warm",
    messageToReplyTo:
      "Hi Professor, I missed the reflection deadline because of a family issue this week. I know the policy says late work may not be accepted, but is there any way I can still submit it before class tomorrow?",
    roughDraftReply:
      "Dear Student, I acknowledge receipt of your email regarding the missed reflection deadline. Late submissions are generally subject to the course policy and may not be accepted. However, I understand that you have indicated a family issue. I will review the situation and respond accordingly. Please be advised that submitting before class tomorrow does not guarantee that it will be accepted.",
    expectedFacts: ["family issue", "before class tomorrow", "course policy"],
  },
  {
    id: "reply-02-sales-followup",
    scenario: "Email or message reply",
    tonePreset: "Friendly",
    messageToReplyTo:
      "Thanks for sending the proposal. We like the reporting feature, but the team is comparing two other vendors and probably will not decide until next month.",
    roughDraftReply:
      "Hello Jordan, I am following up on our previous communication regarding the proposal. Please advise whether you would like to proceed with the proposal as discussed. We are happy to provide any additional information that may assist your decision-making process as you evaluate your options.",
    expectedFacts: ["Jordan", "reporting feature", "next month"],
  },
  {
    id: "reply-03-parent-question",
    scenario: "Email or message reply",
    tonePreset: "Professional",
    messageToReplyTo:
      "Hi, I saw Kai's grade dropped this week. He said he turned in everything except one exit ticket. Can you explain what happened?",
    roughDraftReply:
      "Thank you for reaching out regarding Kai's grade. I understand your concern. The grade change is due to two missing participation activities and one missing exit ticket. I can provide additional details if needed and would be happy to discuss this matter further at your earliest convenience.",
    expectedFacts: ["Kai", "two missing participation activities", "one missing exit ticket"],
  },
  {
    id: "support-01-priya-billing",
    scenario: "Customer support",
    tonePreset: "Friendly",
    messageToReplyTo:
      "Hi Reply In My Voice team, our usage report shows 18 active seats for May, but we only approved 15 regular seats. The invoice preview is NZD $126 higher than last month. We had three temporary contractors during the first week of May and they were supposed to be removed after the client handover on May 8. Can you confirm what changed and what we should do before the invoice is finalized? Thanks, Priya",
    roughDraftReply:
      "Hi Priya, Thank you for contacting us regarding the usage report and invoice preview in your account. We understand that there appears to be a discrepancy between the number of active seats shown in the dashboard and the number of seats approved during your renewal. The most likely explanation is that the three temporary contractors were counted as active seats during May. Even if a user only joins for part of the month, prorated charges may apply. Please check whether the contractors are still active and send us their names if you would like assistance.",
    expectedFacts: ["Priya", "18 active seats", "15 regular seats", "NZD $126", "May 8"],
  },
  {
    id: "support-02-export-error",
    scenario: "Customer support",
    tonePreset: "Professional",
    messageToReplyTo:
      "The CSV export from the dashboard is missing the custom tags column. We need the export for our Monday board packet, and the team cannot reconcile April without that column.",
    roughDraftReply:
      "Thank you for reaching out. We apologize for any inconvenience caused by the missing custom tags column in your CSV export. Our team is currently investigating the matter and will provide an update as soon as possible. In the meantime, please be advised that you may try exporting the report again from the dashboard settings page.",
    expectedFacts: ["custom tags column", "Monday board packet", "April"],
  },
  {
    id: "support-03-login-access",
    scenario: "Customer support",
    tonePreset: "Concise",
    messageToReplyTo:
      "Mina was added to our workspace yesterday, but she still sees the old team after logging in with mina@northstar.example. We already resent the invite twice.",
    roughDraftReply:
      "Hello, thank you for contacting support. We understand the issue regarding Mina's account access and apologize for the inconvenience. It may be related to the user's previous workspace association. Please ensure that the invitation has been accepted correctly and that the user is logging in with the appropriate email address.",
    expectedFacts: ["Mina", "mina@northstar.example", "resent the invite twice"],
  },
  {
    id: "cover-01-operations-role",
    scenario: "Cover letter",
    tonePreset: "Professional",
    messageToReplyTo:
      "Job post: Operations Coordinator at a nonprofit education program. The role mentions partner communication, weekly reporting, scheduling, and keeping shared documents organized.",
    roughDraftReply:
      "I am writing to express my interest in the Operations Coordinator position. I am a passionate and results-driven professional with a proven track record of managing communication, coordinating schedules, and supporting team success. In my last role, I prepared weekly partner updates, kept shared folders organized, and helped schedule meetings for a team of eight. I believe I would be a perfect fit for your dynamic team.",
    expectedFacts: ["weekly partner updates", "shared folders", "team of eight"],
  },
  {
    id: "cover-02-customer-success",
    scenario: "Cover letter",
    tonePreset: "Warm",
    messageToReplyTo:
      "Role: Customer Success Associate at a B2B SaaS company. They want someone who can answer customer questions clearly and write helpful follow-up notes.",
    roughDraftReply:
      "I am excited to apply for the Customer Success Associate role. I have always been passionate about helping customers and delivering excellent service. In my current position, I respond to customer questions, summarize action items after calls, and update our help center articles when a pattern appears. I am confident that my communication skills and positive attitude would make me a strong addition to the team.",
    expectedFacts: ["summarize action items", "help center articles", "Customer Success Associate"],
  },
  {
    id: "cover-03-admin-assistant",
    scenario: "Cover letter",
    tonePreset: "Concise",
    messageToReplyTo:
      "Opening: Administrative Assistant for a clinic. The listing emphasizes calendar coordination, patient follow-up notes, and careful handling of private information.",
    roughDraftReply:
      "Please accept my application for the Administrative Assistant position. I am highly organized and detail oriented, and I have extensive experience supporting administrative workflows. At my previous clinic, I managed calendars for three providers, prepared patient follow-up notes, and handled private information carefully according to office procedures. I would welcome the opportunity to bring my skills to your organization.",
    expectedFacts: ["three providers", "patient follow-up notes", "private information"],
  },
  {
    id: "work-01-design-delay",
    scenario: "Work update",
    tonePreset: "Professional",
    messageToReplyTo:
      "Can you send the revised screenshots today? I need to include them in the partner update.",
    roughDraftReply:
      "Unfortunately, the revised screenshots are not available at this time due to a delay in receiving the updated design file. The source file arrived late this morning and still requires one quality check before it can be shared externally. I expect to send the screenshots by 4pm Friday if there are no further issues.",
    expectedFacts: ["source file arrived late", "one quality check", "4pm Friday"],
  },
  {
    id: "work-02-launch-risk",
    scenario: "Work update",
    tonePreset: "Concise",
    messageToReplyTo:
      "Can you give me the current status before the 2pm launch check?",
    roughDraftReply:
      "The current launch status is that the payment flow has passed the latest smoke test, but the webhook retry log still needs review. I am checking the last three failed events and will post the result before the 2pm launch check. At this time, I do not recommend changing the launch decision until that review is complete.",
    expectedFacts: ["payment flow", "last three failed events", "2pm launch check"],
  },
  {
    id: "work-03-research-summary",
    scenario: "Work update",
    tonePreset: "Friendly",
    messageToReplyTo:
      "Did we get enough feedback from the teacher interviews to update the onboarding copy?",
    roughDraftReply:
      "I am writing to inform you that the teacher interview notes are now ready for review. We completed six interviews this week. Four teachers mentioned that the onboarding copy felt too technical, and two asked for a sample response before signing up. I recommend updating the first screen and adding one short example before the next test on Wednesday.",
    expectedFacts: ["six interviews", "four teachers", "two asked", "Wednesday"],
  },
];

await loadEnvLocal();

const rows = [];

for (const sample of cases) {
  const input = rewriteRequestSchema.parse({
    scenario: sample.scenario,
    messageToReplyTo: sample.messageToReplyTo,
    roughDraftReply: sample.roughDraftReply,
    tone: tonePresetToTone(sample.tonePreset),
    tonePreset: sample.tonePreset,
  });
  const plan = createRewritePlan(input);
  const result = await rewriteWithOptimization(input);
  const facts = factCheck(result.rewrittenText, sample.expectedFacts);
  const draft = result.naturalness.draftAiLikePercent;
  const rewrite = result.naturalness.rewriteAiLikePercent;
  const change = result.naturalness.changePoints;
  const signalAvailable = draft !== null && rewrite !== null && change !== null;
  const signalPassed =
    signalAvailable && rewrite < 50 && change <= -30;

  rows.push({
    ...sample,
    diagnosisTags: plan.tags,
    rewritePlan: plan.summary,
    rewrittenText: result.rewrittenText,
    draft,
    rewrite,
    change,
    factsPreserved: facts.passed,
    missingFacts: facts.missing,
    pass: facts.passed && signalPassed,
  });
}

const measured = rows.filter(
  (row) => row.draft !== null && row.rewrite !== null && row.change !== null,
);
const averageDrop = measured.length
  ? Math.round(
      measured.reduce((total, row) => total + Math.abs(row.change ?? 0), 0) /
        measured.length,
    )
  : null;
const belowFifty = measured.filter((row) => (row.rewrite ?? 100) < 50).length;
const passed = rows.filter((row) => row.pass).length;

const lines = [
  "# Scenario Evaluation Results",
  "",
  `Date: ${new Date().toISOString()}`,
  `Cases evaluated: ${rows.length}`,
  `Measured cases: ${measured.length}`,
  `Average AI-like signal drop: ${
    averageDrop === null ? "unavailable" : `${averageDrop} pts`
  }`,
  `Rewrite below 50% AI-like signal: ${belowFifty}/${measured.length}`,
  `Case pass count: ${passed}/${rows.length}`,
  "",
  "Pass requires: all expected facts preserved, scores available, rewrite below 50%, and at least 30 points lower than the draft.",
  "",
  ...rows.flatMap((row) => [
    `## ${row.id}`,
    "",
    `Scenario: ${row.scenario}`,
    `Tone: ${row.tonePreset}`,
    `Diagnosis tags: ${row.diagnosisTags.length ? row.diagnosisTags.join(", ") : "none"}`,
    `Rewrite plan: ${row.rewritePlan}`,
    `Draft AI-like signal: ${points(row.draft)}`,
    `Rewrite AI-like signal: ${points(row.rewrite)}`,
    `Change: ${row.change === null ? "unavailable" : `${row.change} pts`}`,
    `Facts preserved: ${row.factsPreserved ? "yes" : "no"}`,
    `Missing facts: ${row.missingFacts.length ? row.missingFacts.join("; ") : "none"}`,
    `Pass: ${row.pass ? "yes" : "no"}`,
    "",
    "Expected facts:",
    row.expectedFacts.map((fact) => `- ${fact}`).join("\n"),
    "",
    "Before:",
    "",
    "```text",
    row.roughDraftReply,
    "```",
    "",
    "After:",
    "",
    "```text",
    row.rewrittenText,
    "```",
    "",
  ]),
];

const outputPath = path.join(process.cwd(), "docs", "scenario-evaluation-results.md");
await writeFile(outputPath, lines.join("\n"));
console.log(`Wrote ${outputPath}`);
