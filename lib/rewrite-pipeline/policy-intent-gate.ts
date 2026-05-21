import type { RewriteRequestInput } from "../validation";
import type { PolicyIntentGateResult, RewriteFailureKind } from "./types";

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

type PolicyIntentRuleContext = {
  source: string;
  output: string;
  outputForNegativeActionCheck: string;
};

type PolicyIntentRule = {
  id: string;
  kind: RewriteFailureKind;
  message: string;
  applies(context: PolicyIntentRuleContext): boolean;
};

export const policyIntentRules: PolicyIntentRule[] = [
  {
    id: "availability_constraint",
    kind: "policy_intent_drift",
    message: "Dropped availability constraint.",
    applies({ source, output }) {
      return (
        includesAny(source, [
          /\bsubject to availability\b/,
          /\bdepending on (seat )?availability\b/,
        ]) && !/\bavailability\b|\bavailable seats\b|\bif.*available\b/.test(output)
      );
    },
  },
  {
    id: "refund_eligibility_uncertainty",
    kind: "changed_policy_or_condition",
    message: "Turned eligibility uncertainty into a guarantee.",
    applies({ source, output }) {
      return (
        /\bmay (?:still )?be eligible\b|\bmight be eligible\b|\bmay be possible\b/.test(
          source,
        ) &&
        /\bis eligible\b|\bwill be eligible\b|\bfull refund is available\b|\brefund is available\b/.test(
          output,
        ) &&
        !/\bwhether (?:a )?(?:full )?refund is available\b/.test(output)
      );
    },
  },
  {
    id: "unsupported_refund_commitment",
    kind: "unsupported_fact",
    message: "Added a refund commitment that was not in the source.",
    applies({ source, output }) {
      return (
        /\brefund\b/.test(source) &&
        !/\bwill refund\b|\brefund has been\b|\brefund is approved\b/.test(
          source,
        ) &&
        /\bwill refund\b|\brefund has been\b|\brefund is approved\b/.test(output)
      );
    },
  },
  {
    id: "no_action_without_confirmation",
    kind: "policy_intent_drift",
    message: "Dropped the no-action-without-confirmation constraint.",
    applies({ source, output }) {
      return (
        /\b(?:will not|won't|do not)\s+(?:make|update|change|cancel|remove|deactivate)\b.*\bunless\b|\bnot asking you to change anything yet\b/.test(
          source,
        ) &&
        !/\b(?:will not|won't|do not)\s+(?:make|update|change|cancel|remove|deactivate)\b|\bunless\b|\bconfirm\b/.test(
          output,
        )
      );
    },
  },
  {
    id: "negative_constraint_to_action",
    kind: "unsupported_fact",
    message: "Changed a negative constraint into an action.",
    applies({ source, output, outputForNegativeActionCheck }) {
      return (
        /\b(?:do not|don't|not)\s+(?:delete|cancel|remove|change)\b/.test(source) &&
        /\b(?:deleted|canceled|cancelled|removed|changed|updated)\b/.test(
          outputForNegativeActionCheck,
        ) &&
        !/\b(?:do not|don't|not)\s+(?:delete|cancel|remove|change)\b/.test(
          output,
        ) &&
        !(
          /\bgo-live date\b/.test(source) &&
          /\bkeep\b.*\bgo-live date\b/.test(output)
        )
      );
    },
  },
  {
    id: "no_promise_constraint",
    kind: "changed_policy_or_condition",
    message: "Dropped a no-promise/no-guarantee constraint.",
    applies({ source, output }) {
      return (
        /\bnot (?:promising|promise|guarantee|guaranteed)\b|\bcannot (?:promise|guarantee)\b/.test(
          source,
        ) &&
        /\b(?:promise|guarantee|guaranteed|definitely|will be approved)\b/.test(
          output,
        ) &&
        !/\bnot (?:promising|promise|guarantee|guaranteed)\b|\bcannot (?:promise|guarantee)\b/.test(
          output,
        )
      );
    },
  },
];

export function runPolicyIntentGate(
  input: RewriteRequestInput,
  rewrittenText: string,
): PolicyIntentGateResult {
  const source = normalize(sourceText(input));
  const output = normalize(rewrittenText);
  const outputForNegativeActionCheck = output
    .replace(/\bupdated implementation note\b/g, "")
    .replace(/\bwhat changed\b/g, "");
  const issues: PolicyIntentGateResult["issues"] = [];

  if (!source.trim()) {
    return { safe: true, issues };
  }

  const context = { source, output, outputForNegativeActionCheck };
  for (const rule of policyIntentRules) {
    if (!rule.applies(context)) {
      continue;
    }
    issues.push({
      ruleId: rule.id,
      kind: rule.kind,
      message: rule.message,
    });
  }

  return {
    safe: issues.length === 0,
    issues,
  };
}
