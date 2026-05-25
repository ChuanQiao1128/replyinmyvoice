import type { RewriteRequestInput } from "../validation";
import type { InputAnalysis, RewriteStrategy } from "./types";

function normalize(value: string) {
  return value.toLowerCase().replace(/\s+/g, " ").trim();
}

function combinedInput(input: RewriteRequestInput) {
  return [
    input.messageToReplyTo,
    input.roughDraftReply,
    input.audience,
    input.purpose,
    input.whatHappened,
    input.factsToPreserve,
  ]
    .filter(Boolean)
    .join("\n\n");
}

function countWords(value: string) {
  return value.trim().split(/\s+/).filter(Boolean).length;
}

function countParagraphs(value: string) {
  return value
    .split(/\n{2,}/)
    .map((paragraph) => paragraph.trim())
    .filter(Boolean).length;
}

function countMatches(value: string, patterns: RegExp[]) {
  return patterns.reduce((count, pattern) => count + (value.match(pattern)?.length ?? 0), 0);
}

function hasPolicyCareSignal(text: string) {
  return /\b(refund|invoice|billing|prorat|subscription|seat|transfer|enrollment|registration|cohort|eligible|eligibility|availability|available|credit|policy|cancel|cancellation|charge|account change|make changes|confirm|approval)\b/i.test(
    text,
  );
}

function hasTeacherParentSignal(text: string) {
  return /\b(parent|student|teacher|grade|assignment|homework|quiz|class|late work|partial credit|make-up|makeup)\b/i.test(
    text,
  );
}

function hasSalesSignal(text: string) {
  return /\b(proposal|renewal|vendor|demo|pricing|plan options|buyer|lead|sales|finance thread)\b/i.test(
    text,
  );
}

function hasWorkplaceSignal(text: string) {
  return /\b(update|blocker|launch|handoff|incident|meeting|review|owner|qa|deadline)\b/i.test(
    text,
  );
}

function hasCoverLetterSignal(text: string) {
  return /\b(applying|application|role|cover letter|position|hiring|resume|qualifications)\b/i.test(
    text,
  );
}

function containsForwardedThread(text: string) {
  return /(^|\n)\s*(from:|sent:|to:|subject:|on .+ wrote:|[-]{2,}\s*forwarded message|[-]{2,}\s*original message)/i.test(
    text,
  );
}

function containsListsOrQuotes(text: string) {
  return /(^|\n)\s*(?:[-*]|\d+[.)])\s+\S/.test(text) || /["“][^"”]{30,}["”]/.test(text);
}

function factualSignalCount(text: string) {
  return countMatches(text, [
    /\b\d+(?:[.:]\d+)?\s*(?:am|pm|a\.m\.|p\.m\.)?\b/gi,
    /\b(?:monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b/gi,
    /\b(?:jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\.?\s+\d{1,2}\b/gi,
    /\b(?:nzd|usd|aud|\$)\s?\d[\d,]*(?:\.\d+)?\b/gi,
    /\b[A-Z][a-z]+@[a-z0-9.-]+\.[a-z]{2,}\b/g,
  ]);
}

function selectScenarioHint(text: string): InputAnalysis["scenarioHint"] {
  if (hasPolicyCareSignal(text)) {
    return "support_policy";
  }

  if (hasTeacherParentSignal(text)) {
    return "teacher_parent";
  }

  if (hasSalesSignal(text)) {
    return "sales_followup";
  }

  if (hasCoverLetterSignal(text)) {
    return "cover_letter";
  }

  if (hasWorkplaceSignal(text)) {
    return "workplace_update";
  }

  return "general";
}

function selectInitialStrategy({
  containsForwarded,
  containsStructuredContent,
  paragraphCount,
  policyCare,
  scenarioHint,
  wordCount,
}: {
  containsForwarded: boolean;
  containsStructuredContent: boolean;
  paragraphCount: number;
  policyCare: boolean;
  scenarioHint: InputAnalysis["scenarioHint"];
  wordCount: number;
}): RewriteStrategy {
  if (containsForwarded) {
    return "messy_thread_cleanup";
  }

  if (containsStructuredContent) {
    return "quote_list_safe_rewrite";
  }

  if (policyCare || scenarioHint === "support_policy") {
    return "support_policy_options_rewrite";
  }

  if (wordCount >= 180 || paragraphCount >= 5) {
    return "full_structure_rewrite";
  }

  if (wordCount <= 70) {
    return "minimal_polish";
  }

  return "facts_first_reconstruct";
}

export function analyzeRewriteInput(input: RewriteRequestInput): InputAnalysis {
  const text = combinedInput(input);
  const normalizedText = normalize(text);
  const wordCount = countWords(text);
  const paragraphCount = countParagraphs(text);
  const containsForwarded = containsForwardedThread(text);
  const containsStructuredContent = containsListsOrQuotes(text);
  const policyCare = hasPolicyCareSignal(text);
  const factSignals = factualSignalCount(text);
  const scenarioHint = selectScenarioHint(text);
  const inputKind: InputAnalysis["inputKind"] = containsForwarded
    ? "messy_thread"
    : containsStructuredContent
      ? "quote_or_list_heavy"
      : input.messageToReplyTo.trim()
        ? "reply_with_context"
        : "draft_only";
  const factualDensity: InputAnalysis["factualDensity"] =
    factSignals >= 6 || wordCount >= 260
      ? "high"
      : factSignals >= 3 || wordCount >= 130
        ? "medium"
        : "low";
  const structureRisk: InputAnalysis["structureRisk"] =
    containsForwarded || containsStructuredContent || paragraphCount >= 8
      ? "high"
      : paragraphCount >= 5 || wordCount >= 220
        ? "medium"
        : "low";
  const riskLevel: InputAnalysis["riskLevel"] =
    policyCare || factualDensity === "high" || structureRisk === "high"
      ? "high"
      : factualDensity === "medium" || structureRisk === "medium"
        ? "medium"
        : "low";
  const rewriteFreedom: InputAnalysis["rewriteFreedom"] =
    policyCare || factualDensity === "high"
      ? "minimal"
      : normalizedText.includes("do not") || normalizedText.includes("cannot")
        ? "moderate"
        : "high";
  const recommendedInitialStrategy = selectInitialStrategy({
    containsForwarded,
    containsStructuredContent,
    paragraphCount,
    policyCare,
    scenarioHint,
    wordCount,
  });
  const reasons = [
    inputKind,
    scenarioHint,
    `risk:${riskLevel}`,
    `facts:${factualDensity}`,
    `structure:${structureRisk}`,
    `freedom:${rewriteFreedom}`,
  ];

  if (policyCare) {
    reasons.push("policy_care");
  }

  if (containsStructuredContent) {
    reasons.push("structured_content");
  }

  if (containsForwarded) {
    reasons.push("forwarded_thread");
  }

  return {
    inputKind,
    scenarioHint,
    riskLevel,
    factualDensity,
    structureRisk,
    rewriteFreedom,
    requiresStructurePreservation: containsStructuredContent || containsForwarded,
    requiresPolicyCare: policyCare,
    containsForwardedThread: containsForwarded,
    containsListsOrQuotes: containsStructuredContent,
    wordCount,
    paragraphCount,
    recommendedInitialStrategy,
    reasons,
  };
}
