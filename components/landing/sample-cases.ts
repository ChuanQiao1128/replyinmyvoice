export type HomepageSampleCase = {
  label: string;
  /** Single-letter monogram shown on the comparison-card tab. */
  icon: string;
  context: string;
  draft: string;
  rewrite: string;
  before: number;
  after: number;
  sourceDocument: "docs/sample-cases.md";
};

export const homepageSampleCases: HomepageSampleCase[] = [
  {
    label: "Teacher Message",
    icon: "T",
    context:
      "Maya asks whether she can still submit a missed reflection after a family issue.",
    draft:
      "Dear Maya, I acknowledge receipt of your email regarding the missed reflection. Late submissions are generally not accepted under the course policy. I will review the circumstances you described and determine whether any exception can be considered. Please be advised that approval is not guaranteed and further information may be required before a decision is made.",
    rewrite:
      "Hi Maya, thanks for letting me know what happened. I can look at this with you tomorrow and check it against the late-work policy before deciding the next step. If there is anything else I should understand about the family issue, send it through before then.",
    before: 81,
    after: 39,
    sourceDocument: "docs/sample-cases.md",
  },
  {
    label: "Sales Follow-Up",
    icon: "S",
    context:
      "Jordan says the team is still comparing vendors and may need another week.",
    draft:
      "Hello Jordan, I am following up regarding the proposal sent last Tuesday. Please advise whether your team has completed its vendor comparison and whether you would like to proceed with the package as discussed. I would appreciate any update you can provide so that we can determine the appropriate next steps.",
    rewrite:
      "Hi Jordan, just checking back on the proposal from Tuesday. If your team is still comparing vendors, no problem. I can send a shorter version with the two options side by side, or answer anything that would help you decide next week.",
    before: 76,
    after: 41,
    sourceDocument: "docs/sample-cases.md",
  },
  {
    label: "Workplace Email",
    icon: "W",
    context:
      "A teammate needs revised numbers for a partner update, but the source file arrived late.",
    draft:
      "Unfortunately, the requested numbers are not available at this time because the source information was delayed. I understand that the partner update is important, and I will provide the revised figures as soon as the underlying file has been checked and the information is ready for circulation.",
    rewrite:
      "The source file came in late, so I need one more check before I send the revised numbers. I know you need them for the partner update. I will get the final version to you by 4pm Friday.",
    before: 73,
    after: 32,
    sourceDocument: "docs/sample-cases.md",
  },
  {
    label: "Client Reply",
    icon: "C",
    context:
      "Priya asks why this month's report totals look different from last month.",
    draft:
      "Dear Priya, we apologize for any inconvenience caused by the discrepancy in the report totals. Our team is currently looking into the matter and will provide an update as soon as possible. We appreciate your patience while we review the relevant information and determine what may have changed.",
    rewrite:
      "Hi Priya, thanks for flagging this. I am checking the report now because this month includes a category that was hidden last month. I will send you a clear line-by-line note today so you can see exactly what changed.",
    before: 79,
    after: 37,
    sourceDocument: "docs/sample-cases.md",
  },
];
