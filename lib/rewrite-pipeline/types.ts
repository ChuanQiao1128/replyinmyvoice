export type ExtractedFacts = {
  recipient_name: string;
  sender_name_or_role: string;
  people_mentioned: string[];
  main_purpose: string;
  key_facts: string[];
  required_actions: string[];
  deadlines: string[];
  dates_times: string[];
  positive_notes: string[];
  concerns: string[];
  policies_or_conditions: string[];
  available_support: string[];
  clarifications: string[];
  facts_that_must_not_change: string[];
  sensitive_points: string[];
  original_tone: string;
};

export type ScenarioClassification = {
  domain:
    | "education"
    | "workplace"
    | "customer_support"
    | "sales"
    | "personal"
    | "general";
  intent:
    | "explain"
    | "remind"
    | "decline"
    | "apologize"
    | "follow_up"
    | "request"
    | "summarize"
    | "general_reply";
  format: "email" | "message" | "note";
  relationship: string;
  risk: "low" | "medium" | "high";
  confidence: number;
  style_card_id: string;
  needs_action_steps: boolean;
  needs_empathy: boolean;
  needs_deadline_preservation: boolean;
  notes: string[];
};

export type StyleCard = {
  style_card_id: string;
  voice: string;
  paragraph_style: string;
  sentence_style: string;
  opening_style: string;
  body_style: string;
  closing_style: string;
  good_phrases: string[];
  phrases_to_avoid_or_limit: string[];
  rules: string[];
};

export type CandidateKey =
  | "candidate_a_concise"
  | "candidate_b_warm"
  | "candidate_c_natural";

export type CandidateSet = Record<CandidateKey, string>;

export type CandidateScore = {
  factual_accuracy: number;
  naturalness: number;
  tone_appropriateness: number;
  concision: number;
  low_template_feel: number;
  clarity_of_action_steps: number;
  total: number;
  issues: string[];
};

export type ReviewResult = {
  scores: Record<CandidateKey, CandidateScore>;
  best_candidate_key: CandidateKey;
  required_edits: string[];
  risk_notes: string[];
};

export type LlmFactCheckResult = {
  safe_to_send: boolean;
  has_added_new_facts: boolean;
  has_removed_important_facts: boolean;
  has_changed_names: boolean;
  has_changed_dates_or_deadlines: boolean;
  has_changed_conditions_or_policies: boolean;
  tone_problem: boolean;
  issues: string[];
  required_repairs: string[];
};

export type FactReconstructModelRole =
  | "cheap_structured"
  | "mid_writer"
  | "strong_escalation";

export type FactReconstructConfig = {
  strategyVersion: "fact_reconstruct";
  naturalnessThreshold: number;
  maxEscalations: number;
  models: Record<FactReconstructModelRole, string>;
  pricing: Record<
    FactReconstructModelRole,
    {
      inputPer1M: number;
      outputPer1M: number;
    }
  >;
};
