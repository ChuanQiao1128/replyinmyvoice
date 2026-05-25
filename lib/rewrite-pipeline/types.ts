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

export type RequiredFact = {
  id: string;
  text: string;
  importance: "critical" | "supporting" | "optional";
  canBeRephrased: boolean;
  evidence?: string;
};

export type InputAnalysis = {
  inputKind:
    | "draft_only"
    | "reply_with_context"
    | "messy_thread"
    | "quote_or_list_heavy";
  scenarioHint:
    | "support_policy"
    | "teacher_parent"
    | "sales_followup"
    | "workplace_update"
    | "cover_letter"
    | "general";
  riskLevel: "low" | "medium" | "high";
  factualDensity: "low" | "medium" | "high";
  structureRisk: "low" | "medium" | "high";
  rewriteFreedom: "minimal" | "moderate" | "high";
  requiresStructurePreservation: boolean;
  requiresPolicyCare: boolean;
  containsForwardedThread: boolean;
  containsListsOrQuotes: boolean;
  wordCount: number;
  paragraphCount: number;
  recommendedInitialStrategy: RewriteStrategy;
  reasons: string[];
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

export type RewriteFailureKind =
  | "fact_loss"
  | "unsupported_fact"
  | "changed_policy_or_condition"
  | "broken_numbered_list"
  | "broken_quote_boundary"
  | "sentence_per_paragraph"
  | "line_split_paraphrase"
  | "support_macro_voice"
  | "naturalness_not_improved"
  | "over_rewritten"
  | "too_generic"
  | "provider_unavailable"
  | "repeated_structure_failure"
  | "policy_intent_drift";

export type RewriteStrategy =
  | "minimal_polish"
  | "facts_first_reconstruct"
  | "full_structure_rewrite"
  | "support_policy_options_rewrite"
  | "quote_list_safe_rewrite"
  | "messy_thread_cleanup"
  | "targeted_sentence_repair"
  | "strong_model_restructure"
  | "quality_failure";

export type RewriteStrategyDecision = {
  strategy: RewriteStrategy;
  reason: string;
  failureKinds: RewriteFailureKind[];
  modelTier: "standard" | "strong";
  maxCandidates: number;
  preserveFacts: boolean;
  preserveStructure: boolean;
  stopReason?:
    | "budget_exceeded"
    | "quality_unachievable"
    | "provider_unavailable";
};

export type RewriteBudget = {
  maxAttempts: number;
  maxCandidatesPerAttempt: number;
  allowStrongModel: boolean;
  maxStrongAttempts: number;
  maxEstimatedUsd: number;
  reason: string;
};

export type RewriteBudgetState = {
  attemptsUsed: number;
  strongAttemptsUsed: number;
  estimatedUsdUsed: number;
};

export type PolicyIntentGateResult = {
  safe: boolean;
  issues: Array<{
    ruleId: string;
    kind: RewriteFailureKind;
    message: string;
  }>;
};

export type SentenceRiskDiagnosis = {
  sentence: string;
  issue_tags: string[];
  repair_instruction: string;
};

export type SentenceRiskReview = {
  sentence_diagnostics: SentenceRiskDiagnosis[];
  overall_notes: string[];
};

export type FactReconstructModelRole =
  | "cheap_structured"
  | "mid_writer"
  | "strong_escalation";

export type RewriteAttemptLedgerEntry = {
  attemptNo: number;
  strategy: RewriteStrategy;
  modelRole: FactReconstructModelRole | "deterministic_fallback";
  modelName: string;
  thinkingMode: "non_thinking" | "thinking_high";
  candidateText: string;
  failureAnalysis: string;
  failureKinds: RewriteFailureKind[];
  factGateResult: string;
  structureGateResult: string;
  policyIntentGateResult: string;
  saplingResult: string;
  nextStrategyDecision: RewriteStrategy;
};

export type FactReconstructConfig = {
  strategyVersion: string;
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
