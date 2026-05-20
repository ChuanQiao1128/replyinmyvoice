import { optionalEnv } from "./env";
import { createId, getSql } from "./db";
import type { FactReconstructQualityError } from "./rewrite-pipeline/pipeline";
import type { RewriteResponsePayload } from "./rewrite-types";
import type { RewriteRequestInput } from "./validation";

type LearningUser = {
  id: string;
};

export type RewriteLearningStatus = "success" | "quality_failed";

export type RewriteLearningEvent = {
  user: LearningUser;
  input: RewriteRequestInput;
  status: RewriteLearningStatus;
  response?: RewriteResponsePayload;
  qualityError?: FactReconstructQualityError;
};

export type RewriteLearningSampleValues = {
  id: string;
  userId: string;
  scenario: string;
  tonePreset: string;
  messageToReplyTo: string | null;
  roughDraftReply: string;
  rewrittenText: string | null;
  draftAiLikePercent: number | null;
  rewriteAiLikePercent: number | null;
  changePoints: number | null;
  label: string | null;
  diagnosisTags: string;
  rewritePlanSummary: string | null;
  candidateSignals: string;
  internalStrategies: number;
  repairCandidates: number;
  rejectedCandidates: number;
  status: RewriteLearningStatus;
  errorCode: string | null;
};

export function isRewriteLearningEnabled() {
  return optionalEnv("REWRITE_LEARNING_LOG_ENABLED", "true") !== "false";
}

function nullableText(value: string) {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

export function buildRewriteLearningSample({
  user,
  input,
  status,
  response,
  qualityError,
}: RewriteLearningEvent): RewriteLearningSampleValues {
  const naturalness = response?.naturalness ?? qualityError?.naturalness;
  const optimization = response?.optimization;
  const candidateSignals =
    response?.optimization.candidateSignals ?? qualityError?.candidateSignals ?? [];

  return {
    id: createId(),
    userId: user.id,
    scenario: input.scenario,
    tonePreset: input.tonePreset,
    messageToReplyTo: nullableText(input.messageToReplyTo),
    roughDraftReply: input.roughDraftReply,
    rewrittenText: response?.rewrittenText ?? null,
    draftAiLikePercent: naturalness?.draftAiLikePercent ?? null,
    rewriteAiLikePercent: naturalness?.rewriteAiLikePercent ?? null,
    changePoints: naturalness?.changePoints ?? null,
    label: naturalness?.label ?? null,
    diagnosisTags: JSON.stringify(
      optimization?.diagnosisTags ?? qualityError?.diagnosisTags ?? [],
    ),
    rewritePlanSummary:
      optimization?.rewritePlanSummary ?? qualityError?.rewritePlanSummary ?? null,
    candidateSignals: JSON.stringify(candidateSignals),
    internalStrategies: optimization?.internalStrategiesTried ?? 0,
    repairCandidates:
      optimization?.repairCandidatesTried ??
      qualityError?.repairCandidatesTried ??
      0,
    rejectedCandidates:
      optimization?.rejectedCandidates ?? qualityError?.rejectedCandidates ?? 0,
    status,
    errorCode: status === "quality_failed" ? "quality_gate_failed" : null,
  };
}

export async function logRewriteLearningSample(event: RewriteLearningEvent) {
  if (!isRewriteLearningEnabled()) {
    return null;
  }

  const sample = buildRewriteLearningSample(event);
  const sql = getSql();

  await sql`
    INSERT INTO "RewriteLearningSample" (
      "id",
      "userId",
      "scenario",
      "tonePreset",
      "messageToReplyTo",
      "roughDraftReply",
      "rewrittenText",
      "draftAiLikePercent",
      "rewriteAiLikePercent",
      "changePoints",
      "label",
      "diagnosisTags",
      "rewritePlanSummary",
      "candidateSignals",
      "internalStrategies",
      "repairCandidates",
      "rejectedCandidates",
      "status",
      "errorCode"
    )
    VALUES (
      ${sample.id},
      ${sample.userId},
      ${sample.scenario},
      ${sample.tonePreset},
      ${sample.messageToReplyTo},
      ${sample.roughDraftReply},
      ${sample.rewrittenText},
      ${sample.draftAiLikePercent},
      ${sample.rewriteAiLikePercent},
      ${sample.changePoints},
      ${sample.label},
      ${sample.diagnosisTags},
      ${sample.rewritePlanSummary},
      ${sample.candidateSignals},
      ${sample.internalStrategies},
      ${sample.repairCandidates},
      ${sample.rejectedCandidates},
      ${sample.status},
      ${sample.errorCode}
    )
  `;

  return sample.id;
}

export async function tryLogRewriteLearningSample(event: RewriteLearningEvent) {
  try {
    return await logRewriteLearningSample(event);
  } catch (error) {
    console.warn("rewrite_learning_log_failed", {
      message: error instanceof Error ? error.message.slice(0, 180) : "Unknown",
    });
    return null;
  }
}
