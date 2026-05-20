import { optionalEnv } from "../env";
import { createId, getSql } from "../db";
import type { FactReconstructQualityError } from "../rewrite-pipeline/pipeline";
import type { RewriteResponsePayload } from "../rewrite-types";
import type { RewriteRequestInput } from "../validation";
import type { ProviderCallTelemetry } from "./rewrite-cost";
import { summarizeProviderCalls } from "./rewrite-cost";

export type RewriteCostLogStatus =
  | "success"
  | "quality_failed"
  | "server_failed";

export type RewriteCostLogInsert = {
  id: string;
  userId: string | null;
  learningSampleId: string | null;
  requestId: string;
  strategyVersion: string;
  scenario: string;
  tonePreset: string;
  status: RewriteCostLogStatus;
  errorCode: string | null;
  startedAt: Date;
  finishedAt: Date | null;
  durationMs: number | null;
  inputCharCount: number;
  draftWordCount: number;
  rewriteWordCount: number | null;
  draftAiLikePercent: number | null;
  rewriteAiLikePercent: number | null;
  changePoints: number | null;
  internalStrategies: number;
  repairCandidates: number;
  rejectedCandidates: number;
  usedEscalation: boolean;
  openAiInputTokens: number;
  openAiOutputTokens: number;
  openAiCostUsd: number;
  saplingCallCount: number;
  saplingCharacters: number;
  saplingCostUsd: number;
  totalEstimatedCostUsd: number;
  modelsUsedJson: string;
  providerCallsJson: string;
  providerCalls: ProviderCallTelemetry[];
};

export type RewriteTelemetryFinishArgs = {
  userId?: string | null;
  learningSampleId?: string | null;
  input: RewriteRequestInput;
  status: RewriteCostLogStatus;
  errorCode?: string | null;
  response?: RewriteResponsePayload;
  qualityError?: FactReconstructQualityError;
  strategyVersion: string;
  finishedAt?: Date;
};

export type RewriteTelemetryCollector = {
  requestId: string;
  startedAt: Date;
  recordProviderCall(call: ProviderCallTelemetry): void;
  finish(args: RewriteTelemetryFinishArgs): RewriteCostLogInsert;
};

function wordCount(text: string | null | undefined) {
  return (text ?? "").trim().split(/\s+/).filter(Boolean).length;
}

function inputCharCount(input: RewriteRequestInput) {
  return [
    input.messageToReplyTo,
    input.roughDraftReply,
    input.audience,
    input.purpose,
    input.whatHappened,
    input.factsToPreserve,
  ].reduce((sum, value) => sum + (value ?? "").length, 0);
}

function usedEscalation(calls: ProviderCallTelemetry[]) {
  return calls.some((call) => call.provider === "openai" && call.role === "strong_escalation");
}

export function createRewriteTelemetryCollector({
  requestId = createId(),
  startedAt = new Date(),
}: {
  requestId?: string;
  startedAt?: Date;
} = {}): RewriteTelemetryCollector {
  const providerCalls: ProviderCallTelemetry[] = [];

  return {
    requestId,
    startedAt,
    recordProviderCall(call) {
      providerCalls.push(call);
    },
    finish(args) {
      const finishedAt = args.finishedAt ?? new Date();
      const naturalness = args.response?.naturalness ?? args.qualityError?.naturalness;
      const optimization = args.response?.optimization;
      const summary = summarizeProviderCalls(providerCalls);

      return {
        id: createId(),
        userId: args.userId ?? null,
        learningSampleId: args.learningSampleId ?? null,
        requestId,
        strategyVersion: args.strategyVersion,
        scenario: args.input.scenario,
        tonePreset: args.input.tonePreset,
        status: args.status,
        errorCode: args.errorCode ?? (
          args.status === "quality_failed" ? "quality_gate_failed" : null
        ),
        startedAt,
        finishedAt,
        durationMs: Math.max(0, finishedAt.getTime() - startedAt.getTime()),
        inputCharCount: inputCharCount(args.input),
        draftWordCount: wordCount(args.input.roughDraftReply),
        rewriteWordCount: args.response?.rewrittenText
          ? wordCount(args.response.rewrittenText)
          : null,
        draftAiLikePercent: naturalness?.draftAiLikePercent ?? null,
        rewriteAiLikePercent: naturalness?.rewriteAiLikePercent ?? null,
        changePoints: naturalness?.changePoints ?? null,
        internalStrategies: optimization?.internalStrategiesTried ?? 0,
        repairCandidates:
          optimization?.repairCandidatesTried ??
          args.qualityError?.repairCandidatesTried ??
          0,
        rejectedCandidates:
          optimization?.rejectedCandidates ?? args.qualityError?.rejectedCandidates ?? 0,
        usedEscalation: usedEscalation(providerCalls),
        openAiInputTokens: summary.openAiInputTokens,
        openAiOutputTokens: summary.openAiOutputTokens,
        openAiCostUsd: summary.openAiCostUsd,
        saplingCallCount: summary.saplingCallCount,
        saplingCharacters: summary.saplingCharacters,
        saplingCostUsd: summary.saplingCostUsd,
        totalEstimatedCostUsd: summary.totalEstimatedCostUsd,
        modelsUsedJson: JSON.stringify(summary.modelsUsed),
        providerCallsJson: JSON.stringify(providerCalls),
        providerCalls: [...providerCalls],
      };
    },
  };
}

export function serializeRewriteCostLog(log: RewriteCostLogInsert) {
  return {
    ...log,
    providerCallsJson: JSON.stringify(log.providerCalls),
    modelsUsedJson: log.modelsUsedJson,
  };
}

function isCostLoggingEnabled() {
  return optionalEnv("REWRITE_COST_LOG_ENABLED", "true") !== "false";
}

export async function persistRewriteCostLog(log: RewriteCostLogInsert) {
  if (!isCostLoggingEnabled()) {
    return null;
  }

  const sql = getSql();

  await sql`
    INSERT INTO "RewriteCostLog" (
      "id",
      "userId",
      "learningSampleId",
      "requestId",
      "strategyVersion",
      "scenario",
      "tonePreset",
      "status",
      "errorCode",
      "startedAt",
      "finishedAt",
      "durationMs",
      "inputCharCount",
      "draftWordCount",
      "rewriteWordCount",
      "draftAiLikePercent",
      "rewriteAiLikePercent",
      "changePoints",
      "internalStrategies",
      "repairCandidates",
      "rejectedCandidates",
      "usedEscalation",
      "openAiInputTokens",
      "openAiOutputTokens",
      "openAiCostUsd",
      "saplingCallCount",
      "saplingCharacters",
      "saplingCostUsd",
      "totalEstimatedCostUsd",
      "modelsUsedJson",
      "providerCallsJson",
      "createdAt",
      "updatedAt"
    )
    VALUES (
      ${log.id},
      ${log.userId},
      ${log.learningSampleId},
      ${log.requestId},
      ${log.strategyVersion},
      ${log.scenario},
      ${log.tonePreset},
      ${log.status},
      ${log.errorCode},
      ${log.startedAt},
      ${log.finishedAt},
      ${log.durationMs},
      ${log.inputCharCount},
      ${log.draftWordCount},
      ${log.rewriteWordCount},
      ${log.draftAiLikePercent},
      ${log.rewriteAiLikePercent},
      ${log.changePoints},
      ${log.internalStrategies},
      ${log.repairCandidates},
      ${log.rejectedCandidates},
      ${log.usedEscalation},
      ${log.openAiInputTokens},
      ${log.openAiOutputTokens},
      ${log.openAiCostUsd},
      ${log.saplingCallCount},
      ${log.saplingCharacters},
      ${log.saplingCostUsd},
      ${log.totalEstimatedCostUsd},
      ${log.modelsUsedJson},
      ${log.providerCallsJson},
      now(),
      now()
    )
    ON CONFLICT ("requestId") DO UPDATE SET
      "status" = EXCLUDED."status",
      "errorCode" = EXCLUDED."errorCode",
      "finishedAt" = EXCLUDED."finishedAt",
      "durationMs" = EXCLUDED."durationMs",
      "draftAiLikePercent" = EXCLUDED."draftAiLikePercent",
      "rewriteAiLikePercent" = EXCLUDED."rewriteAiLikePercent",
      "changePoints" = EXCLUDED."changePoints",
      "internalStrategies" = EXCLUDED."internalStrategies",
      "repairCandidates" = EXCLUDED."repairCandidates",
      "rejectedCandidates" = EXCLUDED."rejectedCandidates",
      "usedEscalation" = EXCLUDED."usedEscalation",
      "openAiInputTokens" = EXCLUDED."openAiInputTokens",
      "openAiOutputTokens" = EXCLUDED."openAiOutputTokens",
      "openAiCostUsd" = EXCLUDED."openAiCostUsd",
      "saplingCallCount" = EXCLUDED."saplingCallCount",
      "saplingCharacters" = EXCLUDED."saplingCharacters",
      "saplingCostUsd" = EXCLUDED."saplingCostUsd",
      "totalEstimatedCostUsd" = EXCLUDED."totalEstimatedCostUsd",
      "modelsUsedJson" = EXCLUDED."modelsUsedJson",
      "providerCallsJson" = EXCLUDED."providerCallsJson",
      "updatedAt" = now()
  `;

  for (const call of log.providerCalls) {
    await sql`
      INSERT INTO "RewriteProviderCall" (
        "id",
        "costLogId",
        "provider",
        "role",
        "model",
        "inputTokens",
        "outputTokens",
        "characters",
        "estimatedCostUsd",
        "latencyMs",
        "success",
        "errorCode",
        "createdAt"
      )
      VALUES (
        ${createId()},
        ${log.id},
        ${call.provider},
        ${call.role},
        ${call.model ?? null},
        ${call.inputTokens ?? null},
        ${call.outputTokens ?? null},
        ${call.characters ?? null},
        ${call.estimatedCostUsd},
        ${call.latencyMs ?? null},
        ${call.success},
        ${call.errorCode ?? null},
        now()
      )
    `;
  }

  return log.id;
}

export async function tryPersistRewriteCostLog(log: RewriteCostLogInsert) {
  try {
    return await persistRewriteCostLog(log);
  } catch (error) {
    console.warn("rewrite_cost_log_failed", {
      message: error instanceof Error ? error.message.slice(0, 180) : "Unknown",
    });
    return null;
  }
}
