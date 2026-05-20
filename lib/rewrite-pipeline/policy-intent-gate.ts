import type { RewriteRequestInput } from "../validation";
import type { PolicyIntentGateResult } from "./types";

function normalize(value: string) {
  return value.toLowerCase().replace(/\s+/g, " ").trim();
}

function sourceText(input: RewriteRequestInput) {
  return [
    input.messageToReplyTo,
    input.roughDraftReply,
    input.whatHappened,
    input.factsToPreserve,
  ]
    .filter(Boolean)
    .join("\n\n");
}

function includesAny(text: string, patterns: RegExp[]) {
  return patterns.some((pattern) => pattern.test(text));
}

export function runPolicyIntentGate(
  input: RewriteRequestInput,
  rewrittenText: string,
): PolicyIntentGateResult {
  const source = normalize(sourceText(input));
  const output = normalize(rewrittenText);
  const issues: PolicyIntentGateResult["issues"] = [];

  if (!source.trim()) {
    return { safe: true, issues };
  }

  if (
    includesAny(source, [/\bsubject to availability\b/, /\bdepending on (seat )?availability\b/]) &&
    !/\bavailability\b|\bavailable seats\b|\bif.*available\b/.test(output)
  ) {
    issues.push({
      kind: "policy_intent_drift",
      message: "Dropped availability constraint.",
    });
  }

  if (
    /\bmay (?:still )?be eligible\b|\bmight be eligible\b|\bmay be possible\b/.test(source) &&
    /\bis eligible\b|\bwill be eligible\b|\bfull refund is available\b|\brefund is available\b/.test(output) &&
    !/\bwhether (?:a )?(?:full )?refund is available\b/.test(output)
  ) {
    issues.push({
      kind: "changed_policy_or_condition",
      message: "Turned eligibility uncertainty into a guarantee.",
    });
  }

  if (
    /\brefund\b/.test(source) &&
    !/\bwill refund\b|\brefund has been\b|\brefund is approved\b/.test(source) &&
    /\bwill refund\b|\brefund has been\b|\brefund is approved\b/.test(output)
  ) {
    issues.push({
      kind: "unsupported_fact",
      message: "Added a refund commitment that was not in the source.",
    });
  }

  if (
    /\b(?:will not|won't|do not)\s+(?:make|update|change|cancel|remove|deactivate)\b.*\bunless\b|\bnot asking you to change anything yet\b/.test(
      source,
    ) &&
    !/\b(?:will not|won't|do not)\s+(?:make|update|change|cancel|remove|deactivate)\b|\bunless\b|\bconfirm\b/.test(
      output,
    )
  ) {
    issues.push({
      kind: "policy_intent_drift",
      message: "Dropped the no-action-without-confirmation constraint.",
    });
  }

  if (
    /\b(?:do not|don't|not)\s+(?:delete|cancel|remove|change)\b/.test(source) &&
    /\b(?:deleted|canceled|cancelled|removed|changed|updated)\b/.test(output) &&
    !/\b(?:do not|don't|not)\s+(?:delete|cancel|remove|change)\b/.test(output)
  ) {
    issues.push({
      kind: "unsupported_fact",
      message: "Changed a negative constraint into an action.",
    });
  }

  if (
    /\bnot (?:promising|promise|guarantee|guaranteed)\b|\bcannot (?:promise|guarantee)\b/.test(source) &&
    /\b(?:promise|guarantee|guaranteed|definitely|will be approved)\b/.test(output) &&
    !/\bnot (?:promising|promise|guarantee|guaranteed)\b|\bcannot (?:promise|guarantee)\b/.test(output)
  ) {
    issues.push({
      kind: "changed_policy_or_condition",
      message: "Dropped a no-promise/no-guarantee constraint.",
    });
  }

  return {
    safe: issues.length === 0,
    issues,
  };
}
