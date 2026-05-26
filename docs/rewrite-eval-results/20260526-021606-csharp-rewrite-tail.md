# C# Rewrite Eval - tail

Started: 2026-05-26T02:16:06.9813060+00:00
Finished: 2026-05-26T02:27:54.1146470+00:00
Cases evaluated: 21
Successful rewrites: 15/21
Fact pass count: 12/21
Customer-usable pass: 12/21 (output + all must_keep preserved + engine success + no forbidden violation)
Forbidden-claim violations (deterministic screen): 0/21
Naturalness failures recoverable under relaxed gate (rewrite <= 40): 0
Measured rewrites: 15
Average signal drop: 11 pts
Baseline-above-threshold average drop: 40 pts (2 cases)
Rewrites below 50% signal: 15/15
Model calls: 32
Sapling calls: 49
Model: deepseek-v4-pro
Max attempts: 10

| Case | Category | Tone | Usable | Success | Draft | Rewrite | Change | Facts | Forbidden | Error | Missing facts |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
rewrite-draft-017 | property_logistics | warm | yes | yes | 21% | 18% | -3 | yes | 0 |  | 
rewrite-draft-029 | hr_recruiting | warm | no | no | unavailable | unavailable | unavailable | no | 0 | model_timeout | The candidate is Desmond.; The role is Content Operations Manager.; The interview date was May 16.; The company decided to move forward with another candidate.; Specific panel feedback will not be shared at this stage.; The decision was not due to a single interview moment.; The candidate pool was strong and the decision was close.; Desmond's background in workflow tooling and editorial coordination is noted as relevant.; Desmond's details can be kept on file.; Desmond is welcome to reapply when a relevant role is posted.
rewrite-draft-031 | customer_support | warm | no | yes | 43% | 15% | -28 | no | 0 |  | The team is actively investigating.
rewrite-draft-036 | medical_admin | warm | no | no | unavailable | unavailable | unavailable | no | 0 | model_timeout | The patient is Celeste.; The referral was sent to Lakewood Cardiology on May 19.; The portal status is currently "pending."; The referral coordinator expects a response within three to five business days.; A response is expected by May 26 if everything is on track.; The portal will update automatically once cardiology confirms.; If no contact is received by May 27, Celeste should call 555-0148 between 9 a.m. and 4 p.m. and ask for the referral desk.; The agent is not a clinician and cannot advise on symptoms or medical decisions.; Clinical details cannot be shared through this message channel.; Urgent symptoms require calling 911 or going to the emergency room.
rewrite-draft-038 | school_admin | warm | no | no | unavailable | unavailable | unavailable | no | 0 | model_timeout | The parent is Paloma.; The student is Wren.; The program is Summer Science Enrichment.; The registration fee is $65 per student.; Payment is due by June 11.; All 24 spots are filled.; Wren is currently on the waitlist.; If a spot opens before June 11, the school will contact Paloma right away.; If no spot is available by June 11, the registration is released to the next waitlist family.; No payment is collected if Wren does not get a spot.
rewrite-draft-040 | nonprofit_community | warm | yes | yes | 27% | 35% | 8 | yes | 0 |  | 
rewrite-draft-044 | teacher_parent | warm | yes | yes | 32% | 16% | -16 | yes | 0 |  | 
rewrite-draft-048 | hr_recruiting | warm | no | no | unavailable | unavailable | unavailable | no | 0 | model_timeout | The candidate is Wendell.; The role is Senior Analyst.; The panel interview was on May 13.; The hiring team will not be moving forward with an offer.; The specific rationale for the decision is confidential.; The panel noted Wendell's experience with financial modeling.; The panel noted Wendell's presentation in the May 13 session.; No further detailed feedback beyond those two points can be shared.; The recruiter for future Analytics roles is Priya Fernandes.; Priya Fernandes is listed on the careers page as the contact for Analytics openings.; No promises about future roles or timelines can be made.; The hiring contact email is hiring@example.org.
rewrite-draft-049 | school_admin | warm | no | no | unavailable | unavailable | unavailable | no | 0 | model_timeout | The program is the Summer Enrichment Program for the 2026 summer session.; The program runs July 7 through July 25.; The location is Ridgemont East campus, Room 104.; Eligibility is for students currently enrolled in grades 3 through 6 at Ridgemont.; Students entering grade 3 in the fall are not eligible this session.; Enrollment is capped at 24 students.; Students beyond 24 go on a waitlist in application-received order.; Being on last year's interest list does not guarantee a spot.; The program fee is $185 per student.; The application form is Form SEP-2026.; Applications and payment must reach the main office by Friday, June 13 at 4 p.m.; Contact is enrichment@example.com or the main office Tuesday through Thursday 8 a.m. to 3 p.m.
rewrite-draft-055 | sales_followup | warm | yes | yes | 30% | 25% | -5 | yes | 0 |  | 
rewrite-draft-057 | property_logistics | warm | yes | yes | 83% | 31% | -52 | yes | 0 |  | 
rewrite-draft-058 | hr_recruiting | warm | no | no | unavailable | unavailable | unavailable | no | 0 | model_timeout | The candidate is Willard.; The role is Operations Coordinator.; The panel review is still in progress.; Reviews are expected to be complete by June 3.; No decision has been made yet.; The recruiter will reach out after June 3 with an update.
rewrite-draft-061 | customer_support | warm | yes | yes | 37% | 18% | -19 | yes | 0 |  | 
rewrite-draft-068 | hr_recruiting | warm | yes | yes | 27% | 31% | 4 | yes | 0 |  | 
rewrite-draft-069 | nonprofit_community | warm | yes | yes | 30% | 17% | -13 | yes | 0 |  | 
rewrite-draft-075 | sales_followup | warm | yes | yes | 13% | 19% | 6 | yes | 0 |  | 
rewrite-draft-078 | hr_recruiting | warm | yes | yes | 22% | 21% | -1 | yes | 0 |  | 
rewrite-draft-086 | medical_admin | warm | yes | yes | 15% | 12% | -3 | yes | 0 |  | 
rewrite-draft-088 | hr_recruiting | warm | no | yes | 25% | 18% | -7 | no | 0 |  | The hiring team is moving forward with a different applicant.
rewrite-draft-089 | nonprofit_community | warm | yes | yes | 29% | 21% | -8 | yes | 0 |  | 
rewrite-draft-099 | nonprofit_community | warm | no | yes | 33% | 0% | -33 | no | 0 |  | The donation amount is $500.; The donation was made on September 15 to the Clearwater Community Fund.; The report will cover aggregate results across all donors, not a per-donor breakdown.; Questions before the report can be directed to grants@example.org.
