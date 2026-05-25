import { readFileSync } from "node:fs";
import path from "node:path";

import { afterEach, describe, expect, it, vi } from "vitest";

import {
  __evalScenarioTestUtils,
  parseLearningBaselineCorpus,
} from "../../scripts/eval-scenarios";

const fixturePath = path.join(
  process.cwd(),
  "tests",
  "fixtures",
  "learning-corpus-mini.md",
);

describe("learning baseline corpus parser", () => {
  const markdown = readFileSync(fixturePath, "utf8");

  it("parses learning baseline markdown tables into eval cases", () => {
    const cases = parseLearningBaselineCorpus(markdown);

    expect(cases).toHaveLength(5);
    expect(cases[0]).toMatchObject({
      id: "lbc-blank-001",
      scenario: "Blank / custom",
      tonePreset: "Warm",
      messageToReplyTo: "",
      roughDraftReply: "Please note that the room changed to B12 for Thursday.",
      expectedFacts: ["Room changed to B12", "day is Thursday."],
    });
    expect(cases[3]).toMatchObject({
      id: "lbc-email-001",
      scenario: "Email or message reply",
      tonePreset: "Warm",
      expectedFacts: ["Lina", "make-up quiz Thursday after school."],
    });
  });

  it("trims table cell values and fact fragments", () => {
    const cases = parseLearningBaselineCorpus(markdown);

    expect(cases[1]).toMatchObject({
      id: "lbc-blank-002",
      roughDraftReply:
        "Need a note that the survey link was fixed at 2:15 PM and replies are due Friday.",
      expectedFacts: ["Survey link fixed at 2:15 PM", "replies due Friday."],
    });
  });

  it("rejects unknown tones with the row ID", () => {
    const invalid = markdown.replace(
      "| lbc-email-002 | hand_crafted_edge_case | Email | Direct |",
      "| lbc-email-002 | hand_crafted_edge_case | Email | Friendly |",
    );

    expect(() => parseLearningBaselineCorpus(invalid)).toThrow(
      /lbc-email-002.*tone.*Friendly/i,
    );
  });

  it("rejects rows with missing columns and cites the row ID", () => {
    const invalid = markdown.replace(
      "| lbc-blank-003 | hand_crafted_edge_case | Blank | Warm | Thank Aria for covering pickup yesterday and offer to take Friday. | Thank Aria; pickup was yesterday; offer Friday. | 40-60% |",
      "| lbc-blank-003 | hand_crafted_edge_case | Blank | Warm | Thank Aria for covering pickup yesterday and offer to take Friday. | Thank Aria; pickup was yesterday; offer Friday. |",
    );

    expect(() => parseLearningBaselineCorpus(invalid)).toThrow(
      /lbc-blank-003.*7 columns/i,
    );
  });
});

describe("scenario eval fact matcher", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    delete process.env.EVAL_SEMANTIC_JUDGE_RETRY_DELAY_MS;
  });

  describe("semantic anchor matching", () => {
    const matchingPairs = [
      {
        fact: "Late-work policy allows up to 70% credit within 5 school days.",
        output:
          "Our late-work policy gives up to 70% credit for work turned in within 5 school days",
      },
      {
        fact: "Annual renewal charged $288 on August 1.",
        output: "Your Annual plan renewed on August 1 for $288.",
      },
      {
        fact:
          "Camden Foods requested month-to-month pilot capped at $2,000 with SSO.",
        output:
          "Hi Camden Foods, Thanks for the note about the month-to-month pilot capped at $2,000 with SSO.",
      },
      {
        fact:
          "Private meeting options May 13 at 2:00 PM or May 14 at 10:30 AM for 25 minutes.",
        output:
          "a 25-minute meeting on either May 13 at 2:00 PM or May 14 at 10:30 AM",
      },
      {
        fact: "Billing manager escalation possible, but no refund promise.",
        output:
          "Escalate to the billing manager for a possible one-time exception. I can't promise a refund",
      },
      {
        fact:
          "14-day grace review requires tax-exempt letter and board officer confirmation.",
        output:
          "A 14-day grace review is available only when a tax-exempt letter and board officer confirmation are both uploaded.",
      },
      {
        fact: "Do not change client hold without confirmation.",
        output:
          "We'll keep the client calendar hold as-is until you confirm a preference.",
      },
    ];

    const missingPairs = [
      {
        fact: "Annual renewal charged $288 on August 1.",
        output: "Your Annual plan renewed in August for $289.",
      },
      {
        fact: "Annual renewal charged $288 on August 1.",
        output: "Your Annual plan renewed for $288.",
      },
      {
        fact: "Order #R8142.",
        output: "We received your order and are looking into it.",
      },
      {
        fact: "Corrected project due April 19 at 3:30 PM.",
        output: "Please submit the corrected project by April 12 at 3:30 PM.",
      },
      {
        fact: "Alex Moreno.",
        output: "Hi there, I checked the account for you.",
      },
      {
        fact: "$64 refund posted June 7 for one jacket.",
        output: "a refund was posted for one jacket.",
      },
    ];

    it.each(matchingPairs)("matches preserved paraphrase: $fact", ({ fact, output }) => {
      expect(__evalScenarioTestUtils.factCheck(output, [fact])).toEqual({
        passed: true,
        missing: [],
      });
    });

    it.each(missingPairs)("rejects hard-data loss: $fact", ({ fact, output }) => {
      expect(__evalScenarioTestUtils.factCheck(output, [fact])).toEqual({
        passed: false,
        missing: [fact],
      });
    });
  });

  it("accepts natural equivalents for late-policy upload facts", () => {
    const text = [
      "You can turn in the assignment by Monday, March 18 at 8:00 AM with no late penalty.",
      "After that, the standard late policy will apply.",
      "If you still have trouble uploading the file, just send me an email.",
    ].join(" ");

    const result = __evalScenarioTestUtils.factCheck(text, [
      "No late penalty if submitted by then.",
      "Standard late policy applies after that.",
      "Student should email if the file still will not upload.",
    ]);

    expect(result).toEqual({ passed: true, missing: [] });
  });

  it("accepts natural equivalents for no-change subscription facts", () => {
    const text = [
      "I will not cancel or downgrade the account until you confirm which option you want.",
      "Please let me know whether you want cancellation at renewal or the downgrade to Solo.",
    ].join(" ");

    const result = __evalScenarioTestUtils.factCheck(text, [
      "Do not cancel or downgrade without confirmation.",
      "Ask whether they want cancellation at renewal or downgrade.",
    ]);

    expect(result).toEqual({ passed: true, missing: [] });
  });

  it("accepts natural equivalents for permission slip and dashboard update facts", () => {
    const result = __evalScenarioTestUtils.factCheck(
      [
        "Maya's signed permission slip and $12 payment are still missing.",
        "Please send a new signed permission slip by April 2, or I can send one home.",
        "The core dashboard is good to go. The Q2 forecast tab isn't finished yet because we need Finance to confirm 7 accounts.",
        "Some duplicate renewal rows set us back. If they confirm by 9:30 AM on May 7, I can have the tab ready by 11:00 AM.",
      ].join(" "),
      [
        "Maya needs a signed permission slip by April 2.",
        "Parent can send a new form or ask for one to be sent home.",
        "Q2 forecast tab depends on Finance confirming 7 accounts.",
        "Need Finance confirmation by 9:30 AM May 7 to send by 11:00 AM.",
        "Duplicate renewal rows caused the delay.",
      ],
    );

    expect(result).toEqual({ passed: true, missing: [] });
  });

  it("accepts natural equivalents for pricing and meeting-option facts", () => {
    const result = __evalScenarioTestUtils.factCheck(
      [
        "$1,800/month for 25 seats, with a one-time $650 onboarding fee.",
        "Liam got 72 percent on the March 20 reading quiz, so he did not fail it.",
        "I can meet Tuesday, March 26 at 4:15 PM or Friday, March 29 at 8:05 AM.",
        "Which time works for you?",
      ].join(" "),
      [
        "Price is $1,800 per month for 25 seats plus $650 onboarding.",
        "Quiz score was 72 percent, not a failure.",
        "Offer Tuesday, March 26 at 4:15 PM or Friday, March 29 at 8:05 AM.",
        "Ask which time works.",
      ],
    );

    expect(result).toEqual({ passed: true, missing: [] });
  });

  it("accepts natural equivalents for express replacement wording", () => {
    const result = __evalScenarioTestUtils.factCheck(
      "If you send those photos by 2:00 PM on April 12, we can ship two replacement mugs express at no extra cost.",
      [
        "Express replacement for 2 mugs can be sent at no extra cost if photos arrive by 2:00 PM April 12.",
      ],
    );

    expect(result).toEqual({ passed: true, missing: [] });
  });

  it("accepts strict transcript-charge paraphrases from rimv-email-048", () => {
    const text = [
      "At North Ridge College, financial aid cannot be used for library replacement charges.",
      "North Ridge College applies an official transcript hold when a balance goes over $100.",
      "Your official transcript can still be sent by the May 20 deadline.",
      "For the official transcript, you can pay the $145 or have the charge corrected through an approved process.",
    ].join(" ");

    const facts = [
      "North Ridge College financial aid cannot cover library replacement charges.",
      "North Ridge College official transcript hold applies to balances over $100.",
      "Official transcript requested by May 20.",
      "Official transcript requires payment or approved correction for $145.",
    ];
    const normalizedText = __evalScenarioTestUtils.normalize(text);

    for (const fact of facts) {
      expect(
        __evalScenarioTestUtils.includesFact(normalizedText, fact),
      ).toBe(true);
    }
    expect(__evalScenarioTestUtils.factCheck(text, facts)).toEqual({
      passed: true,
      missing: [],
    });
  });

  it("still rejects real transcript-charge fact loss", () => {
    const facts = [
      "North Ridge College financial aid cannot cover library replacement charges.",
      "Official transcript requested by May 20.",
      "Official transcript requires payment or approved correction for $145.",
    ];

    expect(
      __evalScenarioTestUtils.factCheck(
        [
          "At North Ridge College, financial aid cannot be used for library replacement charges.",
          "Your official transcript can still be sent by the May 20 deadline.",
          "For the official transcript, you can pay the $155 or have the charge corrected through an approved process.",
        ].join(" "),
        facts,
      ),
    ).toMatchObject({
      passed: false,
      missing: ["Official transcript requires payment or approved correction for $145."],
    });

    expect(
      __evalScenarioTestUtils.factCheck(
        [
          "Financial aid cannot be used for library replacement charges.",
          "Your official transcript can still be sent by the May 20 deadline.",
          "For the official transcript, you can pay the $145 or have the charge corrected through an approved process.",
        ].join(" "),
        facts,
      ),
    ).toMatchObject({
      passed: false,
      missing: [
        "North Ridge College financial aid cannot cover library replacement charges.",
      ],
    });

    expect(
      __evalScenarioTestUtils.factCheck(
        [
          "At North Ridge College, financial aid cannot be used for library replacement charges.",
          "Your official transcript can still be sent once the balance is cleared.",
          "For the official transcript, you can pay the $145 or have the charge corrected through an approved process.",
        ].join(" "),
        facts,
      ),
    ).toMatchObject({
      passed: false,
      missing: ["Official transcript requested by May 20."],
    });
  });

  it("drops semantic-judge misses when deterministic anchors prove the fact is present", () => {
    const text = [
      "I checked the billing ledger and your cancellation timestamp on July 13.",
      "Your Team Notes account was closed on July 12.",
    ].join(" ");
    const semanticResult = {
      facts: {
        passed: false,
        missing: ["The check date is July 13."],
      },
      forbiddenViolations: [],
      quality: {
        draftScore: null,
        rewriteScore: null,
        delta: null,
        notes: [],
      },
    };

    expect(
      __evalScenarioTestUtils.mergeSemanticJudgeWithDeterministicFactCheck(
        text,
        ["The check date is July 13."],
        semanticResult,
      ),
    ).toEqual({
      facts: { passed: true, missing: [] },
      forbiddenViolations: [],
      quality: {
        draftScore: null,
        rewriteScore: null,
        delta: null,
        notes: [],
      },
    });
  });

  it("retries transient semantic judge provider failures before failing a case", async () => {
    process.env.OPENAI_API_KEY = "test-key";
    process.env.EVAL_SEMANTIC_JUDGE_RETRY_DELAY_MS = "0";

    const fetchMock = vi
      .fn()
      .mockRejectedValueOnce(new Error("connect ETIMEDOUT"))
      .mockResolvedValueOnce(
        Response.json({
          choices: [
            {
              message: {
                content: JSON.stringify({
                  mustKeep: [{ fact: "Order R4821.", verdict: "PASS" }],
                  mustNotClaim: [],
                  quality: {
                    draftScore: 7,
                    rewriteScore: 8,
                    notes: ["Clearer without changing the order ID."],
                  },
                }),
              },
            },
          ],
        }),
      );
    vi.stubGlobal("fetch", fetchMock);

    const result = await __evalScenarioTestUtils.semanticFactJudge(
      "Order R4821 needs the address by Friday.",
      "Please send the address for order R4821 by Friday.",
      ["Order R4821."],
      [],
      "Keep order ID and deadline clear.",
    );

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(result.facts).toEqual({ passed: true, missing: [] });
    expect(result.quality.delta).toBe(1);
  });

  it("marks material quality regression separately from hard fact gates", () => {
    expect(__evalScenarioTestUtils.isQualityRegression(7, 5)).toBe(true);
    expect(__evalScenarioTestUtils.isQualityRegression(8, 5)).toBe(true);
    expect(__evalScenarioTestUtils.isQualityRegression(7, 6)).toBe(false);
    expect(__evalScenarioTestUtils.isQualityRegression(null, 5)).toBe(true);
    expect(__evalScenarioTestUtils.isQualityRegression(7, null)).toBe(false);
  });

  it("prevents customer-usable pass when quality regresses materially", () => {
    expect(
      __evalScenarioTestUtils.customerUsablePassFromGates({
        hasRewrittenText: true,
        factsPassed: true,
        unsupportedFactsCount: 0,
        forbiddenViolationsCount: 0,
        qualityFailure: false,
        signalNotWorse: true,
        qualityRegression: true,
      }),
    ).toBe(false);
  });

  it("flags saved smoke case 006 invented scheduling options in eval reporting", () => {
    const input = {
      scenario: "General reply",
      messageToReplyTo: "",
      roughDraftReply: [
        "Hi Ren, I need to move our Thursday, May 16 appointment from 11 a.m. because the room is",
        "unavailable. I can offer Thursday at 2:30 p.m. or Friday at 9 a.m. Please choose one by",
        "Wednesday at noon. I cannot hold both times after noon, but I will confirm the selected",
        "slot as soon as you reply.",
      ].join(" "),
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    } as const;

    const unsupported = __evalScenarioTestUtils.detectEvalUnsupportedFacts(
      input,
      [
        "Ren, the room isn't available for our original time on May 16, so we need to reschedule.",
        "I can hold either 11 a.m. or 2:30 p.m. on Wednesday, but not both after noon-once you choose one, the other will be released.",
        "If neither works, let me know if Thursday at 9 a.m. or Friday works, and we'll confirm a new time.",
        "Please reply by Wednesday at noon so I can finalize the booking.",
      ].join(" "),
    );

    expect(unsupported.map((fact) => fact.normalizedText)).toEqual(
      expect.arrayContaining([
        "wednesday at 11:00 am",
        "wednesday at 2:30 pm",
        "thursday at 9:00 am",
      ]),
    );
  });

  it("flags saved smoke case 009 invented same-day photo deadline in eval reporting", () => {
    const input = {
      scenario: "General reply",
      messageToReplyTo: "",
      roughDraftReply: [
        "Hi Cam, I am following up about the leak under the kitchen sink in Unit 4B.",
        "The plumber confirmed yesterday that the shutoff valve needs to be replaced before the cabinet base can be dried out.",
        "The earliest access window the vendor gave us is Tuesday, May 28 between 9 a.m. and noon.",
        "Please confirm whether someone can let the plumber in during that window or whether we should use the lockbox code already on file.",
        "Please also send one photo of the cabinet floor today if the water has spread past the towel line.",
        "I know this has been inconvenient, but I cannot approve a rent credit from this maintenance thread.",
        "The maintenance file needs the access confirmation first, then I can send the vendor confirmation number.",
      ].join(" "),
      audience: "",
      purpose: "",
      whatHappened: "",
      factsToPreserve: "",
      tone: "warm",
      tonePreset: "Warm",
    } as const;

    const unsupported = __evalScenarioTestUtils.detectEvalUnsupportedFacts(
      input,
      [
        "Cam,",
        "Quick update on the leak under the kitchen sink in Unit 4B. We're ready to schedule the plumber for Tuesday, May 28 between 9 a.m. and noon, but I need your access confirmation before I can send the vendor confirmation number.",
        "Separately, if water has spread past the towel line, please send one photo of the cabinet floor today by 9 a.m. so we can see how things look.",
        "Thanks.",
      ].join(" "),
    );

    expect(unsupported.map((fact) => fact.normalizedText)).toContain(
      "today by 9:00 am",
    );
  });
});
