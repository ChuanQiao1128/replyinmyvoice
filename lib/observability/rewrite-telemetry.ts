import { normalizeRewriteRequestFailureReason } from "../rewrite-failure-reasons";
import type { FactReconstructQualityError } from "../rewrite-pipeline/pipeline";
import type { RewriteResponsePayload } from "../rewrite-types";
import type { RewriteRequestInput } from "../validation";
import type { ProviderCallTelemetry } from "./rewrite-cost";
import { summarizeProviderCalls } from "./rewrite-cost";

function createId(): string {
  return crypto.randomUUID();
}

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
        errorCode: normalizeRewriteRequestFailureReason({
          errorCode: args.errorCode,
          qualityReason: args.qualityError?.reason,
          status: args.status,
        }),
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
