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

/**
 * Homepage demo samples for the hero comparison. Each case pairs a stiff,
 * templated "rough draft" with a natural, fact-preserving rewrite in the
 * sender's own voice — every fact in a rewrite also appears in its draft and
 * context, so the rewrite changes the voice, not the information. The signal
 * values are representative homepage figures. The landing-demo-samples test
 * keeps these aligned with docs/sample-cases.md.
 */
export const homepageSampleCases: HomepageSampleCase[] = [
  {
    label: "Teacher Message",
    icon: "T",
    context:
      "Daniel emails to say he was out sick for Friday's quiz and is anxious about both his grade and the essay deadline.",
    draft:
      "Dear Daniel, thank you very much for reaching out, and I hope you are feeling significantly better. I completely understand your concerns regarding both Friday's quiz and the upcoming essay deadline, and I want to reassure you that there is no need to worry. Please be advised that students who are absent due to illness are entitled to a makeup opportunity. Accordingly, you may complete the makeup quiz this coming Tuesday during the lunch period in Room 204; there is no need to bring anything other than a pen. Furthermore, with respect to the argumentative essay, I have decided to extend the submission deadline from Monday to Wednesday in order to accommodate your recent absence. Should you wish to review any of the material prior to the assessment, please do not hesitate to attend my office hours on Thursday between 3:00 and 4:00 PM, at which time I would be more than happy to provide any additional support you may require. Please do let me know if you have any further questions, and I will respond at my earliest convenience. Wishing you a swift and speedy recovery.",
    rewrite:
      "Hi Daniel — sorry you were out sick, hope you're properly on the mend! Don't stress about the quiz or the essay, we'll sort both out. For the quiz: come find me Tuesday at lunch in Room 204 and you can take the makeup version then — just bring a pen, nothing else. And I've pushed your essay deadline back from Monday to Wednesday, so you get a couple of extra days to do it properly instead of rushing it the second you're back. If you want to go over anything before the quiz, I'm around Thursday from 3 to 4 for office hours — genuinely happy to walk through whatever's still fuzzy, the tricky parts trip up plenty of people. Honestly, you haven't fallen behind, so don't let it hang over you. Focus on feeling better first, and we'll pick up the rest once you're in. If Tuesday lunch doesn't work or anything's unclear, just shoot me a message and we'll find another time. Feel better soon!",
    before: 74,
    after: 10,
    sourceDocument: "docs/sample-cases.md",
  },
  {
    label: "Sales Follow-Up",
    icon: "S",
    context:
      "Jonah is comparing two vendors and wants the final numbers plus whether onboarding can start before June 1.",
    draft:
      "Hi Jonah, I hope this email finds you well. Thank you so much for your continued interest in our platform, and for giving us the opportunity to be considered. I am pleased to provide the final pricing details for your evaluation. The total investment would be $1,800 per month for 25 seats, in addition to a one-time onboarding fee of $650. With regard to your timeline, please be advised that our earliest available kickoff date is May 28. Full setup typically requires approximately 5 business days following receipt of both the signed contract and the necessary data access. Consequently, while we are able to commence the onboarding process prior to June 1, we are unfortunately unable to guarantee full completion by that specific date. We remain fully committed to ensuring a smooth and seamless transition for your team. Please do not hesitate to reach out should you require any further clarification, and I would be more than happy to schedule a call at your earliest convenience. Thank you once again for your time and consideration; we truly look forward to the possibility of working together.",
    rewrite:
      "Hi Jonah — happy to lay it all out so you can compare us properly. Pricing is $1,800 a month for 25 seats, plus a one-time $650 onboarding fee. On timing: the earliest we can kick off is May 28, and setup usually takes about 5 business days once we've got the signed contract and data access sorted. So to answer what you actually asked — yes, we can start before June 1, but I don't want to oversell it: I can't promise everything's fully live by then, and I'd rather be straight with you now than have it bite you later. If it'd help your comparison, I'm glad to jump on a quick call and break down how those 5 days actually play out, or walk you through how a similar-sized team handled the same deadline. No pressure either way — I know you've got another option on the table, and honestly I'd rather you pick what's genuinely right for you than feel pushed. Either way, thanks for considering us. Just tell me what's most useful and I'll get it over to you.",
    before: 69,
    after: 12,
    sourceDocument: "docs/sample-cases.md",
  },
  {
    label: "Workplace Email",
    icon: "W",
    context:
      "You need to tell your manager Dana the redesign is slipping and get her to choose between two options before Friday's client call.",
    draft:
      "Hi Dana, I hope you are doing well. I wanted to take a moment to proactively flag a potential concern regarding the timeline for the website redesign project. As you may already be aware, we have unfortunately encountered some unforeseen delays, due primarily to the late delivery of the design assets. Consequently, it now appears highly unlikely that we will be in a position to meet the original launch date of June 6. In light of this, I have identified two potential paths forward for your consideration. Option A would involve launching on schedule with a reduced scope, deferring the blog and careers pages to a subsequent phase. Alternatively, Option B would involve maintaining the full scope while moving the launch date to June 13. Based on my assessment, I would respectfully recommend Option A, as it enables us to honor our commitment to the client. That said, I would greatly value your input on the matter. Given that we have a client call scheduled for Friday, it would be tremendously helpful if you could kindly share your decision beforehand. Please let me know should you wish to discuss this further.",
    rewrite:
      "Hi Dana — quick heads-up before Friday's client call, because I'd rather you hear this from me now than get surprised on the call. The redesign is slipping: the design assets landed late, and hitting the original June 6 launch is looking unrealistic. I see two ways to play it. Option A: still launch June 6, but trim the scope — ship the core site and push the blog and careers pages to a fast follow a week later. Option B: keep everything and move the launch to June 13. My honest take is Option A. The client cares far more about the date than about every page being live on day one, and we can quietly add the rest right after. But it's your call — you know the account better than I do. Could you let me know which way you want to go before Friday? That way we walk in aligned and I'm not improvising in front of the client. And if it's easier to just talk it through for five minutes today, I'm around whenever you've got a gap.",
    before: 82,
    after: 7,
    sourceDocument: "docs/sample-cases.md",
  },
  {
    label: "Cold Reply",
    icon: "C",
    context:
      "A recruiter, Priya, cold-emails about a Senior PM role at a fintech startup, lists a comp range, and asks if you're open to a chat this week.",
    draft:
      "Dear Priya, thank you so much for reaching out and for thinking of me in connection with this opportunity. I sincerely appreciate you taking the time to consider my background for the Senior Product Manager position at your client's fintech startup. After careful consideration, I would like to express that I am cautiously open to exploring this opportunity further. While I am currently very happy and engaged in my present role, I do believe it is always prudent to remain open to compelling opportunities that align with one's long-term career objectives. With regard to the compensation range of $160,000 to $185,000 that you mentioned, I would say that it is broadly in line with my expectations, although I would naturally welcome the opportunity to discuss the complete package in greater detail. In terms of scheduling, I would be more than happy to make myself available for an introductory conversation at some point this week. Please feel free to suggest a few times that may be convenient for you, and I will do my utmost to accommodate. Once again, thank you for considering me; I very much look forward to hearing from you soon.",
    rewrite:
      "Hi Priya — thanks for reaching out, this one actually caught my eye. I'm happy where I am right now, so I'm not actively looking, but a Senior PM role at a fintech startup is the kind of thing I'd at least want to hear more about before I say no to it. The $160–185k range you mentioned is in the right ballpark for me, so we wouldn't be wasting each other's time on that front. Before we book a call, a couple of quick things that'd help me figure out if it's worth both our time: is the role remote or hybrid, how big is the product team right now, and is this backfilling someone or a brand-new seat? On timing, this week works fine — I'm pretty open Wednesday or Thursday afternoon, so send a couple of slots and I'll grab one. Fair warning: I'm not in any rush to move, so I'll be upfront about whether it's a real fit rather than just taking the meeting for the sake of it. But genuinely, thanks for thinking of me — let's talk.",
    before: 88,
    after: 8,
    sourceDocument: "docs/sample-cases.md",
  },
  {
    label: "AI-Assisted Draft",
    icon: "A",
    context:
      "You drafted a refund reply to a customer, Mara, with an AI assistant; it is accurate but sounds generic and corporate, and you want it to sound like you while keeping the numbers and the refund window exact.",
    draft:
      "Dear Mara, Thank you for reaching out to us regarding your recent cancellation, and please accept our sincere appreciation for your continued patronage. We completely understand your concerns and are more than happy to assist. Please be advised that, in accordance with our refund policy, customers are eligible to receive a prorated refund provided that the request is submitted within fourteen (14) days of the renewal date. As your cancellation occurred merely three days into the new term, you are indeed eligible for a prorated refund of your $89 annual subscription. The refund will be processed to your original payment method, and you may reasonably expect it to appear within five (5) to seven (7) business days. Should you have any further questions or require additional assistance, please do not hesitate to contact us. We value your business immensely and sincerely hope to have the opportunity to serve you again in the future.",
    rewrite:
      "Hi Mara — good news, you're all set for a refund. You cancelled just 3 days into the new term, so you're well inside our 14-day window, which means you get a prorated refund on the $89 annual plan. I'll send it back to the card you originally paid with, and it usually lands within 5 to 7 business days. If it hasn't shown up by then, just reply here and I'll chase it down for you. Sorry it didn't work out this time — and if you ever decide to come back, your old setup will be right where you left it.",
    before: 80,
    after: 11,
    sourceDocument: "docs/sample-cases.md",
  },
];
