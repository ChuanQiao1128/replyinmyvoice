import type { ScenarioClassification, StyleCard } from "./types";

export const styleCards: Record<string, StyleCard> = {
  teacher_parent_email_default: {
    style_card_id: "teacher_parent_email_default",
    voice: "clear, professional, warm but not overly emotional",
    paragraph_style: "short paragraphs, each with one purpose",
    sentence_style:
      "mostly medium-length sentences with a few shorter direct sentences",
    opening_style: "acknowledge the parent's concern without overdoing empathy",
    body_style: "state the facts clearly, then give practical next steps",
    closing_style: "supportive but concise",
    good_phrases: [
      "Thank you for reaching out.",
      "I understand your concern.",
      "At this point,",
      "I would suggest that",
      "Just to clarify,",
    ],
    phrases_to_avoid_or_limit: [
      "Thank you for your partnership",
      "moving forward",
      "support his success",
      "take ownership",
      "I completely understand",
      "Please do not hesitate to reach out",
      "I wanted to take a moment to",
      "as we work together",
    ],
    rules: [
      "Do not blame the student.",
      "Do not overpromise.",
      "Preserve assignment names, deadlines, and support times exactly.",
      "Make the next step clear.",
      "Avoid sounding like a school policy document.",
      "Do not add motivational filler.",
    ],
  },
  general_professional_reply: {
    style_card_id: "general_professional_reply",
    voice: "natural, clear, professional, and specific",
    paragraph_style: "short paragraphs with one clear idea each",
    sentence_style: "plain sentences with natural variation",
    opening_style: "start with the actual situation instead of a generic opener",
    body_style: "keep concrete details and practical next steps",
    closing_style: "concise and appropriate to the relationship",
    good_phrases: ["Thanks for the note.", "Quick update:", "Just to clarify,"],
    phrases_to_avoid_or_limit: [
      "I hope this email finds you well",
      "Thank you for your partnership",
      "moving forward",
      "at your earliest convenience",
      "please do not hesitate",
    ],
    rules: [
      "Do not add names, dates, promises, or outcomes.",
      "Keep the user's intent intact.",
      "Prefer a realistic email over a polished template.",
    ],
  },
  workplace_followup_default: {
    style_card_id: "workplace_followup_default",
    voice: "direct, practical, and collegial",
    paragraph_style: "compact paragraphs or short blocks",
    sentence_style: "clear and action-oriented",
    opening_style: "lead with status or decision",
    body_style: "include blockers, owners, and dates when provided",
    closing_style: "state the next action without extra filler",
    good_phrases: ["Quick update:", "The main blocker is", "Next step:"],
    phrases_to_avoid_or_limit: [
      "circle back",
      "align on next steps",
      "moving forward",
      "touch base",
    ],
    rules: [
      "Preserve owners, blockers, dates, and requested actions.",
      "Do not turn uncertainty into certainty.",
      "Keep the update easy to scan.",
    ],
  },
  customer_support_resolution: {
    style_card_id: "customer_support_resolution",
    voice: "calm, specific, and helpful",
    paragraph_style: "short paragraphs for issue, explanation, and next step",
    sentence_style: "plain and concrete",
    opening_style: "acknowledge the concrete issue without generic empathy",
    body_style: "explain only what is known from the facts",
    closing_style: "give the next step or what to send",
    good_phrases: ["Thanks for laying this out.", "The issue appears to be"],
    phrases_to_avoid_or_limit: [
      "I see how this can be confusing",
      "From what you described",
      "For next steps",
      "rest assured",
    ],
    rules: [
      "Do not invent refunds, account changes, timelines, or policies.",
      "Preserve amounts, plan names, dates, and account identifiers.",
      "Keep the explanation specific enough to answer the customer's question.",
    ],
  },
  sales_followup_warm: {
    style_card_id: "sales_followup_warm",
    voice: "warm, low-pressure, and specific",
    paragraph_style: "brief email-thread paragraphs",
    sentence_style: "simple and relationship-aware",
    opening_style: "acknowledge the buyer's actual context",
    body_style: "offer the useful next step without pressure",
    closing_style: "leave room for their process",
    good_phrases: ["No rush from my side.", "I can send a shorter summary."],
    phrases_to_avoid_or_limit: [
      "proceed with the proposal",
      "decision-making process",
      "happy to provide any additional information",
    ],
    rules: [
      "Do not invent attachments, discounts, meetings, or commitments.",
      "Preserve product features and decision timing.",
      "Avoid pressure language.",
    ],
  },
};

export function getStyleCard(scenario: ScenarioClassification): StyleCard {
  if (scenario.confidence < 0.55) {
    return styleCards.general_professional_reply;
  }

  return (
    styleCards[scenario.style_card_id] ?? styleCards.general_professional_reply
  );
}
