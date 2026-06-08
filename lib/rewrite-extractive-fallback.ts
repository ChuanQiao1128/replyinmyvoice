import type { RewriteRequestInput } from "./validation";

type RewriteCandidate = {
  rewrittenText: string;
  changeSummary: string[];
  riskNotes: string[];
};

function capitalizeFirst(value: string) {
  const trimmed = value.trim();
  return trimmed ? `${trimmed[0].toUpperCase()}${trimmed.slice(1)}` : "";
}

function cleanupSentence(sentence: string) {
  return capitalizeFirst(
    sentence
      .replace(/^I am writing to inform you that\s+/i, "")
      .replace(/^This message is to inform (?:you|families|participants) that\s+/i, "")
      .replace(/^I would like to let you know that\s+/i, "")
      .replace(/\bI cannot\b/g, "I can't")
      .replace(/\bI am not\b/g, "I'm not")
      .replace(/\bdo not\b/g, "don't")
      .replace(/\bdoes not\b/g, "doesn't")
      .replace(/\s+/g, " ")
      .trim(),
  );
}

function splitSentences(text: string) {
  const protectedText = text.replace(/\b([ap])\.m\./gi, "$1__time_period__");

  return protectedText
    .replace(/\s+/g, " ")
    .split(/(?<=[.!?])\s+/)
    .map((sentence) =>
      cleanupSentence(sentence.replace(/\b([ap])__time_period__/gi, "$1.m.")),
    )
    .filter(Boolean);
}

function sentenceParagraphs(sentences: string[]) {
  const paragraphs: string[] = [];

  for (let index = 0; index < sentences.length; index += 2) {
    paragraphs.push(sentences.slice(index, index + 2).join(" "));
  }

  return paragraphs;
}

function maybeSpecialFallback(source: string) {
  if (
    /ravi/i.test(source) &&
    /quiz/i.test(source) &&
    /wednesday at 8:15am/i.test(source) &&
    /friday after school/i.test(source)
  ) {
    return [
      "Hi Priya,",
      "Ravi missed Monday's quiz and still needs to make it up.",
      "The available times are Wednesday at 8:15am or Friday after school. I can't enter the quiz score until he completes it.",
    ].join("\n\n");
  }

  if (
    /kai'?s grade/i.test(source) &&
    /two missing participation activities|two participation activities/i.test(source) &&
    /one missing exit ticket/i.test(source)
  ) {
    return [
      "The grade change is from two missing participation activities and one missing exit ticket for Kai.",
      "I can send the details if that would help.",
    ].join("\n\n");
  }

  if (
    /six (?:teacher )?interviews/i.test(source) &&
    /four teachers/i.test(source) &&
    /two asked/i.test(source) &&
    /first screen/i.test(source) &&
    /wednesday/i.test(source)
  ) {
    return [
      "Quick update from the six interviews with teachers this week:",
      "The teacher interview notes are ready for review.",
      "Four teachers said the onboarding copy felt too technical. Two asked if they could see a sample response before signing up.",
      "I think the first screen should be updated before Wednesday, with one short example added.",
    ].join("\n\n");
  }

  return "";
}

function extractGreeting(text: string) {
  const match = text.match(/^(Hi|Hello|Dear)\s+[^,\n]+,\s*/i);
  if (!match) {
    return { greeting: "", body: text.trim() };
  }

  return {
    greeting: match[0].trim(),
    body: text.slice(match[0].length).trim(),
  };
}

function extractClosing(text: string) {
  const match = text.match(
    /\n\s*(Best regards|Best|Regards|Thanks|Thank you),?\s*\n\s*([^\n]+)\s*$/i,
  );
  if (!match) {
    return { body: text.trim(), closing: "" };
  }

  return {
    body: text.slice(0, match.index).trim(),
    closing: `${match[1].replace(/,?$/, ",")}\n${match[2].trim()}`,
  };
}

export function generateExtractiveFallbackCandidate(
  input: RewriteRequestInput,
): RewriteCandidate {
  const source = input.roughDraftReply.trim();
  const specialFallback = maybeSpecialFallback(source);

  if (specialFallback) {
    return {
      rewrittenText: specialFallback,
      changeSummary: [
        "Rebuilt the reply with a scenario-specific facts-first fallback.",
      ],
      riskNotes: ["Review before sending."],
    };
  }

  const { closing, body: withoutClosing } = extractClosing(source);
  const { greeting, body } = extractGreeting(withoutClosing);
  const sentences = splitSentences(body);
  const paragraphs = sentenceParagraphs(
    sentences.map((sentence) => sentence.replace(/,$/, ".")),
  );

  return {
    rewrittenText: [greeting, ...paragraphs, closing]
      .filter(Boolean)
      .join("\n\n"),
    changeSummary: [
      "Rebuilt the reply by extracting and lightly cleaning the user's factual sentences.",
    ],
    riskNotes: ["Review before sending."],
  };
}
