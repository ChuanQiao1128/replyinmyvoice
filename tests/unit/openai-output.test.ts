import { describe, expect, it } from "vitest";

import {
  generateGuaranteedRewriteCandidate,
  generateRewriteCandidate,
  getOpenAiModelForStage,
  getRewriteStrategies,
  normalizeRewriteOutput,
} from "../../lib/openai";
import { isCandidateCompleteEnough } from "../../lib/rewrite-completeness";

describe("normalizeRewriteOutput", () => {
  it("accepts string summaries from the model and normalizes them to arrays", () => {
    const result = normalizeRewriteOutput({
      rewrittenText: "Thanks for letting me know. I can review this tomorrow.",
      changeSummary: "Softened the opening and made the reply more specific.",
      riskNotes: "Review the late-work policy before sending.",
    });

    expect(result.changeSummary).toEqual([
      "Softened the opening and made the reply more specific.",
    ]);
    expect(result.riskNotes).toEqual([
      "Review the late-work policy before sending.",
    ]);
  });

  it("replaces empty risk notes with a review reminder", () => {
    const result = normalizeRewriteOutput({
      rewrittenText: "I can send the numbers by 10am tomorrow.",
      changeSummary: ["Made the timing clear."],
      riskNotes: [""],
    });

    expect(result.riskNotes).toEqual(["Review the reply before sending."]);
  });

  it("fills safe defaults when the model omits metadata but returns rewritten text", () => {
    const result = normalizeRewriteOutput({
      rewrittenText: "I can send the corrected file before Monday at 10am.",
    });

    expect(result.changeSummary).toEqual([
      "Rewrote the draft into a more natural reply.",
    ]);
    expect(result.riskNotes).toEqual(["Review the reply before sending."]);
  });
});

describe("OpenAI model configuration", () => {
  it("supports staged model overrides with legacy fallback", () => {
    const original = {
      OPENAI_MODEL: process.env.OPENAI_MODEL,
      OPENAI_MODEL_PRIMARY: process.env.OPENAI_MODEL_PRIMARY,
      OPENAI_MODEL_REPAIR: process.env.OPENAI_MODEL_REPAIR,
      OPENAI_MODEL_ESCALATION: process.env.OPENAI_MODEL_ESCALATION,
      OPENAI_MODEL_FINAL_STRONG: process.env.OPENAI_MODEL_FINAL_STRONG,
    };

    try {
      process.env.OPENAI_MODEL = "legacy-model";
      process.env.OPENAI_MODEL_PRIMARY = "primary-model";
      process.env.OPENAI_MODEL_REPAIR = "repair-model";
      process.env.OPENAI_MODEL_ESCALATION = "escalation-model";
      process.env.OPENAI_MODEL_FINAL_STRONG = "strong-model";

      expect(getOpenAiModelForStage("primary")).toBe("primary-model");
      expect(getOpenAiModelForStage("repair")).toBe("repair-model");
      expect(getOpenAiModelForStage("escalation")).toBe("escalation-model");
      expect(getOpenAiModelForStage("final_strong")).toBe("strong-model");
    } finally {
      for (const [key, value] of Object.entries(original)) {
        if (value === undefined) {
          delete process.env[key];
        } else {
          process.env[key] = value;
        }
      }
    }
  });
});

describe("thread fallback rewrite pass", () => {
  const threadFallback = getRewriteStrategies().find(
    (strategy) => strategy.id === "thread_reply",
  );

  it("infers a teacher-parent reply from a general draft-only request", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "General reply",
        messageToReplyTo: "",
        roughDraftReply: [
          "Hi Monica,",
          "Jordan is missing the reading response, vocabulary practice, and the short reflection paragraph from Friday.",
          "He should start with the reading response and vocabulary practice because those can be done quickly.",
          "Then he can work on the reflection paragraph.",
          "If he turns everything in by the end of this week, I can still accept it for partial credit.",
          "Best regards,",
          "Ms. Carter",
        ].join("\n\n"),
        audience: "",
        purpose: "",
        whatHappened: "",
        factsToPreserve: "",
        tone: "warm",
        tonePreset: "Warm",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("Hi Monica");
    expect(result.rewrittenText).toContain("Jordan");
    expect(result.rewrittenText).toContain("reading response");
    expect(result.rewrittenText).toContain("vocabulary practice");
    expect(result.rewrittenText).toContain("end of this week");
    expect(result.rewrittenText).toContain("partial credit");
    expect(result.rewrittenText).toContain("Ms. Carter");
  });

  it("infers a support reply from a general billing request", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "General reply",
        messageToReplyTo: "",
        roughDraftReply:
          "Hi Priya, the usage report shows 18 active seats, but the renewal only approved 15 regular seats. The extra NZD $126 appears tied to three temporary contractors who were active during May. The base plan did not change.",
        audience: "",
        purpose: "",
        whatHappened: "",
        factsToPreserve: "",
        tone: "direct",
        tonePreset: "Direct",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("Priya");
    expect(result.rewrittenText).toContain("18 active seats");
    expect(result.rewrittenText).toContain("15 regular seats");
    expect(result.rewrittenText).toContain("NZD $126");
    expect(result.rewrittenText).toContain("base plan");
  });

  it("infers export support from a general request and keeps message facts", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "General reply",
        messageToReplyTo:
          "The CSV export from the dashboard is missing the custom tags column. We need the export for our Monday board packet, and the team cannot reconcile April without that column.",
        roughDraftReply:
          "Thank you for reaching out. We apologize for any inconvenience caused by the missing custom tags column in your CSV export. Our team is currently investigating the matter and will provide an update.",
        audience: "",
        purpose: "",
        whatHappened: "",
        factsToPreserve: "",
        tone: "direct",
        tonePreset: "Direct",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("custom tags column");
    expect(result.rewrittenText).toContain("Monday");
    expect(result.rewrittenText).toContain("April");
  });

  it("uses invoice facts from the request instead of hardcoded sample details", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "Customer support",
        messageToReplyTo: "Why is this invoice higher than last month?",
        roughDraftReply:
          "Thank you for contacting us regarding your invoice. The amount may vary based on service usage.",
        audience: "A customer asking about invoice amount",
        purpose: "Explain without sounding generic",
        whatHappened: "They added three seats on June 2.",
        factsToPreserve:
          "Three seats added June 2. Includes prorated seat charges.",
        tone: "direct",
        tonePreset: "Direct",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("Three seats");
    expect(result.rewrittenText).toContain("June 2");
    expect(result.rewrittenText).not.toContain("two seats");
    expect(result.rewrittenText).not.toContain("May 4");
  });

  it("uses delay timing from the request instead of hardcoded sample timing", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "Work update",
        messageToReplyTo: "Can you send the numbers today?",
        roughDraftReply:
          "Unfortunately, due to unforeseen circumstances, the requested numbers cannot be provided today.",
        audience: "A teammate waiting for numbers",
        purpose: "Explain a short delay",
        whatHappened: "The source file arrived late.",
        factsToPreserve: "Source file arrived late. Send by 4pm Friday.",
        tone: "warm",
        tonePreset: "Warm",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("4pm Friday");
    expect(result.rewrittenText).not.toContain("10am tomorrow");
  });

  it("keeps export support facts in the deterministic support fallback", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "Customer support",
        messageToReplyTo:
          "The April and May CSV exports are missing the custom tags column for the Northeast region, and we need a corrected file before Monday at 10am.",
        roughDraftReply:
          "Thank you for contacting us regarding the missing custom tags column. Our team is investigating and will provide an update.",
        audience: "",
        purpose: "",
        whatHappened: "",
        factsToPreserve: "",
        tone: "direct",
        tonePreset: "Direct",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("custom tags column");
    expect(result.rewrittenText).toContain("April");
    expect(result.rewrittenText).toContain("May");
    expect(result.rewrittenText).toContain("Northeast region");
    expect(result.rewrittenText).toContain("Monday");
    expect(result.rewrittenText).toContain("10am");
  });

  it("answers sales follow-up context instead of repeating generic proposal language", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "Email or message reply",
        messageToReplyTo:
          "We like the reporting feature, but the team is comparing two other vendors and probably will not decide until next month.",
        roughDraftReply:
          "Hello Jordan, I am following up on our previous communication regarding the proposal. Please advise whether you would like to proceed with the proposal as discussed.",
        audience: "",
        purpose: "",
        whatHappened: "",
        factsToPreserve: "",
        tone: "warm",
        tonePreset: "Warm",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("Jordan");
    expect(result.rewrittenText).toContain("reporting feature");
    expect(result.rewrittenText).toContain("next month");
    expect(result.rewrittenText).not.toContain("Please advise");
  });

  it("uses a teacher-parent fallback instead of returning a polished grade memo", async () => {
    expect(threadFallback).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "Email or message reply",
        messageToReplyTo:
          "Hi Ms. Carter, Jordan has a low grade and told me he turned in most assignments. What is missing, and is there still time to make up the work?",
        roughDraftReply: [
          "Hi Monica,",
          "Thank you for reaching out and sharing your concerns about Jordan’s current grade.",
          "At this point, Jordan is missing several assignments from the past two weeks, including the reading response, the vocabulary practice, and the short reflection paragraph from Friday.",
          "He has participated in class discussions, but the missing written work is having a significant impact on his overall grade.",
          "I would recommend that Jordan first focus on the reading response and vocabulary practice, since those can be completed more quickly.",
          "After that, he can work on the short reflection paragraph from Friday.",
          "If he turns these in by the end of this week, I will still accept them for partial credit.",
          "I also encourage him to speak with me after class or during lunch if he needs clarification.",
          "I appreciate your partnership and your willingness to help Jordan take responsibility.",
          "I believe he can get back on track with a clear plan and steady follow-through.",
          "Best regards,",
          "Ms. Carter",
        ].join("\n\n"),
        audience: "",
        purpose: "",
        whatHappened: "",
        factsToPreserve: "",
        tone: "warm",
        tonePreset: "Warm",
      },
      threadFallback!,
    );

    expect(result.rewrittenText).toContain("Hi Monica");
    expect(result.rewrittenText).toContain("Jordan is missing");
    expect(result.rewrittenText).toContain(
      "start with the reading response and vocabulary practice",
    );
    expect(result.rewrittenText).toContain("since those should be the quickest");
    expect(result.rewrittenText).toContain(
      "Then he can work on the short reflection paragraph from Friday",
    );
    expect(result.rewrittenText).toContain("reading response");
    expect(result.rewrittenText).toContain("vocabulary practice");
    expect(result.rewrittenText).toContain("reflection paragraph");
    expect(result.rewrittenText).toContain("end of this week");
    expect(result.rewrittenText).toContain("partial credit");
    expect(result.rewrittenText).toContain("after class or during lunch");
    expect(result.rewrittenText).toContain("partnership");
    expect(result.rewrittenText).toContain("take responsibility");
    expect(result.rewrittenText).toContain("clear plan");
    expect(result.rewrittenText).toContain("steady follow-through");
    expect(result.rewrittenText).toContain("Best regards");
    expect(result.rewrittenText).toContain("Ms. Carter");
    expect(result.rewrittenText).not.toContain("Carter is missing");
    expect(result.rewrittenText).not.toContain("I appreciate you reaching out");
    expect(result.rewrittenText).not.toContain("overall grade");
  });

  it("keeps long teacher draft facts in the deterministic fallback", () => {
    const input = {
      scenario: "General reply",
      messageToReplyTo: "",
      roughDraftReply: [
        "Hi Monica,",
        "Thank you for reaching out and sharing your concerns about Jordan's grade. I understand how stressful this situation can feel, and I appreciate you checking in so we can work together to help him catch up.",
        "Right now, Jordan is missing three assignments from the past two weeks: the reading response, the vocabulary practice, and the short reflection paragraph from last Friday. He continues to participate in class discussions, and I want to recognize that his verbal contributions have been thoughtful. However, the missing written work is beginning to have a significant impact on his overall grade.",
        "I recommend that Jordan begin with the reading response and vocabulary practice, since those should be the quickest to complete. After that, he can work on the short reflection paragraph. If he submits all three assignments by this Friday at 5 p.m., I will still accept them for partial credit.",
        "I also want to clarify that he does not need to redo any work he has already completed. This only applies to the three missing assignments listed above. If he has any questions about the instructions, he is welcome to come see me during lunch on Tuesday or Thursday, and I would be happy to walk him through what is expected.",
        "Thank you again for your support and for helping Jordan take responsibility for catching up. I believe he can get back on track if he completes the missing work this week and builds a more consistent routine moving forward.",
        "Best regards,",
        "Ms. Carter",
      ].join("\n\n"),
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    } as const;

    const result = generateGuaranteedRewriteCandidate(input);

    expect(result.rewrittenText).toContain("Hi Monica");
    expect(result.rewrittenText).toContain("Jordan");
    expect(result.rewrittenText).toContain("three assignments");
    expect(result.rewrittenText).toContain("past two weeks");
    expect(result.rewrittenText).toContain("reading response");
    expect(result.rewrittenText).toContain("vocabulary practice");
    expect(result.rewrittenText).toContain("short reflection paragraph from last Friday");
    expect(result.rewrittenText).toContain("this Friday at 5 p.m.");
    expect(result.rewrittenText).toContain("partial credit");
    expect(result.rewrittenText).toContain("does not need to redo");
    expect(result.rewrittenText).toContain("Tuesday or Thursday");
    expect(result.rewrittenText).toContain("Best regards");
    expect(result.rewrittenText).toContain("Ms. Carter");
    expect(isCandidateCompleteEnough(input, result.rewrittenText)).toBe(true);
  });

  it("builds a facts-first support rewrite from extracted billing facts", async () => {
    const factsFirst = getRewriteStrategies().find(
      (strategy) => strategy.id === "facts_first",
    );
    expect(factsFirst).toBeDefined();

    const result = await generateRewriteCandidate(
      {
        scenario: "Customer support",
        messageToReplyTo: "",
        roughDraftReply: [
          "Hi Priya,",
          "Thanks for explaining the situation clearly.",
          "Based on what you've described, the increase is most likely related to the three contractor accounts that were added during the first week of May.",
          "That would explain why the dashboard is showing 18 active seats instead of the 15 regular seats your team approved.",
          "In plain English, this looks like a prorated seat charge rather than a change to your base plan.",
          "So the extra NZD $126 is likely coming from the three temporary users being active for part of the billing period.",
          "You could explain it to your finance manager like this.",
          "For the next step, please check whether those three contractor accounts are still active.",
          "If you send over the names or email addresses of the three contractors, we can help confirm whether they are still active.",
        ].join("\n\n"),
        audience: "",
        purpose: "",
        whatHappened: "",
        factsToPreserve: "",
        tone: "warm",
        tonePreset: "Warm",
      },
      factsFirst!,
    );

    expect(result.rewrittenText).toContain("Priya");
    expect(result.rewrittenText).toContain("18 active seats");
    expect(result.rewrittenText).toContain("15 regular seats");
    expect(result.rewrittenText).toContain("NZD $126");
    expect(result.rewrittenText).toContain("finance manager");
    expect(result.rewrittenText).toContain("names or email addresses");
    expect(result.rewrittenText).not.toContain("Based on what");
    expect(result.rewrittenText).not.toContain("For the next step");
  });

  it("does not route every customer-support fallback through invoice billing", () => {
    const input = {
      scenario: "Customer support",
      messageToReplyTo:
        "Mina was added to our workspace yesterday, but she still sees the old pilot workspace after logging in with mina@northstar.example. We resent the invite twice and she cannot access the billing report folder.",
      roughDraftReply:
        "Hello, thank you for contacting support. It may be related to the user's previous workspace association.",
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "direct",
      tonePreset: "Direct",
    } as const;
    const result = generateGuaranteedRewriteCandidate(input);

    expect(result.rewrittenText).toContain("Mina");
    expect(result.rewrittenText).toContain("email address");
    expect(result.rewrittenText).toContain("mina@northstar.example");
    expect(result.rewrittenText).toContain("invite was resent twice");
    expect(result.rewrittenText).toContain("billing report folder");
    expect(result.rewrittenText).not.toContain("invoice preview");
    expect(result.rewrittenText).not.toContain("temporary users");
    expect(isCandidateCompleteEnough(input, result.rewrittenText)).toBe(true);
  });

  it("uses a plan-change billing fallback instead of seat billing language", () => {
    const result = generateGuaranteedRewriteCandidate({
      scenario: "Customer support",
      messageToReplyTo:
        "We switched from the Starter plan to the Team plan on May 3 because our manager wanted shared templates. The invoice preview shows the old plan credit and new plan charge separately.",
      roughDraftReply:
        "Thank you for reaching out regarding the invoice preview after your recent plan change from Starter to Team.",
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    });

    expect(result.rewrittenText).toContain("Starter plan");
    expect(result.rewrittenText).toContain("Team plan");
    expect(result.rewrittenText).toContain("May 3");
    expect(result.rewrittenText).toContain("shared templates");
    expect(result.rewrittenText).toContain("old plan credit");
    expect(result.rewrittenText).toContain("new plan charge");
    expect(result.rewrittenText).not.toContain("active seats");
  });

  it("uses a package-delay support fallback instead of a generic quick update", () => {
    const input = {
      scenario: "General reply",
      messageToReplyTo: "",
      roughDraftReply: [
        "Hi Emma,",
        "Thank you for contacting us about the delay with your recent order.",
        "Your order has already left our fulfillment center and is currently with the delivery carrier.",
        "The delay seems to be related to a temporary processing issue at the local distribution facility, rather than a problem with the order itself.",
        "Your package is still in transit and has not been marked as lost or returned.",
        "We expect the delivery carrier to provide a new delivery update within the next one to two business days.",
        "There is no action required from you right now.",
        "If the tracking status does not change within two business days, please reply and we can open a follow-up investigation with the carrier.",
        "Best regards,",
        "Customer Support Team",
      ].join("\n\n"),
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    } as const;

    const result = generateGuaranteedRewriteCandidate(input);

    expect(result.rewrittenText).toContain("Hi Emma");
    expect(result.rewrittenText).toContain("fulfillment center");
    expect(result.rewrittenText).toContain("delivery carrier");
    expect(result.rewrittenText).toContain("temporary processing issue");
    expect(result.rewrittenText).toContain("local distribution facility");
    expect(result.rewrittenText).toContain("still in transit");
    expect(result.rewrittenText).toContain("lost or returned");
    expect(result.rewrittenText).toContain("one to two business days");
    expect(result.rewrittenText).toContain("no action is required");
    expect(result.rewrittenText).toContain("follow-up investigation with the carrier");
    expect(result.rewrittenText).toContain("Customer Support Team");
    expect(result.rewrittenText).not.toContain("Quick update: Hi Emma");
    expect(isCandidateCompleteEnough(input, result.rewrittenText)).toBe(true);
  });

  it("keeps long billing facts without rejecting plain-English wording as a name", () => {
    const input = {
      scenario: "General reply",
      messageToReplyTo:
        "Priya needs to explain why the invoice preview shows 18 active seats instead of 15 regular seats. The preview is NZD $126 higher, the three temporary contractors were supposed to be removed after May 8, and she needs a plain English explanation for her finance manager. The base plan has not changed.",
      roughDraftReply:
        "Hi Priya, thank you for reaching out about the invoice preview. From what you described, the increase is probably a prorated charge from three temporary contractors, not a base plan change. Please check whether those contractor accounts are still active before the invoice is finalized.",
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    } as const;

    const result = generateGuaranteedRewriteCandidate(input);

    expect(result.rewrittenText).toContain("Priya");
    expect(result.rewrittenText).toContain("18 active seats");
    expect(result.rewrittenText).toContain("15 regular seats");
    expect(result.rewrittenText).toContain("NZD $126");
    expect(result.rewrittenText).toContain("finance manager");
    expect(result.rewrittenText).toContain("base plan");
    expect(isCandidateCompleteEnough(input, result.rewrittenText)).toBe(true);
  });

  it("keeps long export facts including region, months, and deadline", () => {
    const input = {
      scenario: "General reply",
      messageToReplyTo:
        "The April and May CSV exports are missing the custom tags column for the Northeast region. We need a corrected file before Monday at 10am, and the original data should remain safe.",
      roughDraftReply:
        "Thank you for contacting us regarding the missing custom tags column in your April and May CSV exports. We are investigating the export behavior and can provide a workaround for the Monday board packet.",
      audience: "",
      purpose: "",
      whatHappened:
        "The operations team imports the CSV into a spreadsheet, checks each campaign against its custom tag, and then sends the reconciled numbers to finance and the board assistant.",
      factsToPreserve: "",
      tone: "direct",
      tonePreset: "Direct",
    } as const;

    const result = generateGuaranteedRewriteCandidate(input);

    expect(result.rewrittenText).toContain("April and May");
    expect(result.rewrittenText).toContain("Northeast region");
    expect(result.rewrittenText).toContain("custom tags column");
    expect(result.rewrittenText).toContain("Monday at 10am");
    expect(isCandidateCompleteEnough(input, result.rewrittenText)).toBe(true);
  });

  it("keeps implementation schedule facts instead of routing a general export mention to CSV support", () => {
    const input = {
      scenario: "General reply",
      messageToReplyTo: "",
      roughDraftReply: [
        "Hi Morgan,",
        "Thank you for sending over the revised onboarding timeline and the notes from yesterday's implementation call.",
        "The overall timeline still looks workable, but the training session originally planned for Tuesday, 4 June will need to move.",
        "The best replacement time on our side is Thursday, 6 June at 10:30 a.m. If that does not work for your team, the backup option is Friday, 7 June after 2 p.m.",
        "Please do not change the go-live date yet. We are still aiming for Monday, 17 June, as long as the user-permission issue is resolved by the end of next week.",
        "The main blocker is that the warehouse supervisors can see the dashboard, but they cannot approve shift changes.",
        "We also noticed that the export file is missing the approved by column, which our finance team needs for the weekly reconciliation report.",
        "We are not adding the SMS reminder feature in this phase, and we are not ready to approve the additional NZD $480 setup fee for that feature.",
        "I do want the team to document it as a possible phase-two item, because our regional managers may ask about it later.",
        "Could you send us an updated implementation note that includes the proposed training time change, the permission issue, the missing approved by column, a note that SMS reminders are not part of this phase, and confirmation that the go-live date is still Monday, 17 June unless the permission issue is not resolved?",
        "Best,",
        "Avery",
      ].join("\n\n"),
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    } as const;

    const result = generateGuaranteedRewriteCandidate(input);

    expect(result.rewrittenText).toContain("Hi Morgan");
    expect(result.rewrittenText).toContain("Tuesday, 4 June");
    expect(result.rewrittenText).toContain("Thursday, 6 June at 10:30 a.m.");
    expect(result.rewrittenText).toContain("Friday, 7 June after 2 p.m.");
    expect(result.rewrittenText).toContain("Monday, 17 June");
    expect(result.rewrittenText).toContain("user-permission issue");
    expect(result.rewrittenText).toContain("warehouse supervisors");
    expect(result.rewrittenText).toContain("cannot approve shift changes");
    expect(result.rewrittenText).toContain("approved by column");
    expect(result.rewrittenText).toContain("weekly reconciliation report");
    expect(result.rewrittenText).toContain("SMS reminders are not part of this phase");
    expect(result.rewrittenText).toContain("NZD $480");
    expect(result.rewrittenText).toContain("phase-two item");
    expect(result.rewrittenText).toContain("regional managers");
    expect(result.rewrittenText).toContain("Avery");
    expect(result.rewrittenText).not.toContain("custom tags column");
    expect(result.rewrittenText).not.toContain("underlying campaign data");
    expect(isCandidateCompleteEnough(input, result.rewrittenText)).toBe(true);
  });

  it("keeps long workshop room-change facts in the deterministic fallback", () => {
    const input = {
      scenario: "Blank / custom",
      messageToReplyTo: "",
      roughDraftReply: [
        "This message is to inform participants that the Saturday workshop location has changed due to maintenance in the library. The session will now take place in Room 204, and the start time remains 6:30pm. The agenda is unchanged and will still include scholarship forms, supporting documents, and the application timeline. Participants who already submitted questions do not need to send them again.",
        "Additional details for the note: families received the original workshop reminder on Tuesday, so this update should focus only on the room change and not repeat the full registration instructions.",
        "Participants may still bring printed scholarship drafts if they want feedback during the session.",
      ].join("\n\n"),
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    } as const;

    const result = generateGuaranteedRewriteCandidate(input);

    expect(result.rewrittenText).toContain("Saturday");
    expect(result.rewrittenText).toContain("Room 204");
    expect(result.rewrittenText).toContain("6:30pm");
    expect(result.rewrittenText).toContain("scholarship forms");
    expect(result.rewrittenText).toContain("supporting documents");
    expect(result.rewrittenText).toContain("application timeline");
    expect(result.rewrittenText).toContain("Tuesday");
    expect(result.rewrittenText).toContain("do not need to send them again");
    expect(result.rewrittenText).toContain("Printed scholarship drafts");
    expect(isCandidateCompleteEnough(input, result.rewrittenText)).toBe(true);
  });

  it("keeps sales renewal facts from message context during general fallback", () => {
    const input = {
      scenario: "General reply",
      messageToReplyTo:
        "Thanks for the renewal proposal. We like the reporting feature and the new team templates, but finance asked us to compare two other vendors before we commit. The earliest we can decide is the first week of June. If you have a shorter summary of the two plan options, send it over and I will include it in our internal thread.",
      roughDraftReply:
        "Hello Jordan, I am following up regarding the renewal proposal. We appreciate your interest in the reporting feature and the new team templates. We understand that your finance team is comparing multiple vendors before making a final decision during the first week of June.",
      audience: "",
      purpose: "",
      whatHappened:
        "Keep the reporting feature, team templates, finance review, two other vendors, and first week of June. This should sound like a real sales follow-up.",
      factsToPreserve:
        "Preserve Jordan's name if it appears in the draft. Do not invent a company.",
      tone: "warm",
      tonePreset: "Warm",
    } as const;

    const result = generateGuaranteedRewriteCandidate(input);

    expect(result.rewrittenText).toContain("Jordan");
    expect(result.rewrittenText).toContain("reporting feature");
    expect(result.rewrittenText).toContain("team templates");
    expect(result.rewrittenText).toContain("two other vendors");
    expect(result.rewrittenText).toContain("first week of June");
    expect(isCandidateCompleteEnough(input, result.rewrittenText)).toBe(true);
  });

  it("keeps support-specialist seniority constraints in cover-letter fallback", () => {
    const input = {
      scenario: "Cover letter",
      messageToReplyTo:
        "Role: Support Specialist for a small SaaS company. The applicant is early in their support career and wants the letter to sound confident without pretending to be senior. They have direct experience answering email and chat, noticing recurring customer issues, summarizing patterns for the product team, and updating help center articles.",
      roughDraftReply:
        "I am excited to submit my application for the Support Specialist role. In my previous position, I answered customer questions through email and chat, summarized recurring issues for our product team, and updated help center articles when we noticed the same question coming up repeatedly. I enjoy making complicated product details easier for customers to understand.",
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    } as const;

    const result = generateGuaranteedRewriteCandidate(input);

    expect(result.rewrittenText).toContain("Support Specialist");
    expect(result.rewrittenText).toContain("email and chat");
    expect(result.rewrittenText).toContain("recurring issues");
    expect(result.rewrittenText).toContain("product team");
    expect(result.rewrittenText).toContain("help center articles");
    expect(result.rewrittenText).toContain("small SaaS");
    expect(result.rewrittenText).toMatch(
      /early in my support career|Support Specialist level|without pretending to be senior|do not make me sound senior/i,
    );
  });
});
