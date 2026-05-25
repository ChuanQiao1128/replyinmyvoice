import { generateGuaranteedRewriteCandidate } from "../openai";
import { generateExtractiveFallbackCandidate } from "../rewrite-extractive-fallback";
import type { RewriteResponsePayload } from "../rewrite-types";
import type { RewriteTelemetryCollector } from "../observability/rewrite-telemetry";
import { formatNaturalness, measureWritingSignal } from "../writing-signal";
import type { RewriteRequestInput } from "../validation";
import {
  approveRewriteAttempt,
  createInitialBudgetState,
  createRewriteBudget,
  recordRewriteAttempt,
} from "./budget-manager";
import { createRewriteAttemptLedgerEntry } from "./attempt-ledger";
import {
  adaptiveGateCheck,
  llmFactCheckPasses,
  reviewerScorePasses,
} from "./checks";
import { getFactReconstructConfig } from "./config";
import { reviewExtractedFacts } from "./fact-ledger";
import { analyzeRewriteInput } from "./input-analyzer";
import {
  classifyScenario,
  diagnoseHighRiskSentences,
  escalateCandidate,
  extractFacts,
  finalizeCandidate,
  generateCandidates,
  JsonCompletionQualityError,
  llmFactCheck,
  repairHighRiskSentences,
  reviewCandidates,
} from "./model";
import { runPolicyIntentGate } from "./policy-intent-gate";
import { getStyleCard } from "./style-cards";
import { strategyGuidance } from "./strategy-catalog";
import { chooseInitialStrategy, chooseNextStrategy } from "./strategy-router";
import { selectHighRiskSentences } from "./targeted-repair";
import type {
  CandidateScore,
  CandidateKey,
  CandidateSet,
  ExtractedFacts,
  FactReconstructConfig,
  LlmFactCheckResult,
  ReviewResult,
  RewriteFailureKind,
  RewriteAttemptLedgerEntry,
  RewriteStrategy,
  RewriteStrategyDecision,
  ScenarioClassification,
  StyleCard,
} from "./types";

type FactReconstructQualityFailureReason =
  | "signal_unavailable"
  | "fact_check_failed"
  | "model_json_unusable"
  | "reviewer_threshold_failed"
  | "naturalness_gate_failed";

export class FactReconstructQualityError extends Error {
  naturalness: RewriteResponsePayload["naturalness"];
  rejectedCandidates: number;
  repairCandidatesTried: number;
  candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"];
  attemptLedger: RewriteAttemptLedgerEntry[];
  diagnosisTags: RewriteResponsePayload["optimization"]["diagnosisTags"];
  rewritePlanSummary: string;
  reason: FactReconstructQualityFailureReason;

  constructor({
    naturalness,
    rejectedCandidates,
    repairCandidatesTried,
    candidateSignals,
    attemptLedger,
    diagnosisTags,
    rewritePlanSummary,
    reason,
  }: {
    naturalness: RewriteResponsePayload["naturalness"];
    rejectedCandidates: number;
    repairCandidatesTried: number;
    candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"];
    attemptLedger?: RewriteAttemptLedgerEntry[];
    diagnosisTags: RewriteResponsePayload["optimization"]["diagnosisTags"];
    rewritePlanSummary: string;
    reason: FactReconstructQualityFailureReason;
  }) {
    super("Rewrite did not meet the internal quality bar.");
    this.name = "FactReconstructQualityError";
    this.naturalness = naturalness;
    this.rejectedCandidates = rejectedCandidates;
    this.repairCandidatesTried = repairCandidatesTried;
    this.candidateSignals = candidateSignals;
    this.attemptLedger = attemptLedger ?? [];
    this.diagnosisTags = diagnosisTags;
    this.rewritePlanSummary = rewritePlanSummary;
    this.reason = reason;
  }
}

const candidateKeys: CandidateKey[] = [
  "candidate_a_concise",
  "candidate_b_warm",
  "candidate_c_natural",
];

type CandidatePreselectionRejection = {
  key: CandidateKey;
  score: CandidateScore;
  issues: string[];
  failureKinds: RewriteFailureKind[];
};

function rankedCandidateKeys(review: ReviewResult) {
  return [...candidateKeys].sort((left, right) => {
    if (left === review.best_candidate_key) {
      return -1;
    }

    if (right === review.best_candidate_key) {
      return 1;
    }

    return review.scores[right].total - review.scores[left].total;
  });
}

function selectCandidateBeforeFinalize({
  candidates,
  facts,
  input,
  review,
  styleCard,
}: {
  candidates: CandidateSet;
  facts: ExtractedFacts;
  input: RewriteRequestInput;
  review: ReviewResult;
  styleCard: StyleCard;
}) {
  const rejections: CandidatePreselectionRejection[] = [];

  for (const key of rankedCandidateKeys(review)) {
    const score = review.scores[key];
    const candidate = candidates[key];
    const deterministic = adaptiveGateCheck(input, facts, candidate, styleCard);
    const policyGate = runPolicyIntentGate(input, candidate);
    const issues = [
      ...score.issues.map((issue) => `review:${issue}`),
      ...deterministic.blockingIssues,
      ...policyGate.issues.map((issue) => `policy:${issue.kind}`),
    ];

    if (
      reviewerScorePasses(score) &&
      deterministic.safe &&
      policyGate.safe
    ) {
      return {
        selected: {
          key,
          candidate,
          score,
        },
        rejections,
      };
    }

    rejections.push({
      key,
      score,
      issues,
      failureKinds: [
        ...(reviewerScorePasses(score) ? [] : (["too_generic"] as RewriteFailureKind[])),
        ...failureKindsFromIssues(deterministic.blockingIssues),
        ...policyGate.issues.map((issue) => issue.kind),
      ],
    });
  }

  return {
    selected: null,
    rejections,
  };
}

function naturalnessPasses({
  config,
  draftPercent,
  rewritePercent,
}: {
  config: FactReconstructConfig;
  draftPercent: number | null;
  rewriteSignal?: Awaited<ReturnType<typeof measureWritingSignal>>;
  rewritePercent: number | null;
}) {
  if (draftPercent === null || rewritePercent === null) {
    return false;
  }

  // rewritePercent is the robust median of the cleaned sentence scores
  // (lib/writing-signal.ts), so it only exceeds the threshold when at least half
  // the reply reads AI-like. A single boilerplate or list-marker sentence can no
  // longer veto a fact-perfect email. We intentionally do NOT require
  // `rewrite <= draft` when the draft is already clean: that old rule forced an
  // already-human draft (e.g. 5%) below the threshold and punished normal
  // rewriting.
  return rewritePercent <= config.naturalnessThreshold;
}

function throwQualityFailure({
  attemptLedger = [],
  candidateSignals,
  diagnosisTags,
  draftPercent,
  reason,
  rejectedCandidates,
  repairCandidatesTried,
  rewritePercent,
  rewritePlanSummary,
}: {
  attemptLedger?: RewriteAttemptLedgerEntry[];
  candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"];
  diagnosisTags: RewriteResponsePayload["optimization"]["diagnosisTags"];
  draftPercent: number | null;
  reason: FactReconstructQualityFailureReason;
  rejectedCandidates: number;
  repairCandidatesTried: number;
  rewritePercent: number | null;
  rewritePlanSummary: string;
}): never {
  throw new FactReconstructQualityError({
    naturalness: formatNaturalness(draftPercent, rewritePercent),
    rejectedCandidates,
    repairCandidatesTried,
    candidateSignals,
    attemptLedger,
    diagnosisTags,
    rewritePlanSummary,
    reason,
  });
}

function factCheckSafe({
  deterministicSafe,
  llmResult,
}: {
  deterministicSafe: boolean;
  llmResult: LlmFactCheckResult;
}) {
  if (
    deterministicSafe &&
    llmResult.issues.length === 0 &&
    llmResult.required_repairs.length === 0
  ) {
    return true;
  }

  return deterministicSafe && llmFactCheckPasses(llmResult);
}

function deterministicSafeForFallback(
  input: RewriteRequestInput,
  facts: ExtractedFacts,
  rewrittenText: string,
  styleCard: StyleCard,
) {
  const result = adaptiveGateCheck(input, facts, rewrittenText, styleCard);

  return { result, safe: result.safe };
}

function failureKindsFromIssues(
  issues: string[],
): RewriteFailureKind[] {
  const failures = new Set<RewriteFailureKind>();

  for (const issue of issues) {
    if (issue.startsWith("missing:") || issue.startsWith("missing_locked:")) {
      failures.add("fact_loss");
    } else if (issue.startsWith("unsupported:")) {
      failures.add("unsupported_fact");
    } else if (issue.includes("broken_numbered_list")) {
      failures.add("broken_numbered_list");
    } else if (issue.includes("broken_quote_boundary")) {
      failures.add("broken_quote_boundary");
    } else if (issue.includes("sentence_per_paragraph")) {
      failures.add("sentence_per_paragraph");
    } else if (issue.includes("line_split_paraphrase")) {
      failures.add("line_split_paraphrase");
    } else if (issue.includes("template_phrase")) {
      failures.add("support_macro_voice");
    } else if (issue.includes("meta_language")) {
      failures.add("too_generic");
    }
  }

  return [...failures];
}

function failureKindsFromFactCheck(result: LlmFactCheckResult) {
  const failures = new Set<RewriteFailureKind>();

  if (result.has_removed_important_facts || result.has_changed_names) {
    failures.add("fact_loss");
  }

  if (result.has_added_new_facts) {
    failures.add("unsupported_fact");
  }

  if (
    result.has_changed_dates_or_deadlines ||
    result.has_changed_conditions_or_policies
  ) {
    failures.add("changed_policy_or_condition");
  }

  if (result.tone_problem) {
    failures.add("too_generic");
  }

  return [...failures];
}

function recordFailedAttempt(
  attemptLedger: RewriteAttemptLedgerEntry[],
  entry: Omit<RewriteAttemptLedgerEntry, "attemptNo">,
) {
  attemptLedger.push(
    createRewriteAttemptLedgerEntry({
      attemptNo: attemptLedger.length + 1,
      ...entry,
    }),
  );
}

function saplingSummary(value: number | null, reason: string) {
  return value === null ? `${reason}:unavailable` : `${reason}:${value}%`;
}

function decideRepairStrategy({
  analysis,
  failures,
  previousStrategies,
}: {
  analysis: ReturnType<typeof analyzeRewriteInput>;
  failures: RewriteFailureKind[];
  previousStrategies: RewriteStrategy[];
}) {
  return chooseNextStrategy({
    analysis,
    failures,
    previousStrategies,
  });
}

function recordApprovedStrategy({
  budget,
  budgetState,
  decision,
  previousStrategies,
}: {
  budget: ReturnType<typeof createRewriteBudget>;
  budgetState: ReturnType<typeof createInitialBudgetState>;
  decision: RewriteStrategyDecision;
  previousStrategies: RewriteStrategy[];
}) {
  const approval = approveRewriteAttempt({
    budget,
    decision,
    state: budgetState,
  });

  if (!approval.approved) {
    return {
      budgetState,
      approved: false,
      reason: approval.reason,
    };
  }

  previousStrategies.push(decision.strategy);

  return {
    budgetState: recordRewriteAttempt({
      decision,
      estimatedUsd: decision.modelTier === "strong" ? 0.04 : 0.015,
      state: budgetState,
    }),
    approved: true,
    reason: approval.reason,
  };
}

function withRestoredRecipientGreeting(
  fallback: ReturnType<typeof generateGuaranteedRewriteCandidate>,
  facts: ExtractedFacts,
) {
  const recipient = facts.recipient_name.trim();
  if (!recipient || isUnsafeRestoredRecipient(recipient)) {
    return fallback;
  }

  return {
    ...fallback,
    rewrittenText: fallback.rewrittenText.replace(
      /^(Hi|Hello|Dear),/i,
      `$1 ${recipient},`,
    ),
  };
}

function isUnsafeRestoredRecipient(value: string) {
  return /^(?:account|acceptable|admin|application|basic|billing|cancellation|core|current|customer|duplicate|eligibility|explain|express|finance|invoice|new|offer|order|original|parent|quiz|renewal|solo|standard|student|subscription|support|team|updated|upgrade)$/i.test(
    value.trim(),
  );
}

function diagnosisTagsForScenario(
  scenarioDomain: string,
): RewriteResponsePayload["optimization"]["diagnosisTags"] {
  if (scenarioDomain === "customer_support") {
    return ["support_template_voice", "corporate_polish"];
  }

  if (scenarioDomain === "education") {
    return ["stock_opening", "over_explained"];
  }

  if (scenarioDomain === "sales") {
    return ["corporate_polish", "generic_transitions"];
  }

  return ["corporate_polish", "low_specificity"];
}

function sourceWordCount(input: RewriteRequestInput) {
  return input.roughDraftReply.trim().split(/\s+/).filter(Boolean).length;
}

function shouldPreferExtractiveFallback(input: RewriteRequestInput) {
  return !input.messageToReplyTo.trim() && sourceWordCount(input) <= 200;
}

function buildPassedResponse({
  attemptLedger = [],
  candidateSignals,
  changeSummary,
  diagnosisTags,
  draftPercent,
  internalStrategiesTried,
  rejectedCandidates,
  repairCandidatesTried,
  rewrittenText,
  rewritePercent,
  rewritePlanSummary,
  riskNotes,
  selectionStatus = "passed",
}: {
  attemptLedger?: RewriteAttemptLedgerEntry[];
  candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"];
  changeSummary: string[];
  diagnosisTags: RewriteResponsePayload["optimization"]["diagnosisTags"];
  draftPercent: number;
  internalStrategiesTried: number;
  rejectedCandidates: number;
  repairCandidatesTried: number;
  rewrittenText: string;
  rewritePercent: number | null;
  rewritePlanSummary: string;
  riskNotes: string[];
  selectionStatus?: RewriteResponsePayload["optimization"]["selectionStatus"];
}): RewriteResponsePayload {
  return {
    rewrittenText,
    changeSummary,
    riskNotes,
    naturalness: formatNaturalness(draftPercent, rewritePercent),
    optimization: {
      internalStrategiesTried,
      repairCandidatesTried,
      rejectedCandidates,
      userUsageCharged: 1,
      selectionStatus,
      diagnosisTags,
      rewritePlanSummary,
      attemptLedger,
      candidateSignals,
    },
  };
}

async function tryGuaranteedFallback({
  config,
  draftPercent,
  facts,
  input,
  styleCard,
  telemetry,
}: {
  config: FactReconstructConfig;
  draftPercent: number;
  facts: ExtractedFacts;
  input: RewriteRequestInput;
  styleCard: StyleCard;
  telemetry?: RewriteTelemetryCollector;
}) {
  const modelFallback = withRestoredRecipientGreeting(
    generateGuaranteedRewriteCandidate(input),
    facts,
  );
  const extractiveFallback = withRestoredRecipientGreeting(
    generateExtractiveFallbackCandidate(input),
    facts,
  );
  const fallbackCandidates = shouldPreferExtractiveFallback(input)
    ? [extractiveFallback, modelFallback]
    : [modelFallback, extractiveFallback];
  let bestAttempt: {
    fallback: (typeof fallbackCandidates)[number];
    factSafe: boolean;
    naturalnessSafe: boolean;
    passes: boolean;
    signal: Awaited<ReturnType<typeof measureWritingSignal>>;
  } | null = null;

  for (const fallback of fallbackCandidates) {
    const deterministic = deterministicSafeForFallback(
      input,
      facts,
      fallback.rewrittenText,
      styleCard,
    );
    const policyGate = runPolicyIntentGate(input, fallback.rewrittenText);
    const signal = await measureWritingSignal(fallback.rewrittenText, {
      calibrateSentenceScores: true,
      role: "repair_signal",
      telemetry,
    });
    const factSafe = deterministic.safe && policyGate.safe;
    const naturalnessSafe = factSafe &&
      naturalnessPasses({
        config,
        draftPercent,
        rewriteSignal: signal,
        rewritePercent: signal.aiLikePercent,
      });
	    const attempt = {
	      fallback,
	      factSafe,
	      naturalnessSafe,
	      passes: naturalnessSafe,
	      signal,
	    };

    if (naturalnessSafe) {
      return attempt;
    }

    if (
      !bestAttempt ||
      (factSafe && !bestAttempt.factSafe) ||
      (factSafe === bestAttempt.factSafe &&
        signal.aiLikePercent !== null &&
        (bestAttempt.signal.aiLikePercent === null ||
          signal.aiLikePercent < bestAttempt.signal.aiLikePercent))
    ) {
      bestAttempt = attempt;
    }
  }

  return bestAttempt ?? {
    fallback: fallbackCandidates[0],
    factSafe: false,
    naturalnessSafe: false,
    passes: false,
    signal: { aiLikePercent: null },
  };
}

async function tryTargetedSentenceRepair({
  config,
  draftPercent,
  facts,
  finalEmail,
  finalSignal,
  input,
  scenario,
  styleCard,
  telemetry,
}: {
  config: FactReconstructConfig;
  draftPercent: number;
  facts: ExtractedFacts;
  finalEmail: string;
  finalSignal: Awaited<ReturnType<typeof measureWritingSignal>>;
  input: RewriteRequestInput;
  scenario: ScenarioClassification;
  styleCard: StyleCard;
  telemetry?: RewriteTelemetryCollector;
}) {
  const highRiskSentences = selectHighRiskSentences({
    signal: finalSignal,
    threshold: config.naturalnessThreshold,
  });

  if (highRiskSentences.length === 0) {
    return null;
  }

  const diagnostics = await diagnoseHighRiskSentences({
    facts,
    scenario,
    styleCard,
    highRiskSentences,
    config,
    telemetry,
  });
  const repairedEmail = await repairHighRiskSentences({
    facts,
    scenario,
    styleCard,
    finalEmail,
    diagnostics,
    config,
    telemetry,
  });
  const deterministic = adaptiveGateCheck(
    input,
    facts,
    repairedEmail,
    styleCard,
  );
  const factCheck = await llmFactCheck({
    facts,
    finalEmail: repairedEmail,
    config,
    telemetry,
  });
  const policyGate = runPolicyIntentGate(input, repairedEmail);
  const signal = await measureWritingSignal(repairedEmail, {
    calibrateSentenceScores: true,
    role: "repair_signal",
    telemetry,
  });
  const factSafe = factCheckSafe({
    deterministicSafe: deterministic.safe && policyGate.safe,
    llmResult: factCheck,
  });
  const passes = factSafe &&
    naturalnessPasses({
      config,
      draftPercent,
      rewriteSignal: signal,
      rewritePercent: signal.aiLikePercent,
    });

  return {
    diagnostics,
    factSafe,
    highRiskSentences,
    passes,
    repairedEmail,
    signal,
  };
}

export async function rewriteWithFactReconstruct(
  input: RewriteRequestInput,
  options: {
    config?: FactReconstructConfig;
    telemetry?: RewriteTelemetryCollector;
  } = {},
): Promise<RewriteResponsePayload> {
  const config = options.config ?? getFactReconstructConfig();
  const telemetry = options.telemetry;
  const analysis = analyzeRewriteInput(input);
  const budget = createRewriteBudget(analysis);
  let budgetState = createInitialBudgetState();
  const previousStrategies: RewriteStrategy[] = [];
  const initialDecision = chooseInitialStrategy(analysis);
  const initialApproval = recordApprovedStrategy({
    budget,
    budgetState,
    decision: initialDecision,
    previousStrategies,
  });
  budgetState = initialApproval.budgetState;
  const draftSignal = await measureWritingSignal(input.roughDraftReply, {
    role: "draft_signal",
    telemetry,
  });
  const candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"] = [];
  const attemptLedger: RewriteAttemptLedgerEntry[] = [];
  let diagnosisTags: RewriteResponsePayload["optimization"]["diagnosisTags"] = [];
  let rewritePlanSummary =
    "Stopped because structured model output could not be parsed after retries.";

  if (draftSignal.aiLikePercent === null) {
    throwQualityFailure({
      attemptLedger,
      candidateSignals,
      diagnosisTags: [],
      draftPercent: null,
      rewritePercent: null,
      rejectedCandidates: 0,
      repairCandidatesTried: 0,
      rewritePlanSummary:
        "Stopped before generation because the naturalness reference signal was unavailable.",
      reason: "signal_unavailable",
    });
  }

  try {
  const extractedFacts = await extractFacts(input, config, telemetry);
  const factLedger = reviewExtractedFacts(input, extractedFacts);
  const facts = factLedger.facts;
  const scenario = await classifyScenario(facts, config, telemetry);
  const styleCard = getStyleCard(scenario);
  diagnosisTags = diagnosisTagsForScenario(scenario.domain);
  rewritePlanSummary = [
    `Adaptive rewrite route ${initialDecision.strategy} using ${styleCard.style_card_id}.`,
    `Risk ${analysis.riskLevel}; structure ${analysis.structureRisk}; factual density ${analysis.factualDensity}.`,
    `Fact ledger added ${factLedger.addedAnchors.length} deterministic anchors and rejected ${factLedger.rejectedFacts.length} unsupported extracted facts.`,
    `Budget ${budget.maxAttempts} attempts; strong model ${budget.allowStrongModel ? "allowed once" : "not allowed"}.`,
  ].join(" ");

  const candidates = await generateCandidates({
    facts,
    scenario,
    styleCard,
    strategy: initialDecision.strategy,
    strategyGuidance: strategyGuidance(initialDecision.strategy),
    config,
    telemetry,
  });
  const review = await reviewCandidates({
    facts,
    scenario,
    styleCard,
    candidates,
    config,
    telemetry,
  });
  const preselection = selectCandidateBeforeFinalize({
    candidates,
    facts,
    input,
    review,
    styleCard,
  });

  if (!preselection.selected) {
    const primaryRejection = preselection.rejections[0];
    const failureIssues = primaryRejection?.issues.length
      ? primaryRejection.issues
      : ["No model candidate passed reviewer, deterministic, and policy gates."];
    const failureKinds = primaryRejection?.failureKinds.length
      ? primaryRejection.failureKinds
      : (["fact_loss"] as RewriteFailureKind[]);

    recordFailedAttempt(attemptLedger, {
      strategy: initialDecision.strategy,
      modelRole: "mid_writer",
      modelName: config.models.mid_writer,
      thinkingMode: "non_thinking",
      candidateText: primaryRejection
        ? candidates[primaryRejection.key]
        : candidates[review.best_candidate_key],
      failureAnalysis: `Candidate preselection rejected model candidates: ${failureIssues.join("; ")}`,
      failureKinds,
      factGateResult: "failed",
      structureGateResult: "failed",
      policyIntentGateResult: "failed",
      saplingResult: "not_measured",
      nextStrategyDecision: "facts_first_reconstruct",
    });

    const fallbackAttempt = await tryGuaranteedFallback({
      config,
      draftPercent: draftSignal.aiLikePercent,
      facts,
      input,
      styleCard,
      telemetry,
    });

	    candidateSignals.push({
	      stage: "fallback",
	      aiLikePercent: fallbackAttempt.signal.aiLikePercent,
	      status: fallbackAttempt.naturalnessSafe
	        ? "pass_below_threshold"
	        : "fail_insufficient_reduction",
	      rejected: !fallbackAttempt.passes,
      reason: "Deterministic facts-first fallback after candidate preselection rejected unsafe model candidates.",
    });

    if (fallbackAttempt.passes) {
      return buildPassedResponse({
        attemptLedger,
        rewrittenText: fallbackAttempt.fallback.rewrittenText,
        changeSummary: [
          "Rebuilt the reply from extracted facts instead of paraphrasing the draft.",
          "Used the facts-first fallback after hard gates rejected the model candidates.",
        ],
        riskNotes: fallbackAttempt.fallback.riskNotes.length
          ? fallbackAttempt.fallback.riskNotes
          : ["Review before sending."],
        draftPercent: draftSignal.aiLikePercent,
        rewritePercent: fallbackAttempt.signal.aiLikePercent,
        internalStrategiesTried: 4,
        repairCandidatesTried: 1,
        rejectedCandidates: 3,
        diagnosisTags,
        rewritePlanSummary,
        candidateSignals,
      });
    }

	    throwQualityFailure({
	      attemptLedger,
	      candidateSignals,
	      diagnosisTags,
	      draftPercent: draftSignal.aiLikePercent,
	      rewritePercent: fallbackAttempt.signal.aiLikePercent,
	      rejectedCandidates: 4,
	      repairCandidatesTried: 1,
	      rewritePlanSummary,
	      reason: fallbackAttempt.factSafe
	        ? "reviewer_threshold_failed"
	        : "fact_check_failed",
		    });
	  }

  const bestKey = preselection.selected.key;
  const bestCandidate = preselection.selected.candidate;
  const finalEmail = await finalizeCandidate({
    facts,
    scenario,
    styleCard,
    selectedCandidate: bestCandidate,
    requiredEdits: review.required_edits,
    config,
    telemetry,
  });
  const deterministic = adaptiveGateCheck(input, facts, finalEmail, styleCard);
  const policyGate = runPolicyIntentGate(input, finalEmail);
  const factCheck = await llmFactCheck({
    facts,
    finalEmail,
    config,
    telemetry,
  });

  if (
    !factCheckSafe({
      deterministicSafe: deterministic.safe && policyGate.safe,
      llmResult: factCheck,
    })
  ) {
    if (config.maxEscalations > 0) {
      const failures = [
        ...failureKindsFromIssues(deterministic.blockingIssues),
        ...failureKindsFromFactCheck(factCheck),
        ...policyGate.issues.map((issue) => issue.kind),
      ];
      const repairDecision = decideRepairStrategy({
        analysis,
        failures,
        previousStrategies,
      });
      recordFailedAttempt(attemptLedger, {
        strategy: initialDecision.strategy,
        modelRole: "mid_writer",
        modelName: config.models.mid_writer,
        thinkingMode: "non_thinking",
        candidateText: finalEmail,
        failureAnalysis: [
          ...deterministic.blockingIssues,
          ...policyGate.issues.map((issue) => issue.message),
          ...factCheck.issues,
          ...factCheck.required_repairs,
        ].join(" "),
        failureKinds: failures,
        factGateResult: factCheck.safe_to_send ? "pass" : "fail",
        structureGateResult: deterministic.safe
          ? "pass"
          : `fail:${deterministic.blockingIssues.join(";")}`,
        policyIntentGateResult: policyGate.safe
          ? "pass"
          : `fail:${policyGate.issues.map((issue) => issue.kind).join(";")}`,
        saplingResult: "not_measured",
        nextStrategyDecision: repairDecision.strategy,
      });
      const repairApproval = recordApprovedStrategy({
        budget,
        budgetState,
        decision: repairDecision,
        previousStrategies,
      });
      budgetState = repairApproval.budgetState;
      const escalatedEmail = await escalateCandidate({
        facts,
        scenario,
        styleCard,
        previousFinalEmail: finalEmail,
        reviewNotes: [
          ...review.required_edits,
          ...review.risk_notes,
          ...deterministic.blockingIssues,
          ...policyGate.issues.map((issue) => issue.message),
          ...factCheck.issues,
          ...factCheck.required_repairs,
        ],
        strategy: repairDecision.strategy,
        strategyGuidance: strategyGuidance(repairDecision.strategy),
        attemptHistory: attemptLedger,
        config,
        telemetry,
      });
      const escalatedDeterministic = adaptiveGateCheck(
        input,
        facts,
        escalatedEmail,
        styleCard,
      );
      const escalatedPolicyGate = runPolicyIntentGate(input, escalatedEmail);
      const escalatedFactCheck = await llmFactCheck({
        facts,
        finalEmail: escalatedEmail,
        config,
        telemetry,
      });
      const escalatedSignal = await measureWritingSignal(escalatedEmail, {
        calibrateSentenceScores: true,
        role: "repair_signal",
        telemetry,
      });
      const escalatedFactSafe = factCheckSafe({
        deterministicSafe:
          escalatedDeterministic.safe && escalatedPolicyGate.safe,
        llmResult: escalatedFactCheck,
      });
      const escalatedPasses = escalatedFactSafe &&
        naturalnessPasses({
          config,
          draftPercent: draftSignal.aiLikePercent,
          rewriteSignal: escalatedSignal,
          rewritePercent: escalatedSignal.aiLikePercent,
        });

      candidateSignals.push({
        stage: "repair",
        aiLikePercent: escalatedSignal.aiLikePercent,
        status: escalatedPasses
          ? "pass_below_threshold"
          : "fail_insufficient_reduction",
        rejected: !escalatedPasses,
        reason: [
          "Strong-model escalation fact and naturalness gate.",
          ...deterministic.blockingIssues,
          ...policyGate.issues.map((issue) => issue.message),
          ...factCheck.issues,
          ...factCheck.required_repairs,
        ].join(" "),
      });

      if (escalatedPasses) {
        return {
          rewrittenText: escalatedEmail,
          changeSummary: [
            "Rebuilt the reply from extracted facts instead of paraphrasing the draft.",
            "Used one bounded escalation because the first final missed the fact gate.",
          ],
          riskNotes: review.risk_notes.length
            ? review.risk_notes
            : ["Review before sending."],
          naturalness: formatNaturalness(
            draftSignal.aiLikePercent,
            escalatedSignal.aiLikePercent,
          ),
          optimization: {
            internalStrategiesTried: 3,
            repairCandidatesTried: 1,
            rejectedCandidates: 1,
            userUsageCharged: 1,
            selectionStatus: "passed",
            diagnosisTags,
            rewritePlanSummary,
            attemptLedger,
            candidateSignals,
          },
        };
      }

      recordFailedAttempt(attemptLedger, {
        strategy: repairDecision.strategy,
        modelRole: "strong_escalation",
        modelName: config.models.strong_escalation,
        thinkingMode: "thinking_high",
        candidateText: escalatedEmail,
        failureAnalysis: [
          ...escalatedDeterministic.blockingIssues,
          ...escalatedPolicyGate.issues.map((issue) => issue.message),
          ...escalatedFactCheck.issues,
          ...escalatedFactCheck.required_repairs,
        ].join(" "),
        failureKinds: [
          ...failureKindsFromIssues(escalatedDeterministic.blockingIssues),
          ...failureKindsFromFactCheck(escalatedFactCheck),
          ...escalatedPolicyGate.issues.map((issue) => issue.kind),
          ...(escalatedSignal.aiLikePercent === null
            ? (["provider_unavailable"] as RewriteFailureKind[])
            : (["naturalness_not_improved"] as RewriteFailureKind[])),
        ],
        factGateResult: escalatedFactSafe ? "pass" : "fail",
        structureGateResult: escalatedDeterministic.safe
          ? "pass"
          : `fail:${escalatedDeterministic.blockingIssues.join(";")}`,
        policyIntentGateResult: escalatedPolicyGate.safe
          ? "pass"
          : `fail:${escalatedPolicyGate.issues.map((issue) => issue.kind).join(";")}`,
        saplingResult: saplingSummary(
          escalatedSignal.aiLikePercent,
          escalatedPasses ? "pass" : "fail",
        ),
        nextStrategyDecision: "facts_first_reconstruct",
      });

      const fallbackAttempt = await tryGuaranteedFallback({
        config,
        draftPercent: draftSignal.aiLikePercent,
        facts,
        input,
        styleCard,
        telemetry,
      });

	      candidateSignals.push({
	        stage: "fallback",
	        aiLikePercent: fallbackAttempt.signal.aiLikePercent,
	        status: fallbackAttempt.naturalnessSafe
	          ? "pass_below_threshold"
	          : "fail_insufficient_reduction",
	        rejected: !fallbackAttempt.passes,
        reason: "Deterministic facts-first fallback after escalation miss.",
      });

      if (fallbackAttempt.passes) {
        return buildPassedResponse({
          attemptLedger,
          rewrittenText: fallbackAttempt.fallback.rewrittenText,
          changeSummary: [
            "Rebuilt the reply from extracted facts instead of paraphrasing the draft.",
            "Used the facts-first fallback after the model escalation missed the quality gate.",
          ],
          riskNotes: fallbackAttempt.fallback.riskNotes.length
            ? fallbackAttempt.fallback.riskNotes
            : ["Review before sending."],
          draftPercent: draftSignal.aiLikePercent,
          rewritePercent: fallbackAttempt.signal.aiLikePercent,
          internalStrategiesTried: 4,
          repairCandidatesTried: 2,
          rejectedCandidates: 2,
          diagnosisTags,
          rewritePlanSummary,
          candidateSignals,
        });
      }

      throwQualityFailure({
        attemptLedger,
        candidateSignals,
        diagnosisTags,
        draftPercent: draftSignal.aiLikePercent,
        rewritePercent: fallbackAttempt.signal.aiLikePercent,
        rejectedCandidates: 3,
        repairCandidatesTried: 2,
        rewritePlanSummary,
        reason: fallbackAttempt.factSafe
          ? "naturalness_gate_failed"
          : "fact_check_failed",
      });
    }

    throwQualityFailure({
      attemptLedger,
      candidateSignals,
      diagnosisTags,
      draftPercent: draftSignal.aiLikePercent,
      rewritePercent: null,
      rejectedCandidates: 1,
      repairCandidatesTried: 0,
      rewritePlanSummary,
      reason: "fact_check_failed",
    });
  }

  const finalSignal = await measureWritingSignal(finalEmail, {
    calibrateSentenceScores: true,
    role: "final_signal",
    telemetry,
  });
  candidateSignals.push({
    stage: "initial",
    aiLikePercent: finalSignal.aiLikePercent,
    status: naturalnessPasses({
      config,
      draftPercent: draftSignal.aiLikePercent,
      rewriteSignal: finalSignal,
      rewritePercent: finalSignal.aiLikePercent,
    })
      ? "pass_below_threshold"
      : "fail_insufficient_reduction",
    rejected: !naturalnessPasses({
      config,
      draftPercent: draftSignal.aiLikePercent,
      rewriteSignal: finalSignal,
      rewritePercent: finalSignal.aiLikePercent,
    }),
    reason: "Fact reconstruct final naturalness gate.",
  });

  if (
    naturalnessPasses({
      config,
      draftPercent: draftSignal.aiLikePercent,
      rewriteSignal: finalSignal,
      rewritePercent: finalSignal.aiLikePercent,
    })
  ) {
    return buildPassedResponse({
      attemptLedger,
      rewrittenText: finalEmail,
      changeSummary: [
        "Rebuilt the reply from extracted facts instead of paraphrasing the draft.",
        `Selected the ${bestKey.replaceAll("_", " ")} version after internal review.`,
      ],
      riskNotes: review.risk_notes.length
        ? review.risk_notes
        : ["Review before sending."],
      draftPercent: draftSignal.aiLikePercent,
      rewritePercent: finalSignal.aiLikePercent,
      internalStrategiesTried: 3,
      repairCandidatesTried: 0,
      rejectedCandidates: 0,
      diagnosisTags,
      rewritePlanSummary,
      candidateSignals,
    });
  }

  recordFailedAttempt(attemptLedger, {
    strategy: initialDecision.strategy,
    modelRole: "mid_writer",
    modelName: config.models.mid_writer,
    thinkingMode: "non_thinking",
    candidateText: finalEmail,
    failureAnalysis: "Final candidate missed the Naturalness Check gate.",
    failureKinds:
      finalSignal.aiLikePercent === null
        ? ["provider_unavailable"]
        : ["naturalness_not_improved"],
    factGateResult: "pass",
    structureGateResult: "pass",
    policyIntentGateResult: "pass",
    saplingResult: saplingSummary(finalSignal.aiLikePercent, "fail"),
    nextStrategyDecision: "targeted_sentence_repair",
  });

  const targetedRepairAttempt = await tryTargetedSentenceRepair({
    config,
    draftPercent: draftSignal.aiLikePercent,
    facts,
    finalEmail,
    finalSignal,
    input,
    scenario,
    styleCard,
    telemetry,
  });
  const targetedRepairTried = targetedRepairAttempt ? 1 : 0;

  if (targetedRepairAttempt) {
    candidateSignals.push({
      stage: "targeted_repair",
      aiLikePercent: targetedRepairAttempt.signal.aiLikePercent,
      status: targetedRepairAttempt.passes
        ? "pass_below_threshold"
        : "fail_insufficient_reduction",
      rejected: !targetedRepairAttempt.passes,
      reason:
        "Targeted repair of high-risk sentences after the first final missed the naturalness gate.",
    });

    if (targetedRepairAttempt.passes) {
      return buildPassedResponse({
        attemptLedger,
        rewrittenText: targetedRepairAttempt.repairedEmail,
        changeSummary: [
          "Rebuilt the reply from extracted facts instead of paraphrasing the draft.",
          "Repaired only the highest-risk sentences after the first final missed the internal quality bar.",
        ],
        riskNotes: review.risk_notes.length
          ? review.risk_notes
          : ["Review before sending."],
        draftPercent: draftSignal.aiLikePercent,
        rewritePercent: targetedRepairAttempt.signal.aiLikePercent,
        internalStrategiesTried: 4,
        repairCandidatesTried: 1,
        rejectedCandidates: 1,
        diagnosisTags,
        rewritePlanSummary,
        candidateSignals,
      });
    }

    recordFailedAttempt(attemptLedger, {
      strategy: "targeted_sentence_repair",
      modelRole: "mid_writer",
      modelName: config.models.mid_writer,
      thinkingMode: "non_thinking",
      candidateText: targetedRepairAttempt.repairedEmail,
      failureAnalysis:
        "Targeted sentence repair still missed the Naturalness Check gate.",
      failureKinds:
        targetedRepairAttempt.signal.aiLikePercent === null
          ? ["provider_unavailable"]
          : ["naturalness_not_improved"],
      factGateResult: targetedRepairAttempt.factSafe ? "pass" : "fail",
      structureGateResult: "pass",
      policyIntentGateResult: "pass",
      saplingResult: saplingSummary(
        targetedRepairAttempt.signal.aiLikePercent,
        "fail",
      ),
      nextStrategyDecision: "strong_model_restructure",
    });
  }

  if (config.maxEscalations > 0) {
    const naturalnessDecision = decideRepairStrategy({
      analysis,
      failures: ["naturalness_not_improved"],
      previousStrategies: [...previousStrategies, "targeted_sentence_repair"],
    });
    const naturalnessApproval = recordApprovedStrategy({
      budget,
      budgetState,
      decision: naturalnessDecision,
      previousStrategies,
    });
    budgetState = naturalnessApproval.budgetState;
    const escalatedEmail = await escalateCandidate({
      facts,
      scenario,
      styleCard,
      previousFinalEmail: finalEmail,
      reviewNotes: [...review.required_edits, ...review.risk_notes],
      strategy: naturalnessDecision.strategy,
      strategyGuidance: strategyGuidance(naturalnessDecision.strategy),
      attemptHistory: attemptLedger,
      config,
      telemetry,
    });
    const escalatedDeterministic = adaptiveGateCheck(
      input,
      facts,
      escalatedEmail,
      styleCard,
    );
    const escalatedPolicyGate = runPolicyIntentGate(input, escalatedEmail);
    const escalatedFactCheck = await llmFactCheck({
      facts,
      finalEmail: escalatedEmail,
      config,
      telemetry,
    });
    const escalatedSignal = await measureWritingSignal(escalatedEmail, {
      calibrateSentenceScores: true,
      role: "repair_signal",
      telemetry,
    });
    const escalatedPasses = factCheckSafe({
      deterministicSafe:
        escalatedDeterministic.safe && escalatedPolicyGate.safe,
      llmResult: escalatedFactCheck,
    }) &&
      naturalnessPasses({
        config,
        draftPercent: draftSignal.aiLikePercent,
        rewriteSignal: escalatedSignal,
        rewritePercent: escalatedSignal.aiLikePercent,
      });

    candidateSignals.push({
      stage: "repair",
      aiLikePercent: escalatedSignal.aiLikePercent,
      status: escalatedPasses
        ? "pass_below_threshold"
        : "fail_insufficient_reduction",
      rejected: !escalatedPasses,
      reason: "Strong-model escalation naturalness and fact gate.",
    });

    if (escalatedPasses) {
      return buildPassedResponse({
        attemptLedger,
        rewrittenText: escalatedEmail,
        changeSummary: [
          "Rebuilt the reply from extracted facts instead of paraphrasing the draft.",
          "Used one bounded escalation because the first final did not meet the internal quality bar.",
        ],
        riskNotes: review.risk_notes.length
          ? review.risk_notes
          : ["Review before sending."],
        draftPercent: draftSignal.aiLikePercent,
        rewritePercent: escalatedSignal.aiLikePercent,
        internalStrategiesTried: 3,
        repairCandidatesTried: targetedRepairTried + 1,
        rejectedCandidates: targetedRepairTried + 1,
        diagnosisTags,
        rewritePlanSummary,
        candidateSignals,
      });
    }

    recordFailedAttempt(attemptLedger, {
      strategy: naturalnessDecision.strategy,
      modelRole: "strong_escalation",
      modelName: config.models.strong_escalation,
      thinkingMode: "thinking_high",
      candidateText: escalatedEmail,
      failureAnalysis: [
        "Strong-model escalation missed the final gates.",
        ...escalatedDeterministic.blockingIssues,
        ...escalatedPolicyGate.issues.map((issue) => issue.message),
        ...escalatedFactCheck.issues,
        ...escalatedFactCheck.required_repairs,
      ].join(" "),
      failureKinds: [
        ...failureKindsFromIssues(escalatedDeterministic.blockingIssues),
        ...failureKindsFromFactCheck(escalatedFactCheck),
        ...escalatedPolicyGate.issues.map((issue) => issue.kind),
        ...(escalatedSignal.aiLikePercent === null
          ? (["provider_unavailable"] as RewriteFailureKind[])
          : (["naturalness_not_improved"] as RewriteFailureKind[])),
      ],
      factGateResult: escalatedFactCheck.safe_to_send ? "pass" : "fail",
      structureGateResult: escalatedDeterministic.safe
        ? "pass"
        : `fail:${escalatedDeterministic.blockingIssues.join(";")}`,
      policyIntentGateResult: escalatedPolicyGate.safe
        ? "pass"
        : `fail:${escalatedPolicyGate.issues.map((issue) => issue.kind).join(";")}`,
      saplingResult: saplingSummary(escalatedSignal.aiLikePercent, "fail"),
      nextStrategyDecision: "facts_first_reconstruct",
    });

    const fallbackAttempt = await tryGuaranteedFallback({
      config,
      draftPercent: draftSignal.aiLikePercent,
      facts,
      input,
      styleCard,
      telemetry,
    });

	    candidateSignals.push({
	      stage: "fallback",
	      aiLikePercent: fallbackAttempt.signal.aiLikePercent,
	      status: fallbackAttempt.naturalnessSafe
	        ? "pass_below_threshold"
	        : "fail_insufficient_reduction",
	      rejected: !fallbackAttempt.passes,
      reason: "Deterministic facts-first fallback after escalation miss.",
    });

    if (fallbackAttempt.passes) {
      return buildPassedResponse({
        attemptLedger,
        rewrittenText: fallbackAttempt.fallback.rewrittenText,
        changeSummary: [
          "Rebuilt the reply from extracted facts instead of paraphrasing the draft.",
          "Used the facts-first fallback after the model escalation missed the quality gate.",
        ],
        riskNotes: fallbackAttempt.fallback.riskNotes.length
          ? fallbackAttempt.fallback.riskNotes
          : ["Review before sending."],
        draftPercent: draftSignal.aiLikePercent,
        rewritePercent: fallbackAttempt.signal.aiLikePercent,
        internalStrategiesTried: 4,
        repairCandidatesTried: targetedRepairTried + 2,
        rejectedCandidates: targetedRepairTried + 2,
        diagnosisTags,
        rewritePlanSummary,
        candidateSignals,
      });
    }

    // Graceful degradation: repairs, escalation, and the facts-first fallback
    // all missed the naturalness gate, but finalEmail already cleared the fact
    // gate above, so it is fact-complete. Returning it (flagged best_available)
    // beats emptying a correct reply over a noisy style signal. Genuine fact
    // failures are handled earlier and still produce no output.
    return buildPassedResponse({
      attemptLedger,
      rewrittenText: finalEmail,
      changeSummary: [
        "Rebuilt the reply from extracted facts instead of paraphrasing the draft.",
        "Returned the fact-complete version after repairs could not lower the style signal further.",
      ],
      riskNotes: [
        ...(review.risk_notes.length ? review.risk_notes : []),
        "The style signal still reads a little formal, but every locked fact is preserved — review the tone before sending.",
      ],
      draftPercent: draftSignal.aiLikePercent,
      rewritePercent: finalSignal.aiLikePercent,
      internalStrategiesTried: 4,
      repairCandidatesTried: targetedRepairTried + 2,
      rejectedCandidates: targetedRepairTried + 3,
      diagnosisTags,
      rewritePlanSummary,
      candidateSignals,
      selectionStatus: "best_available",
    });
  }

  // Graceful degradation (escalations disabled): finalEmail cleared the fact
  // gate but missed naturalness and there is no further repair budget. Return
  // the fact-complete reply flagged best_available rather than emptying it.
  return buildPassedResponse({
    attemptLedger,
    rewrittenText: finalEmail,
    changeSummary: [
      "Rebuilt the reply from extracted facts instead of paraphrasing the draft.",
      "Returned the fact-complete version; the style signal reads a little formal but every fact is preserved.",
    ],
    riskNotes: [
      ...(review.risk_notes.length ? review.risk_notes : []),
      "The style signal still reads a little formal, but every locked fact is preserved — review the tone before sending.",
    ],
    draftPercent: draftSignal.aiLikePercent,
    rewritePercent: finalSignal.aiLikePercent,
    internalStrategiesTried: 3,
    repairCandidatesTried: targetedRepairTried,
    rejectedCandidates: 1,
    diagnosisTags,
    rewritePlanSummary,
    candidateSignals,
    selectionStatus: "best_available",
  });
  } catch (error) {
    if (error instanceof JsonCompletionQualityError) {
      throwQualityFailure({
        attemptLedger,
        candidateSignals,
        diagnosisTags,
        draftPercent: draftSignal.aiLikePercent,
        rewritePercent: null,
        rejectedCandidates: candidateSignals.filter((signal) => signal.rejected)
          .length,
        repairCandidatesTried: candidateSignals.length,
        rewritePlanSummary,
        reason: "model_json_unusable",
      });
    }

    throw error;
  }
}
