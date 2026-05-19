import { generateGuaranteedRewriteCandidate } from "../openai";
import { generateExtractiveFallbackCandidate } from "../rewrite-extractive-fallback";
import type { RewriteResponsePayload } from "../rewrite-types";
import { formatNaturalness, measureWritingSignal } from "../writing-signal";
import type { RewriteRequestInput } from "../validation";
import {
  deterministicCheck,
  llmFactCheckPasses,
  reviewerScorePasses,
} from "./checks";
import { getFactReconstructConfig } from "./config";
import {
  classifyScenario,
  escalateCandidate,
  extractFacts,
  finalizeCandidate,
  generateCandidates,
  llmFactCheck,
  reviewCandidates,
} from "./model";
import { getStyleCard } from "./style-cards";
import type {
  CandidateKey,
  FactReconstructConfig,
  LlmFactCheckResult,
  ReviewResult,
} from "./types";

type FactReconstructQualityFailureReason =
  | "signal_unavailable"
  | "fact_check_failed"
  | "reviewer_threshold_failed"
  | "naturalness_gate_failed";

export class FactReconstructQualityError extends Error {
  naturalness: RewriteResponsePayload["naturalness"];
  rejectedCandidates: number;
  repairCandidatesTried: number;
  candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"];
  diagnosisTags: RewriteResponsePayload["optimization"]["diagnosisTags"];
  rewritePlanSummary: string;
  reason: FactReconstructQualityFailureReason;

  constructor({
    naturalness,
    rejectedCandidates,
    repairCandidatesTried,
    candidateSignals,
    diagnosisTags,
    rewritePlanSummary,
    reason,
  }: {
    naturalness: RewriteResponsePayload["naturalness"];
    rejectedCandidates: number;
    repairCandidatesTried: number;
    candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"];
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
    this.diagnosisTags = diagnosisTags;
    this.rewritePlanSummary = rewritePlanSummary;
    this.reason = reason;
  }
}

function selectedScore(review: ReviewResult) {
  return review.scores[review.best_candidate_key];
}

function naturalnessPasses({
  config,
  draftPercent,
  rewritePercent,
}: {
  config: FactReconstructConfig;
  draftPercent: number | null;
  rewritePercent: number | null;
}) {
  if (draftPercent === null || rewritePercent === null) {
    return false;
  }

  if (draftPercent <= config.naturalnessThreshold) {
    return rewritePercent <= draftPercent;
  }

  return rewritePercent <= config.naturalnessThreshold;
}

function throwQualityFailure({
  candidateSignals,
  diagnosisTags,
  draftPercent,
  reason,
  rejectedCandidates,
  repairCandidatesTried,
  rewritePercent,
  rewritePlanSummary,
}: {
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

function withRestoredRecipientGreeting(
  fallback: ReturnType<typeof generateGuaranteedRewriteCandidate>,
  facts: Parameters<typeof deterministicCheck>[1],
) {
  const recipient = facts.recipient_name.trim();
  if (!recipient) {
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

function buildPassedResponse({
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
}: {
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
      selectionStatus: "passed",
      diagnosisTags,
      rewritePlanSummary,
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
}: {
  config: FactReconstructConfig;
  draftPercent: number;
  facts: Parameters<typeof deterministicCheck>[1];
  input: RewriteRequestInput;
  styleCard: Parameters<typeof deterministicCheck>[3];
}) {
  const fallbackCandidates = [
    withRestoredRecipientGreeting(generateExtractiveFallbackCandidate(input), facts),
    withRestoredRecipientGreeting(generateGuaranteedRewriteCandidate(input), facts),
  ];
  let bestAttempt: {
    fallback: (typeof fallbackCandidates)[number];
    factSafe: boolean;
    passes: boolean;
    signal: Awaited<ReturnType<typeof measureWritingSignal>>;
  } | null = null;

  for (const fallback of fallbackCandidates) {
    const deterministic = deterministicCheck(
      input,
      facts,
      fallback.rewrittenText,
      styleCard,
    );
    const factCheck = await llmFactCheck({
      facts,
      finalEmail: fallback.rewrittenText,
      config,
    });
    const signal = await measureWritingSignal(fallback.rewrittenText);
    const factSafe = factCheckSafe({
      deterministicSafe: deterministic.safe,
      llmResult: factCheck,
    });
    const passes = factSafe &&
      naturalnessPasses({
        config,
        draftPercent,
        rewritePercent: signal.aiLikePercent,
      });
    const attempt = { fallback, factSafe, passes, signal };

    if (passes) {
      return attempt;
    }

    if (
      !bestAttempt ||
      (signal.aiLikePercent !== null &&
        (bestAttempt.signal.aiLikePercent === null ||
          signal.aiLikePercent < bestAttempt.signal.aiLikePercent))
    ) {
      bestAttempt = attempt;
    }
  }

  return bestAttempt ?? {
    fallback: fallbackCandidates[0],
    factSafe: false,
    passes: false,
    signal: { aiLikePercent: null },
  };
}

export async function rewriteWithFactReconstruct(
  input: RewriteRequestInput,
): Promise<RewriteResponsePayload> {
  const config = getFactReconstructConfig();
  const draftSignal = await measureWritingSignal(input.roughDraftReply);
  const candidateSignals: RewriteResponsePayload["optimization"]["candidateSignals"] = [];

  if (draftSignal.aiLikePercent === null) {
    throwQualityFailure({
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

  const facts = await extractFacts(input, config);
  const scenario = await classifyScenario(facts, config);
  const styleCard = getStyleCard(scenario);
  const diagnosisTags = diagnosisTagsForScenario(scenario.domain);
  const rewritePlanSummary = `Fact reconstruct using ${styleCard.style_card_id}.`;

  const candidates = await generateCandidates({
    facts,
    scenario,
    styleCard,
    config,
  });
  const review = await reviewCandidates({
    facts,
    scenario,
    styleCard,
    candidates,
    config,
  });
  const bestKey: CandidateKey = review.best_candidate_key;
  const bestCandidate = candidates[bestKey];
  const score = selectedScore(review);

  if (!reviewerScorePasses(score)) {
    const fallbackAttempt = await tryGuaranteedFallback({
      config,
      draftPercent: draftSignal.aiLikePercent,
      facts,
      input,
      styleCard,
    });

    candidateSignals.push({
      stage: "fallback",
      aiLikePercent: fallbackAttempt.signal.aiLikePercent,
      status: fallbackAttempt.passes
        ? "pass_below_threshold"
        : "fail_insufficient_reduction",
      rejected: !fallbackAttempt.passes,
      reason: "Deterministic facts-first fallback after reviewer threshold miss.",
    });

    if (fallbackAttempt.passes) {
      return buildPassedResponse({
        rewrittenText: fallbackAttempt.fallback.rewrittenText,
        changeSummary: [
          "Rebuilt the reply from extracted facts instead of paraphrasing the draft.",
          "Used the facts-first fallback after internal review rejected the model candidates.",
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
      candidateSignals,
      diagnosisTags,
      draftPercent: draftSignal.aiLikePercent,
      rewritePercent: fallbackAttempt.signal.aiLikePercent,
      rejectedCandidates: 4,
      repairCandidatesTried: 1,
      rewritePlanSummary,
      reason: fallbackAttempt.factSafe
        ? "naturalness_gate_failed"
        : "fact_check_failed",
    });
  }

  const finalEmail = await finalizeCandidate({
    facts,
    scenario,
    styleCard,
    selectedCandidate: bestCandidate,
    requiredEdits: review.required_edits,
    config,
  });
  const deterministic = deterministicCheck(input, facts, finalEmail, styleCard);
  const factCheck = await llmFactCheck({
    facts,
    finalEmail,
    config,
  });

  if (!factCheckSafe({ deterministicSafe: deterministic.safe, llmResult: factCheck })) {
    if (config.maxEscalations > 0) {
      const escalatedEmail = await escalateCandidate({
        facts,
        scenario,
        styleCard,
        previousFinalEmail: finalEmail,
        reviewNotes: [
          ...review.required_edits,
          ...review.risk_notes,
          ...deterministic.issues,
          ...factCheck.issues,
          ...factCheck.required_repairs,
        ],
        config,
      });
      const escalatedDeterministic = deterministicCheck(
        input,
        facts,
        escalatedEmail,
        styleCard,
      );
      const escalatedFactCheck = await llmFactCheck({
        facts,
        finalEmail: escalatedEmail,
        config,
      });
      const escalatedSignal = await measureWritingSignal(escalatedEmail);
      const escalatedFactSafe = factCheckSafe({
        deterministicSafe: escalatedDeterministic.safe,
        llmResult: escalatedFactCheck,
      });
      const escalatedPasses = escalatedFactSafe &&
        naturalnessPasses({
          config,
          draftPercent: draftSignal.aiLikePercent,
          rewritePercent: escalatedSignal.aiLikePercent,
        });

      candidateSignals.push({
        stage: "repair",
        aiLikePercent: escalatedSignal.aiLikePercent,
        status: escalatedPasses
          ? "pass_below_threshold"
          : "fail_insufficient_reduction",
        rejected: !escalatedPasses,
        reason: "Strong-model escalation fact and naturalness gate.",
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
            candidateSignals,
          },
        };
      }

      const fallbackAttempt = await tryGuaranteedFallback({
        config,
        draftPercent: draftSignal.aiLikePercent,
        facts,
        input,
        styleCard,
      });

      candidateSignals.push({
        stage: "fallback",
        aiLikePercent: fallbackAttempt.signal.aiLikePercent,
        status: fallbackAttempt.passes
          ? "pass_below_threshold"
          : "fail_insufficient_reduction",
        rejected: !fallbackAttempt.passes,
        reason: "Deterministic facts-first fallback after escalation miss.",
      });

      if (fallbackAttempt.passes) {
        return buildPassedResponse({
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

  const finalSignal = await measureWritingSignal(finalEmail);
  candidateSignals.push({
    stage: "initial",
    aiLikePercent: finalSignal.aiLikePercent,
    status: naturalnessPasses({
      config,
      draftPercent: draftSignal.aiLikePercent,
      rewritePercent: finalSignal.aiLikePercent,
    })
      ? "pass_below_threshold"
      : "fail_insufficient_reduction",
    rejected: !naturalnessPasses({
      config,
      draftPercent: draftSignal.aiLikePercent,
      rewritePercent: finalSignal.aiLikePercent,
    }),
    reason: "Fact reconstruct final naturalness gate.",
  });

  if (
    naturalnessPasses({
      config,
      draftPercent: draftSignal.aiLikePercent,
      rewritePercent: finalSignal.aiLikePercent,
    })
  ) {
    return buildPassedResponse({
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

  if (config.maxEscalations > 0) {
    const escalatedEmail = await escalateCandidate({
      facts,
      scenario,
      styleCard,
      previousFinalEmail: finalEmail,
      reviewNotes: [...review.required_edits, ...review.risk_notes],
      config,
    });
    const escalatedDeterministic = deterministicCheck(
      input,
      facts,
      escalatedEmail,
      styleCard,
    );
    const escalatedFactCheck = await llmFactCheck({
      facts,
      finalEmail: escalatedEmail,
      config,
    });
    const escalatedSignal = await measureWritingSignal(escalatedEmail);
    const escalatedPasses = factCheckSafe({
      deterministicSafe: escalatedDeterministic.safe,
      llmResult: escalatedFactCheck,
    }) &&
      naturalnessPasses({
        config,
        draftPercent: draftSignal.aiLikePercent,
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
        repairCandidatesTried: 1,
        rejectedCandidates: 1,
        diagnosisTags,
        rewritePlanSummary,
        candidateSignals,
      });
    }

    const fallbackAttempt = await tryGuaranteedFallback({
      config,
      draftPercent: draftSignal.aiLikePercent,
      facts,
      input,
      styleCard,
    });

    candidateSignals.push({
      stage: "fallback",
      aiLikePercent: fallbackAttempt.signal.aiLikePercent,
      status: fallbackAttempt.passes
        ? "pass_below_threshold"
        : "fail_insufficient_reduction",
      rejected: !fallbackAttempt.passes,
      reason: "Deterministic facts-first fallback after escalation miss.",
    });

    if (fallbackAttempt.passes) {
      return buildPassedResponse({
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
    candidateSignals,
    diagnosisTags,
    draftPercent: draftSignal.aiLikePercent,
    rewritePercent: finalSignal.aiLikePercent,
    rejectedCandidates: 1,
    repairCandidatesTried: 0,
    rewritePlanSummary,
    reason: "naturalness_gate_failed",
  });
}
