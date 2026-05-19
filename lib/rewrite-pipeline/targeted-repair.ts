import type { WritingSignalResult } from "../writing-signal";

export type HighRiskSentence = {
  sentence: string;
  aiLikePercent: number;
};

function isRepairableSentence(sentence: string) {
  const trimmed = sentence.trim();
  if (trimmed.length < 24) {
    return false;
  }

  return /[a-z]/i.test(trimmed);
}

export function selectHighRiskSentences({
  maxSentences = 3,
  signal,
  threshold,
}: {
  maxSentences?: number;
  signal: WritingSignalResult;
  threshold: number;
}): HighRiskSentence[] {
  return (signal.sentenceScores ?? [])
    .filter(
      (sentenceScore) =>
        sentenceScore.aiLikePercent > threshold &&
        isRepairableSentence(sentenceScore.sentence),
    )
    .sort((left, right) => right.aiLikePercent - left.aiLikePercent)
    .slice(0, maxSentences)
    .map((sentenceScore) => ({
      sentence: sentenceScore.sentence.trim(),
      aiLikePercent: sentenceScore.aiLikePercent,
    }));
}
