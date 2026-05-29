using FluentAssertions;
using ReplyInMyVoice.Domain.RewriteEngine;
using ReplyInMyVoice.Eval;

namespace ReplyInMyVoice.Tests;

public class StageOneEnToZhSafePilotTests
{
    [Fact]
    public async Task Post_check_marks_digit_and_verbatim_facts_survived()
    {
        var factLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_amount", "$1,250.00", RewriteFactCategory.Amount) with
            {
                PreserveMode = RewriteFactPreserveMode.Normalized,
                Normalized = "USD:1250",
            },
            Fact("fact_name", "Mark", RewriteFactCategory.Person),
            Fact("fact_id", "ORD-29447", RewriteFactCategory.Identifier),
            Fact("fact_date", "June 7", RewriteFactCategory.DateOrDeadline),
        });
        var claimLedger = new RewriteClaimLedger(Array.Empty<RewriteClaim>());

        var report = await StageOneZhPostChecker.CreateReportAsync(
            "case-001",
            "Hi Mark, order ORD-29447 is $1,250.00 and due June 7.",
            "你好 Mark，订单 ORD-29447 的金额是 1250 美元。",
            factLedger,
            claimLedger,
            new StaticZhClaimSurvivalJudge(),
            CancellationToken.None);

        report.FactsSurvived.Select(f => f.Id).Should().Equal("fact_amount", "fact_name", "fact_id");
        report.FactsDrifted.Select(f => f.Id).Should().Equal("fact_date");
        report.FactSurvivalPct.Should().Be(75.0);
    }

    [Fact]
    public async Task Post_check_uses_batched_claim_judge_verdicts()
    {
        var claims = new[]
        {
            Claim("C001", "I can send a replacement Monday.", RewriteClaimModality.Offer),
            Claim("C002", "I cannot refund the full June charge.", RewriteClaimModality.Prohibition),
            Claim("C003", "The invoice may change after review.", RewriteClaimModality.Uncertainty),
        };

        var report = await StageOneZhPostChecker.CreateReportAsync(
            "case-014",
            "source",
            "translated",
            new RewriteFactLedger(Array.Empty<RewriteFact>()),
            new RewriteClaimLedger(claims),
            new StaticZhClaimSurvivalJudge(
                new ZhClaimSurvivalVerdict("C001", ZhClaimSurvivalStatus.Yes, "offer preserved"),
                new ZhClaimSurvivalVerdict("C002", ZhClaimSurvivalStatus.Partial, "amount present but prohibition weakened"),
                new ZhClaimSurvivalVerdict("C003", ZhClaimSurvivalStatus.No, "uncertainty missing")),
            CancellationToken.None);

        report.ClaimsSurvived.Select(c => c.Id).Should().Equal("C001");
        report.ClaimsDrifted.Select(c => c.Id).Should().Equal("C002", "C003");
        report.ClaimSurvivalPct.Should().BeApproximately(33.333, 0.001);
        report.ClaimVerdicts["C002"].Reason.Should().Contain("prohibition weakened");
    }

    [Fact]
    public async Task Post_check_marks_empty_claim_ledger_on_nonempty_source_as_warning()
    {
        var report = await StageOneZhPostChecker.CreateReportAsync(
            "case-041",
            "This source should have claims.",
            "这段译文应该被检查。",
            new RewriteFactLedger(Array.Empty<RewriteFact>()),
            new RewriteClaimLedger(Array.Empty<RewriteClaim>()),
            new StaticZhClaimSurvivalJudge(),
            CancellationToken.None);

        report.ClaimSurvivalPct.Should().Be(0);
        report.Warnings.Should().Contain("claim_ledger_empty");
    }

    [Fact]
    public void Claim_verdict_parser_treats_only_yes_as_survived()
    {
        var claims = new[]
        {
            Claim("C001", "A ships Monday.", RewriteClaimModality.Certainty),
            Claim("C002", "B cannot be changed.", RewriteClaimModality.Prohibition),
            Claim("C003", "C may arrive later.", RewriteClaimModality.Uncertainty),
        };
        const string json = """
            {"claims":[
              {"id":"C001","status":"yes","reason":"all tuple fields present"},
              {"id":"C002","status":"partial","reason":"polarity is unclear"},
              {"id":"C999","status":"yes","reason":"unknown ids are ignored"}
            ]}
            """;

        var parsed = ZhClaimSurvivalVerdictParser.Parse(json, claims);

        parsed["C001"].Status.Should().Be(ZhClaimSurvivalStatus.Yes);
        parsed["C002"].Status.Should().Be(ZhClaimSurvivalStatus.Partial);
        parsed["C003"].Status.Should().Be(ZhClaimSurvivalStatus.No);
        parsed["C003"].Reason.Should().Be("judge_missing");
        parsed.Should().NotContainKey("C999");
    }

    [Fact]
    public void Model_selection_uses_validated_claim_model_by_default()
    {
        StageOneModelSelection.ResolveClaimModel(null).Should().Be("deepseek-chat");
        StageOneModelSelection.ResolveClaimModel("  custom-model  ").Should().Be("custom-model");
    }

    [Fact]
    public void Hard_fact_checker_marks_chinese_date_equivalent_present()
    {
        var fact = Fact("fact_date", "March 31, 2026", RewriteFactCategory.DateOrDeadline) with
        {
            PreserveMode = RewriteFactPreserveMode.Normalized,
            Normalized = "2026-03-31",
        };

        var item = StageOneHardFactChecker.Check(fact, "续订截止日期为2026年3月31日。");

        item.Status.Should().Be(ZhFactCheckStatus.Present);
        item.MatchKind.Should().Be(ZhFactMatchKind.Normalized);
        item.ZhEvidence.Should().Contain("2026年3月31日");
    }

    [Fact]
    public void Hard_fact_checker_marks_date_missing_when_day_is_dropped()
    {
        var fact = Fact("fact_date", "March 31, 2026", RewriteFactCategory.DateOrDeadline) with
        {
            PreserveMode = RewriteFactPreserveMode.Normalized,
            Normalized = "2026-03-31",
        };

        var item = StageOneHardFactChecker.Check(fact, "续订截止日期为2026年3月。");

        item.Status.Should().Be(ZhFactCheckStatus.Missing);
        item.MatchKind.Should().Be(ZhFactMatchKind.None);
        item.Issue.Should().Contain("2026-03-31");
    }

    [Fact]
    public void Hard_fact_checker_marks_money_equivalent_present_and_wrong_amount_changed()
    {
        var fact = Fact("fact_money", "$12,500", RewriteFactCategory.Amount) with
        {
            PreserveMode = RewriteFactPreserveMode.Normalized,
            Normalized = "USD:12500",
        };

        StageOneHardFactChecker.Check(fact, "年费为12,500美元。").Status.Should().Be(ZhFactCheckStatus.Present);
        StageOneHardFactChecker.Check(fact, "年费为12,000美元。").Status.Should().Be(ZhFactCheckStatus.Changed);
    }

    [Fact]
    public void Hard_fact_checker_handles_percent_and_duration_equivalents()
    {
        var percent = Fact("fact_percent", "99.9%", RewriteFactCategory.Count) with
        {
            PreserveMode = RewriteFactPreserveMode.Normalized,
            Normalized = "99.9%",
        };
        var duration = Fact("fact_duration", "30 days", RewriteFactCategory.Count) with
        {
            PreserveMode = RewriteFactPreserveMode.Normalized,
            Normalized = "30:day",
        };

        StageOneHardFactChecker.Check(percent, "SLA 目标为99.9%。").MatchKind.Should().Be(ZhFactMatchKind.Normalized);
        StageOneHardFactChecker.Check(duration, "客户可在30天内申请退款。").MatchKind.Should().Be(ZhFactMatchKind.Normalized);
    }

    [Fact]
    public void Hard_fact_checker_handles_weekday_equivalents()
    {
        var tuesday = Fact("fact_tuesday", "Tuesday", RewriteFactCategory.DateOrDeadline) with
        {
            PreserveMode = RewriteFactPreserveMode.Normalized,
            Normalized = "weekday:tuesday",
        };
        var friday = Fact("fact_friday", "Friday", RewriteFactCategory.DateOrDeadline) with
        {
            PreserveMode = RewriteFactPreserveMode.Normalized,
            Normalized = "weekday:friday",
        };

        StageOneHardFactChecker.Check(tuesday, "我预计在5月21日星期二之前发送下一次更新。").MatchKind
            .Should().Be(ZhFactMatchKind.Normalized);
        StageOneHardFactChecker.Check(friday, "请在周五下午5点前回答是或否。").MatchKind
            .Should().Be(ZhFactMatchKind.Normalized);
    }

    [Fact]
    public void Hard_fact_checker_requires_acronym_exact_preservation()
    {
        var sso = Fact("fact_sso", "SSO", RewriteFactCategory.Identifier);
        var api = Fact("fact_api", "API", RewriteFactCategory.Identifier);

        StageOneHardFactChecker.Check(sso, "该计划包括 SSO 和 API 访问权限。").Status.Should().Be(ZhFactCheckStatus.Present);
        StageOneHardFactChecker.Check(api, "该计划包括 SSO 和 API 访问权限。").Status.Should().Be(ZhFactCheckStatus.Present);
        StageOneHardFactChecker.Check(sso, "该计划包括单点登录和接口访问权限。").FailureKind
            .Should().Be(ZhFactFailureKind.ExactRequiredButTranslated);
        StageOneHardFactChecker.Check(api, "该计划包括单点登录和接口访问权限。").FailureKind
            .Should().Be(ZhFactFailureKind.ExactRequiredButTranslated);
        StageOneHardFactChecker.Check(sso, "该计划包括 SSO（单点登录）和 API 访问权限。").MatchKind
            .Should().Be(ZhFactMatchKind.Exact);
    }

    [Fact]
    public void Hard_fact_checker_accepts_allowed_alias_but_rejects_generic_product_name()
    {
        var portal = Fact("fact_portal", "Acme Billing Portal", RewriteFactCategory.Identifier) with
        {
            PreserveMode = RewriteFactPreserveMode.ExactOrTranslatedAlias,
            AllowedAliases = new[] { "Acme 计费门户" },
        };

        StageOneHardFactChecker.Check(portal, "请登录 Acme 计费门户。").MatchKind.Should().Be(ZhFactMatchKind.Alias);
        StageOneHardFactChecker.Check(portal, "请登录账单系统。").FailureKind.Should().Be(ZhFactFailureKind.EntityGeneralized);
    }

    [Fact]
    public void Hard_fact_checker_does_not_auto_pass_unapproved_proposed_alias()
    {
        var portal = Fact("fact_portal", "Acme Billing Portal", RewriteFactCategory.Identifier) with
        {
            PreserveMode = RewriteFactPreserveMode.ExactOrTranslatedAlias,
            ProposedAliases = new[] { "Acme 账单入口" },
        };

        var item = StageOneHardFactChecker.Check(portal, "请登录 Acme 账单入口。");

        item.Status.Should().Be(ZhFactCheckStatus.Ambiguous);
        item.MatchKind.Should().Be(ZhFactMatchKind.None);
        item.FailureKind.Should().Be(ZhFactFailureKind.AliasNotApproved);
        item.ZhEvidence.Should().Be("Acme 账单入口");
        item.RecommendedNextAction.Should().Be("approve_alias");
    }

    [Fact]
    public void Hard_fact_checker_routes_unmatched_exact_or_alias_entity_to_alias_review()
    {
        var entity = Fact("fact_place", "Riverside Park", RewriteFactCategory.Identifier) with
        {
            PreserveMode = RewriteFactPreserveMode.ExactOrTranslatedAlias,
        };

        var item = StageOneHardFactChecker.Check(entity, "大家好，这是本周六河滨公园清洁日的详细信息。");

        item.Status.Should().Be(ZhFactCheckStatus.Ambiguous);
        item.FailureKind.Should().Be(ZhFactFailureKind.AliasNotApproved);
        item.RecommendedNextAction.Should().Be("approve_alias");
    }

    [Fact]
    public void Hard_fact_checker_routes_transliterated_person_names_to_alias_review()
    {
        var person = Fact("fact_person", "Jamie", RewriteFactCategory.Person);

        var item = StageOneHardFactChecker.Check(person, "嗨，杰米，我检查了记录。");

        item.Status.Should().Be(ZhFactCheckStatus.Ambiguous);
        item.FailureKind.Should().Be(ZhFactFailureKind.AliasNotApproved);
        item.RecommendedNextAction.Should().Be("approve_alias");
    }

    [Fact]
    public void Approved_alias_catalog_promotes_known_person_alias_to_allowed_alias()
    {
        var ledger = new RewriteFactLedger(new[]
        {
            Fact("fact_person", "Jamie", RewriteFactCategory.Person),
        });

        var approved = StageOneApprovedAliasCatalog.Apply(ledger);
        var item = StageOneHardFactChecker.Check(approved.Facts.Single(), "嗨，杰米，我检查了记录。");

        approved.Facts.Single().PreserveMode.Should().Be(RewriteFactPreserveMode.ExactOrTranslatedAlias);
        approved.Facts.Single().AllowedAliases.Should().Contain("杰米");
        item.Status.Should().Be(ZhFactCheckStatus.Present);
        item.MatchKind.Should().Be(ZhFactMatchKind.Alias);
    }

    [Fact]
    public void Approved_alias_catalog_does_not_auto_approve_unknown_proposed_alias()
    {
        var ledger = new RewriteFactLedger(new[]
        {
            Fact("fact_portal", "Acme Billing Portal", RewriteFactCategory.Identifier) with
            {
                PreserveMode = RewriteFactPreserveMode.ExactOrTranslatedAlias,
                ProposedAliases = new[] { "Acme 账单入口" },
            },
        });

        var approved = StageOneApprovedAliasCatalog.Apply(ledger);
        var item = StageOneHardFactChecker.Check(approved.Facts.Single(), "请登录 Acme 账单入口。");

        approved.Facts.Single().AllowedAliases.Should().BeNullOrEmpty();
        item.Status.Should().Be(ZhFactCheckStatus.Ambiguous);
        item.FailureKind.Should().Be(ZhFactFailureKind.AliasNotApproved);
    }

    [Fact]
    public void Approved_alias_catalog_promotes_wide_sample_person_and_place_aliases()
    {
        var ledger = new RewriteFactLedger(new[]
        {
            Fact("fact_lena", "Lena", RewriteFactCategory.Person),
            Fact("fact_priya_shah", "Priya Shah", RewriteFactCategory.Person) with
            {
                PreserveMode = RewriteFactPreserveMode.ExactOrTranslatedAlias,
            },
            Fact("fact_riverside", "Riverside Park", RewriteFactCategory.Identifier) with
            {
                PreserveMode = RewriteFactPreserveMode.ExactOrTranslatedAlias,
            },
            Fact("fact_hall", "Hall B", RewriteFactCategory.Identifier) with
            {
                PreserveMode = RewriteFactPreserveMode.ExactOrTranslatedAlias,
            },
        });

        var approved = StageOneApprovedAliasCatalog.Apply(ledger);

        StageOneHardFactChecker.Check(approved.Facts.Single(f => f.Text == "Lena"), "嗨，莉娜。")
            .MatchKind.Should().Be(ZhFactMatchKind.Alias);
        StageOneHardFactChecker.Check(approved.Facts.Single(f => f.Text == "Priya Shah"), "普丽娅·沙阿负责合作伙伴门户。")
            .MatchKind.Should().Be(ZhFactMatchKind.Alias);
        StageOneHardFactChecker.Check(approved.Facts.Single(f => f.Text == "Riverside Park"), "河滨公园清洁日。")
            .MatchKind.Should().Be(ZhFactMatchKind.Alias);
        StageOneHardFactChecker.Check(approved.Facts.Single(f => f.Text == "Hall B"), "我们仍然在B大厅集合。")
            .MatchKind.Should().Be(ZhFactMatchKind.Alias);
    }

    [Fact]
    public void Alias_glossary_routes_unapproved_entries_to_proposed_alias_review()
    {
        var ledger = new RewriteFactLedger(new[]
        {
            Fact("fact_portal", "Acme Billing Portal", RewriteFactCategory.Identifier) with
            {
                PreserveMode = RewriteFactPreserveMode.ExactOrTranslatedAlias,
            },
        });
        var entries = new[]
        {
            new StageOneAliasEntry(
                SourceText: "Acme Billing Portal",
                AliasText: "Acme 账单入口",
                AliasLanguage: "zh-Hans",
                Source: "test-proposed",
                Approved: false,
                DomainScope: "stage1-test"),
        };

        var applied = StageOneApprovedAliasCatalog.Apply(ledger, entries, domainScope: "stage1-test");
        var fact = applied.Facts.Single();
        var item = StageOneHardFactChecker.Check(fact, "请登录 Acme 账单入口。");

        fact.AllowedAliases.Should().BeNullOrEmpty();
        fact.ProposedAliases.Should().Contain("Acme 账单入口");
        item.Status.Should().Be(ZhFactCheckStatus.Ambiguous);
        item.FailureKind.Should().Be(ZhFactFailureKind.AliasNotApproved);
    }

    [Fact]
    public void Alias_glossary_applies_only_matching_domain_scope()
    {
        var ledger = new RewriteFactLedger(new[]
        {
            Fact("fact_lena", "Lena", RewriteFactCategory.Person),
        });
        var entries = new[]
        {
            new StageOneAliasEntry(
                SourceText: "Lena",
                AliasText: "莉娜",
                AliasLanguage: "zh-Hans",
                Source: "other-domain-fixture",
                Approved: true,
                DomainScope: "other-domain"),
        };

        var applied = StageOneApprovedAliasCatalog.Apply(ledger, entries, domainScope: "stage1-test");

        applied.Facts.Single().AllowedAliases.Should().BeNullOrEmpty();
        StageOneHardFactChecker.Check(applied.Facts.Single(), "嗨，莉娜。").FailureKind
            .Should().Be(ZhFactFailureKind.AliasNotApproved);
    }

    [Fact]
    public void Hard_fact_checker_marks_chinese_numeral_duration_as_normalizer_gap()
    {
        var duration = Fact("fact_duration", "thirty days", RewriteFactCategory.Count) with
        {
            PreserveMode = RewriteFactPreserveMode.Normalized,
            Normalized = "30:day",
        };

        var item = StageOneHardFactChecker.Check(duration, "客户可在三十天内申请退款。");

        item.Status.Should().Be(ZhFactCheckStatus.Ambiguous);
        item.FailureKind.Should().Be(ZhFactFailureKind.NormalizerGap);
        item.RecommendedNextAction.Should().Be("add_normalizer_case");
    }

    [Fact]
    public void Hard_fact_checker_marks_ordinary_phrase_as_over_extracted()
    {
        var phrase = Fact("fact_phrase", "approval process", RewriteFactCategory.Other);

        var item = StageOneHardFactChecker.Check(phrase, "审批流程仍在继续。");

        item.Status.Should().Be(ZhFactCheckStatus.Ambiguous);
        item.FailureKind.Should().Be(ZhFactFailureKind.OverExtractedNonHardFact);
        item.RecommendedNextAction.Should().Be("demote_to_term");
    }

    [Fact]
    public async Task Repair_request_omits_normalized_or_alias_passed_facts()
    {
        var date = Fact("fact_date", "March 31, 2026", RewriteFactCategory.DateOrDeadline) with
        {
            PreserveMode = RewriteFactPreserveMode.Normalized,
            Normalized = "2026-03-31",
        };
        var sso = Fact("fact_sso", "SSO", RewriteFactCategory.Identifier);
        var repairer = new StaticZhMinimalRepairer("续订截止日期为2026年3月31日，并且 SSO 可用。");

        await StageOneZhRepairLoop.RunAsync(
            "case-normalized",
            "The renewal deadline is March 31, 2026. SSO remains available.",
            "续订截止日期为2026年3月31日，单点登录可用。",
            new RewriteFactLedger(new[] { date, sso }),
            new RewriteClaimLedger(new[] { Claim("C001", "SSO remains available.", RewriteClaimModality.Certainty) }),
            new StaticZhClaimSurvivalJudge(new ZhClaimSurvivalVerdict("C001", ZhClaimSurvivalStatus.Yes, "preserved")),
            repairer,
            maxRepairAttempts: 1,
            CancellationToken.None);

        repairer.Requests.Should().ContainSingle();
        repairer.Requests[0].Report.FactsDrifted.Select(f => f.Id).Should().Equal("fact_sso");
    }

    [Fact]
    public async Task Repair_request_excludes_non_actionable_fact_failures()
    {
        var missing = Fact("fact_missing", "SSO", RewriteFactCategory.Identifier);
        var aliasReview = Fact("fact_alias", "Acme Billing Portal", RewriteFactCategory.Identifier) with
        {
            PreserveMode = RewriteFactPreserveMode.ExactOrTranslatedAlias,
            ProposedAliases = new[] { "Acme 账单入口" },
        };
        var normalizerGap = Fact("fact_duration", "thirty days", RewriteFactCategory.Count) with
        {
            PreserveMode = RewriteFactPreserveMode.Normalized,
            Normalized = "30:day",
        };
        var overExtracted = Fact("fact_phrase", "approval process", RewriteFactCategory.Other);
        var repairer = new StaticZhMinimalRepairer("SSO 仍然可用。");

        await StageOneZhRepairLoop.RunAsync(
            "case-repair-filter",
            "SSO remains available. Acme Billing Portal is available for thirty days.",
            "单点登录仍然可用。请登录 Acme 账单入口。审批流程为三十天。",
            new RewriteFactLedger(new[] { missing, aliasReview, normalizerGap, overExtracted }),
            new RewriteClaimLedger(new[] { Claim("C001", "SSO remains available.", RewriteClaimModality.Certainty) }),
            new StaticZhClaimSurvivalJudge(new ZhClaimSurvivalVerdict("C001", ZhClaimSurvivalStatus.Yes, "preserved")),
            repairer,
            maxRepairAttempts: 1,
            CancellationToken.None);

        repairer.Requests.Should().ContainSingle();
        repairer.Requests[0].Report.FactsDrifted.Select(f => f.Id).Should().Equal("fact_missing");
        repairer.Requests[0].Report.FactChecks.Where(c => c.Status != ZhFactCheckStatus.Present).Select(c => c.FailureKind)
            .Should().OnlyContain(kind => kind == ZhFactFailureKind.ExactRequiredButTranslated);
    }

    [Fact]
    public void Hard_fact_builder_filters_semantic_sentence_facts_and_adds_normalized_metadata()
    {
        var rawLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_money", "$12,500", RewriteFactCategory.Amount),
            Fact("fact_date", "March 31, 2026", RewriteFactCategory.DateOrDeadline),
            Fact("fact_negative", "I cannot approve the discount without review.", RewriteFactCategory.NegativeConstraint),
            Fact("fact_word_count", "one", RewriteFactCategory.Count),
        });

        var built = StageOneHardFactLedgerBuilder.Build(
            rawLedger,
            "The annual fee is $12,500. The renewal deadline is March 31, 2026. The plan includes SSO and API access.");

        built.Facts.Select(f => f.Id).Should().Contain(new[] { "fact_money", "fact_date", "fact_acronym_sso", "fact_acronym_api" });
        built.Facts.Select(f => f.Id).Should().NotContain(new[] { "fact_negative", "fact_word_count" });
        built.Facts.Single(f => f.Id == "fact_money").Normalized.Should().Be("USD:12500");
        built.Facts.Single(f => f.Id == "fact_date").Normalized.Should().Be("2026-03-31");
        built.Facts.Single(f => f.Id == "fact_acronym_sso").PreserveMode.Should().Be(RewriteFactPreserveMode.Exact);
    }

    [Fact]
    public void Hard_fact_builder_normalizes_weekday_names()
    {
        var rawLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_tuesday", "Tuesday", RewriteFactCategory.DateOrDeadline),
        });

        var built = StageOneHardFactLedgerBuilder.Build(rawLedger, "I expect to send the next update by Tuesday.");

        built.Facts.Single().Normalized.Should().Be("weekday:tuesday");
    }

    [Fact]
    public void Hard_fact_builder_filters_clock_minute_counts_but_keeps_quantities()
    {
        var rawLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_minute", "30", RewriteFactCategory.Count),
            Fact("fact_quantity", "18", RewriteFactCategory.Count),
        });

        var built = StageOneHardFactLedgerBuilder.Build(
            rawLedger,
            "I will send another update by 5:30 p.m. The quote is for 18 seats.");

        built.Facts.Select(f => f.Id).Should().NotContain("fact_minute");
        built.Facts.Single(f => f.Id == "fact_quantity").Normalized.Should().Be("18");
    }

    [Fact]
    public void Hard_fact_builder_filters_clock_hour_counts_but_keeps_quantities()
    {
        var rawLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_hour_two", "2", RewriteFactCategory.Count),
            Fact("fact_hour_nine", "9", RewriteFactCategory.Count),
            Fact("fact_quantity", "18", RewriteFactCategory.Count),
        });

        var built = StageOneHardFactLedgerBuilder.Build(
            rawLedger,
            "I can offer Thursday at 2:30 p.m. or Friday at 9 a.m. The quote is for 18 seats.");

        built.Facts.Select(f => f.Id).Should().NotContain(new[] { "fact_hour_two", "fact_hour_nine" });
        built.Facts.Single(f => f.Id == "fact_quantity").Normalized.Should().Be("18");
    }

    [Fact]
    public void Hard_fact_builder_filters_capitalized_words_that_are_not_names()
    {
        var rawLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_jamie", "Jamie", RewriteFactCategory.Person),
            Fact("fact_there", "There", RewriteFactCategory.Person),
            Fact("fact_what", "What", RewriteFactCategory.Person),
            Fact("fact_team", "Team", RewriteFactCategory.Person),
        });

        var built = StageOneHardFactLedgerBuilder.Build(rawLedger, "Jamie can reply after the team review.");

        built.Facts.Select(f => f.Id).Should().Equal("fact_jamie");
    }

    [Fact]
    public void Hard_fact_builder_filters_wide_sample_capitalized_words_that_are_not_entities()
    {
        var rawLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_lena", "Lena", RewriteFactCategory.Person),
            Fact("fact_priya", "Priya", RewriteFactCategory.Person),
            Fact("fact_our", "Our", RewriteFactCategory.Person),
            Fact("fact_your", "Your", RewriteFactCategory.Person),
            Fact("fact_after", "After", RewriteFactCategory.Person),
            Fact("fact_they", "They", RewriteFactCategory.Person),
            Fact("fact_could", "Could", RewriteFactCategory.Person),
            Fact("fact_both", "Both", RewriteFactCategory.Person),
            Fact("fact_weekend", "Weekend", RewriteFactCategory.Person),
            Fact("fact_meals", "Meals", RewriteFactCategory.Person),
            Fact("fact_room", "Room", RewriteFactCategory.Person),
            Fact("fact_questions", "Questions", RewriteFactCategory.Person),
            Fact("fact_email", "Email", RewriteFactCategory.Person),
        });

        var built = StageOneHardFactLedgerBuilder.Build(
            rawLedger,
            "Lena and Priya can reply after the support review. Your team can use Room 12 for weekend meals. Questions can go by email.");

        built.Facts.Select(f => f.Id).Should().Equal(new[] { "fact_lena", "fact_priya" });
    }

    [Fact]
    public void Hard_fact_builder_merges_title_case_entity_fragments_before_alias_review()
    {
        var rawLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_riverside", "Riverside", RewriteFactCategory.Person),
            Fact("fact_park", "Park", RewriteFactCategory.Person),
            Fact("fact_elm", "Elm", RewriteFactCategory.Person),
            Fact("fact_street", "Street", RewriteFactCategory.Person),
        });

        var built = StageOneHardFactLedgerBuilder.Build(
            rawLedger,
            "The Riverside Park clean-up day meets at the Elm Street entrance.");

        built.Facts.Select(f => f.Text).Should().Contain(new[] { "Riverside Park", "Elm Street" });
        built.Facts.Select(f => f.Text).Should().NotContain(new[] { "Riverside", "Park", "Elm", "Street" });
        built.Facts.Single(f => f.Text == "Riverside Park").PreserveMode.Should().Be(RewriteFactPreserveMode.ExactOrTranslatedAlias);
    }

    [Fact]
    public void Hard_fact_builder_merges_letter_suffixed_location_but_keeps_salutation_names()
    {
        var rawLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_hi", "Hi", RewriteFactCategory.Person),
            Fact("fact_felix", "Felix", RewriteFactCategory.Person),
            Fact("fact_hall", "Hall", RewriteFactCategory.Person),
            Fact("fact_package", "Package", RewriteFactCategory.Person),
            Fact("fact_volunteer", "Volunteer", RewriteFactCategory.Person),
            Fact("fact_parking", "Parking", RewriteFactCategory.Person),
        });

        var built = StageOneHardFactLedgerBuilder.Build(
            rawLedger,
            "Hi Felix, Package 1 arrived. Volunteer check-in is in Hall B. Parking is in the east lot only.");

        built.Facts.Select(f => f.Text).Should().Contain(new[] { "Felix", "Hall B" });
        built.Facts.Select(f => f.Text).Should().NotContain(new[] { "Hi Felix", "Hi", "Hall", "Package", "Volunteer", "Parking" });
        built.Facts.Single(f => f.Text == "Hall B").PreserveMode.Should().Be(RewriteFactPreserveMode.ExactOrTranslatedAlias);
    }

    [Fact]
    public void Hard_fact_builder_keeps_merged_person_names_as_person_alias_review()
    {
        var rawLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_priya", "Priya", RewriteFactCategory.Person),
            Fact("fact_shah", "Shah", RewriteFactCategory.Person),
        });

        var built = StageOneHardFactLedgerBuilder.Build(
            rawLedger,
            "Priya Shah owns the partner portal.");
        var merged = built.Facts.Single(f => f.Text == "Priya Shah");
        var item = StageOneHardFactChecker.Check(merged, "普丽娅·沙阿负责合作伙伴门户。");

        merged.Category.Should().Be(RewriteFactCategory.Person);
        item.FailureKind.Should().Be(ZhFactFailureKind.AliasNotApproved);
        item.RecommendedNextAction.Should().Be("approve_alias");
    }

    [Fact]
    public void Hard_fact_builder_demotes_role_title_fragments_from_fact_ledger()
    {
        var rawLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_senior", "Senior", RewriteFactCategory.Person),
            Fact("fact_support", "Support", RewriteFactCategory.Person),
            Fact("fact_lead", "Lead", RewriteFactCategory.Person),
            Fact("fact_coordinator", "Coordinator", RewriteFactCategory.Person),
            Fact("fact_luis", "Luis", RewriteFactCategory.Person),
        });

        var built = StageOneHardFactLedgerBuilder.Build(
            rawLedger,
            "Luis is the hiring contact for the Operations Coordinator role, not the Senior Support Lead role.");

        built.Facts.Select(f => f.Text).Should().Equal("Luis");
    }

    [Fact]
    public void Report_renderer_includes_case_rows_and_drift_details()
    {
        var report = new ZhPostCheckReport(
            CaseId: "rewrite-draft-014",
            OriginalEn: "I cannot refund the full June charge.",
            TranslatedZh: "我不能退还六月费用。",
            FactsSurvived: new[] { Fact("fact_amount", "$42.00", RewriteFactCategory.Amount) },
            FactsDrifted: new[] { Fact("fact_id", "INV-8842", RewriteFactCategory.Identifier) },
            ClaimsSurvived: Array.Empty<RewriteClaim>(),
            ClaimsDrifted: new[]
            {
                Claim("C002", "I cannot refund the full June charge.", RewriteClaimModality.Prohibition),
            },
            FactSurvivalPct: 50,
            ClaimSurvivalPct: 0)
        {
            FactDriftReasons = new Dictionary<string, string> { ["fact_id"] = "numeric_anchor_missing" },
            ClaimVerdicts = new Dictionary<string, ZhClaimSurvivalVerdict>
            {
                ["C002"] = new("C002", ZhClaimSurvivalStatus.Partial, "full amount missing"),
            },
        };

        var markdown = StageOneZhReportRenderer.Render(
            DateTimeOffset.Parse("2026-05-28T00:00:00Z"),
            new[] { report },
            youdaoCalls: 1,
            deepSeekCalls: 1,
            model: "deepseek-v4-pro");

        markdown.Should().Contain("| rewrite-draft-014 | 50.0% | 0.0% | 1/2 | 0/1 |");
        markdown.Should().Contain("fact_id: `INV-8842` - numeric_anchor_missing");
        markdown.Should().Contain("C002: partial - full amount missing");
        markdown.Should().Contain("I cannot refund the full June charge.");
    }

    [Fact]
    public async Task Repair_loop_repairs_once_and_rechecks_when_initial_report_has_drifts()
    {
        var factLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_sso", "SSO", RewriteFactCategory.Identifier),
        });
        var claims = new[]
        {
            Claim("C001", "Customers may request a refund within 30 days.", RewriteClaimModality.Permission),
        };
        var judge = new TextSensitiveZhClaimJudge(
            failingText: "客户可以在30天内获得退款。",
            claimId: "C001",
            failingReason: "permission was strengthened to entitlement");
        var repairer = new StaticZhMinimalRepairer("客户可在30天内申请退款，并且 SSO 保持可用。");

        var result = await StageOneZhRepairLoop.RunAsync(
            "case-repair",
            "Customers may request a refund within 30 days. SSO remains available.",
            "客户可以在30天内获得退款。",
            factLedger,
            new RewriteClaimLedger(claims),
            judge,
            repairer,
            maxRepairAttempts: 1,
            CancellationToken.None);

        result.RepairAttempts.Should().Be(1);
        result.RawTranslatedZh.Should().Be("客户可以在30天内获得退款。");
        result.FinalZh.Should().Be("客户可在30天内申请退款，并且 SSO 保持可用。");
        result.InitialReport.HasErrors.Should().BeTrue();
        result.FinalReport.HasErrors.Should().BeFalse();
        repairer.Requests.Should().ContainSingle();
        repairer.Requests[0].Report.ClaimsDrifted.Select(c => c.Id).Should().Equal("C001");
    }

    [Fact]
    public async Task Repair_loop_does_not_call_repairer_when_initial_report_passes()
    {
        var factLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_sso", "SSO", RewriteFactCategory.Identifier),
        });
        var repairer = new StaticZhMinimalRepairer("should not be used");

        var result = await StageOneZhRepairLoop.RunAsync(
            "case-pass",
            "SSO remains available.",
            "SSO 仍然可用。",
            factLedger,
            new RewriteClaimLedger(new[] { Claim("C001", "SSO remains available.", RewriteClaimModality.Certainty) }),
            new StaticZhClaimSurvivalJudge(new ZhClaimSurvivalVerdict("C001", ZhClaimSurvivalStatus.Yes, "preserved")),
            repairer,
            maxRepairAttempts: 1,
            CancellationToken.None);

        result.RepairAttempts.Should().Be(0);
        result.FinalZh.Should().Be("SSO 仍然可用。");
        result.FinalReport.HasErrors.Should().BeFalse();
        repairer.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Repair_loop_rejects_repair_that_makes_claim_survival_worse()
    {
        var factLedger = new RewriteFactLedger(new[]
        {
            Fact("fact_sso", "SSO", RewriteFactCategory.Identifier),
        });
        var claimLedger = new RewriteClaimLedger(new[]
        {
            Claim("C001", "Customers may request a refund within 30 days.", RewriteClaimModality.Permission),
        });
        var judge = new TextSensitiveZhClaimJudge(
            failingText: "客户可在30天内申请退款，并且 SSO 保持可用。",
            claimId: "C001",
            failingReason: "repair removed the permission nuance");
        var repairer = new StaticZhMinimalRepairer("客户可在30天内申请退款，并且 SSO 保持可用。");

        var result = await StageOneZhRepairLoop.RunAsync(
            "case-regression",
            "Customers may request a refund within 30 days. SSO remains available.",
            "客户可在30天内申请退款。",
            factLedger,
            claimLedger,
            judge,
            repairer,
            maxRepairAttempts: 1,
            CancellationToken.None);

        result.RepairAttempts.Should().Be(1);
        result.FinalZh.Should().Be("客户可在30天内申请退款。");
        result.FinalReport.ClaimsSurvived.Should().HaveCount(1);
        result.Warnings.Should().Contain("repair_rejected_claim_regression");
    }

    [Fact]
    public void Safe_intermediate_renderer_includes_raw_repaired_and_before_after_status()
    {
        var initial = new ZhPostCheckReport(
            CaseId: "case-render",
            OriginalEn: "SSO remains available.",
            TranslatedZh: "单点登录仍然可用。",
            FactsSurvived: Array.Empty<RewriteFact>(),
            FactsDrifted: new[] { Fact("fact_sso", "SSO", RewriteFactCategory.Identifier) },
            ClaimsSurvived: Array.Empty<RewriteClaim>(),
            ClaimsDrifted: Array.Empty<RewriteClaim>(),
            FactSurvivalPct: 0,
            ClaimSurvivalPct: 100)
        {
            FactDriftReasons = new Dictionary<string, string> { ["fact_sso"] = "verbatim_anchor_missing" },
        };
        var final = initial with
        {
            TranslatedZh = "SSO 仍然可用。",
            FactsSurvived = new[] { Fact("fact_sso", "SSO", RewriteFactCategory.Identifier) },
            FactsDrifted = Array.Empty<RewriteFact>(),
            FactSurvivalPct = 100,
            FactDriftReasons = new Dictionary<string, string>(),
        };
        var result = new ZhSafeIntermediateResult(
            "case-render",
            "SSO remains available.",
            "单点登录仍然可用。",
            "SSO 仍然可用。",
            initial,
            final,
            RepairAttempts: 1);

        var markdown = StageOneZhReportRenderer.RenderSafeIntermediates(
            DateTimeOffset.Parse("2026-05-28T00:00:00Z"),
            new[] { result },
            youdaoCalls: 1,
            deepSeekCalls: 3,
            model: "deepseek-chat");

        markdown.Should().Contain("| case-render | 0.0% | 100.0% | 100.0% | 100.0% | 1 | pass |");
        markdown.Should().Contain("<details><summary>Raw Youdao ZH</summary>");
        markdown.Should().Contain("单点登录仍然可用。");
        markdown.Should().Contain("<details><summary>Final Safe ZH</summary>");
        markdown.Should().Contain("SSO 仍然可用。");
    }

    [Fact]
    public void Safe_intermediate_renderer_includes_failure_breakdown()
    {
        var missing = Fact("fact_sso", "SSO", RewriteFactCategory.Identifier);
        var alias = Fact("fact_portal", "Acme Billing Portal", RewriteFactCategory.Identifier) with
        {
            PreserveMode = RewriteFactPreserveMode.ExactOrTranslatedAlias,
            ProposedAliases = new[] { "Acme 账单入口" },
        };
        var report = new ZhPostCheckReport(
            CaseId: "case-breakdown",
            OriginalEn: "SSO remains available in Acme Billing Portal.",
            TranslatedZh: "单点登录在 Acme 账单入口中仍然可用。",
            FactsSurvived: Array.Empty<RewriteFact>(),
            FactsDrifted: new[] { missing, alias },
            ClaimsSurvived: Array.Empty<RewriteClaim>(),
            ClaimsDrifted: Array.Empty<RewriteClaim>(),
            FactSurvivalPct: 0,
            ClaimSurvivalPct: 100)
        {
            FactChecks = new[]
            {
                StageOneHardFactChecker.Check(missing, "单点登录在 Acme 账单入口中仍然可用。"),
                StageOneHardFactChecker.Check(alias, "单点登录在 Acme 账单入口中仍然可用。"),
            },
        };
        var result = new ZhSafeIntermediateResult(
            "case-breakdown",
            report.OriginalEn,
            report.TranslatedZh,
            report.TranslatedZh,
            report,
            report,
            RepairAttempts: 0);

        var markdown = StageOneZhReportRenderer.RenderSafeIntermediates(
            DateTimeOffset.Parse("2026-05-28T00:00:00Z"),
            new[] { result },
            youdaoCalls: 1,
            deepSeekCalls: 1,
            model: "deepseek-chat");

        markdown.Should().Contain("## Final failure breakdown");
        markdown.Should().Contain("- exact_required_but_translated: `1`");
        markdown.Should().Contain("- alias_not_approved: `1`");
        markdown.Should().Contain("recommended_next_action: `approve_alias`");
    }

    [Fact]
    public void Safe_intermediate_renderer_includes_final_drifts_for_unrepaired_failures()
    {
        var missing = Fact("fact_sso", "SSO", RewriteFactCategory.Identifier);
        var report = new ZhPostCheckReport(
            CaseId: "case-unrepaired-fail",
            OriginalEn: "SSO remains available.",
            TranslatedZh: "单点登录仍然可用。",
            FactsSurvived: Array.Empty<RewriteFact>(),
            FactsDrifted: new[] { missing },
            ClaimsSurvived: Array.Empty<RewriteClaim>(),
            ClaimsDrifted: Array.Empty<RewriteClaim>(),
            FactSurvivalPct: 0,
            ClaimSurvivalPct: 100)
        {
            FactChecks = new[] { StageOneHardFactChecker.Check(missing, "单点登录仍然可用。") },
        };
        var result = new ZhSafeIntermediateResult(
            "case-unrepaired-fail",
            report.OriginalEn,
            report.TranslatedZh,
            report.TranslatedZh,
            report,
            report,
            RepairAttempts: 0);

        var markdown = StageOneZhReportRenderer.RenderSafeIntermediates(
            DateTimeOffset.Parse("2026-05-28T00:00:00Z"),
            new[] { result },
            youdaoCalls: 1,
            deepSeekCalls: 1,
            model: "deepseek-chat");

        markdown.Should().Contain("Final fact drifts:");
        markdown.Should().Contain("fact_sso: `SSO`");
    }

    private static RewriteFact Fact(string id, string text, RewriteFactCategory category) =>
        new(
            Id: id,
            Text: text,
            Source: "roughDraftReply",
            Importance: RewriteFactImportance.Critical,
            Category: category,
            CanBeRephrased: false,
            SourceSpan: text);

    private static RewriteClaim Claim(string id, string sourceSpan, RewriteClaimModality modality) =>
        new(
            Id: id,
            SourceSpan: sourceSpan,
            Subject: "subject",
            Action: "action",
            Object: "object",
            Modality: modality,
            Polarity: modality == RewriteClaimModality.Prohibition
                ? RewriteClaimPolarity.Negative
                : RewriteClaimPolarity.Positive,
            TimeScope: null,
            Condition: null,
            MustPreserve: Array.Empty<string>());

    private sealed class StaticZhClaimSurvivalJudge(params ZhClaimSurvivalVerdict[] verdicts)
        : IZhClaimSurvivalJudge
    {
        public Task<IReadOnlyDictionary<string, ZhClaimSurvivalVerdict>> JudgeAsync(
            string originalEn,
            string translatedZh,
            IReadOnlyList<RewriteClaim> claims,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<string, ZhClaimSurvivalVerdict> result = verdicts.ToDictionary(v => v.ClaimId);
            return Task.FromResult(result);
        }
    }

    private sealed class TextSensitiveZhClaimJudge(
        string failingText,
        string claimId,
        string failingReason) : IZhClaimSurvivalJudge
    {
        public Task<IReadOnlyDictionary<string, ZhClaimSurvivalVerdict>> JudgeAsync(
            string originalEn,
            string translatedZh,
            IReadOnlyList<RewriteClaim> claims,
            CancellationToken cancellationToken)
        {
            var verdict = translatedZh == failingText
                ? new ZhClaimSurvivalVerdict(claimId, ZhClaimSurvivalStatus.Partial, failingReason)
                : new ZhClaimSurvivalVerdict(claimId, ZhClaimSurvivalStatus.Yes, "preserved");
            IReadOnlyDictionary<string, ZhClaimSurvivalVerdict> result = new Dictionary<string, ZhClaimSurvivalVerdict>
            {
                [claimId] = verdict,
            };
            return Task.FromResult(result);
        }
    }

    private sealed class StaticZhMinimalRepairer(string repairedText) : IZhMinimalRepairer
    {
        public List<ZhMinimalRepairRequest> Requests { get; } = new();

        public Task<string?> RepairAsync(ZhMinimalRepairRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult<string?>(repairedText);
        }
    }
}
