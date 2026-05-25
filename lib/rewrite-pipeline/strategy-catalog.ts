import type { RewriteStrategy } from "./types";

export type StrategyCatalogEntry = {
  strategy: RewriteStrategy;
  description: string;
  generationGuidance: string[];
};

export const strategyCatalog: Record<RewriteStrategy, StrategyCatalogEntry> = {
  minimal_polish: {
    strategy: "minimal_polish",
    description: "Lightly edit short, low-risk drafts without changing structure.",
    generationGuidance: [
      "Keep the original structure mostly intact.",
      "Tighten stiff phrases without adding new facts.",
    ],
  },
  facts_first_reconstruct: {
    strategy: "facts_first_reconstruct",
    description: "Rebuild from extracted facts instead of paraphrasing the draft.",
    generationGuidance: [
      "Use the facts as the source of truth.",
      "Avoid line-by-line paraphrasing.",
      "Group related facts into natural paragraphs.",
    ],
  },
  full_structure_rewrite: {
    strategy: "full_structure_rewrite",
    description: "Repair broken paragraph flow by rebuilding the whole reply structure.",
    generationGuidance: [
      "Do not place every sentence in its own paragraph.",
      "Use paragraphs with clear jobs: context, facts, next step, closing.",
      "Avoid detached numbered-list markers.",
    ],
  },
  support_policy_options_rewrite: {
    strategy: "support_policy_options_rewrite",
    description: "Rewrite support and policy replies with careful options and constraints.",
    generationGuidance: [
      "Separate what is known, what is conditional, and what the user should do next.",
      "Preserve eligibility, availability, confirmation, and no-change constraints.",
      "Do not promise refunds, transfers, cancellations, or account changes unless stated.",
    ],
  },
  quote_list_safe_rewrite: {
    strategy: "quote_list_safe_rewrite",
    description: "Preserve list and quote boundaries while rewriting surrounding prose.",
    generationGuidance: [
      "Keep list markers attached to their list items.",
      "Do not break quoted summaries across unrelated paragraphs.",
      "Use paragraphs before or after lists instead of splitting every line.",
    ],
  },
  messy_thread_cleanup: {
    strategy: "messy_thread_cleanup",
    description: "Extract a clean reply from forwarded or messy email-thread material.",
    generationGuidance: [
      "Do not include forwarded headers.",
      "Write only the sender's reply.",
      "Preserve the relevant facts from the thread.",
    ],
  },
  targeted_sentence_repair: {
    strategy: "targeted_sentence_repair",
    description: "Repair weak local sentences after the main reply is otherwise safe.",
    generationGuidance: [
      "Change only weak sentences.",
      "Keep facts, paragraph order, and structure stable.",
    ],
  },
  strong_model_restructure: {
    strategy: "strong_model_restructure",
    description: "Use a stronger model once when local repair cannot satisfy gates.",
    generationGuidance: [
      "Rewrite from facts again.",
      "Keep the structure natural and send-ready.",
      "Do not add new facts.",
    ],
  },
  quality_failure: {
    strategy: "quality_failure",
    description: "Stop without returning a successful rewrite.",
    generationGuidance: [],
  },
};

export function strategyGuidance(strategy: RewriteStrategy) {
  return strategyCatalog[strategy].generationGuidance;
}
