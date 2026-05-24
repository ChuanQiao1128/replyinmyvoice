using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReplyInMyVoice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcilePostgresOnlyModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlanTier = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RateLimitPerMinute = table.Column<int>(type: "int", nullable: false),
                    MonthlyQuota = table.Column<int>(type: "int", nullable: false),
                    CurrentPeriodUsage = table.Column<int>(type: "int", nullable: false),
                    CurrentPeriodStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    MeasuredCount = table.Column<int>(type: "int", nullable: false),
                    FindingCount = table.Column<int>(type: "int", nullable: false),
                    CandidateCount = table.Column<int>(type: "int", nullable: false),
                    PromotionDecision = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ValidationStatus = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DigestPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Referrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferrerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefereeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreditedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SignupIpHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Referrals_AppUsers_ReferrerId",
                        column: x => x.ReferrerId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RewriteCanaryRollbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanaryStrategyVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ControlStrategyVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Scenario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    WindowRewrites = table.Column<int>(type: "int", nullable: false),
                    RollingAverageSignalDrop = table.Column<double>(type: "float", nullable: false),
                    BaselineAverageSignalDrop = table.Column<double>(type: "float", nullable: false),
                    RegressionPoints = table.Column<double>(type: "float", nullable: false),
                    WindowStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WindowEndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AdminEmailStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AdminEmailSentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GithubIssueStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GithubIssueUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GithubIssueOpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewriteCanaryRollbacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RewriteCostLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LearningSampleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    StrategyVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Scenario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TonePreset = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    InputCharCount = table.Column<int>(type: "int", nullable: false),
                    DraftWordCount = table.Column<int>(type: "int", nullable: false),
                    RewriteWordCount = table.Column<int>(type: "int", nullable: true),
                    DraftAiLikePercent = table.Column<int>(type: "int", nullable: true),
                    RewriteAiLikePercent = table.Column<int>(type: "int", nullable: true),
                    ChangePoints = table.Column<int>(type: "int", nullable: true),
                    InternalStrategies = table.Column<int>(type: "int", nullable: false),
                    RepairCandidates = table.Column<int>(type: "int", nullable: false),
                    RejectedCandidates = table.Column<int>(type: "int", nullable: false),
                    UsedEscalation = table.Column<bool>(type: "bit", nullable: false),
                    OpenAiInputTokens = table.Column<int>(type: "int", nullable: false),
                    OpenAiOutputTokens = table.Column<int>(type: "int", nullable: false),
                    OpenAiCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    SaplingCallCount = table.Column<int>(type: "int", nullable: false),
                    SaplingCharacters = table.Column<int>(type: "int", nullable: false),
                    SaplingCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalEstimatedCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ModelsUsedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderCallsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewriteCostLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewriteCostLogs_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RewriteCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AmountGranted = table.Column<int>(type: "int", nullable: false),
                    AmountConsumed = table.Column<int>(type: "int", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StripeEventId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewriteCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewriteCredits_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RewriteLearningSamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Scenario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TonePreset = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    MessageToReplyTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoughDraftReply = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RewrittenText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DraftAiLikePercent = table.Column<int>(type: "int", nullable: true),
                    RewriteAiLikePercent = table.Column<int>(type: "int", nullable: true),
                    ChangePoints = table.Column<int>(type: "int", nullable: true),
                    Label = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DiagnosisTags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RewritePlanSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CandidateSignals = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InternalStrategies = table.Column<int>(type: "int", nullable: false),
                    RepairCandidates = table.Column<int>(type: "int", nullable: false),
                    RejectedCandidates = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewriteLearningSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewriteLearningSamples_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeyUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    LatencyMs = table.Column<int>(type: "int", nullable: true),
                    CostUsdEstimate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeyUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeyUsages_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scenario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CommonTone = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PrimaryDiagnosisTag = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FailureType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DiagnosisTags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceCount = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PromotionRecommendation = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SampleRefs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningFindings_LearningRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "LearningRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RewriteProviderCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    Characters = table.Column<int>(type: "int", nullable: true),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    LatencyMs = table.Column<int>(type: "int", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewriteProviderCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewriteProviderCalls_RewriteCostLogs_CostLogId",
                        column: x => x.CostLogId,
                        principalTable: "RewriteCostLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StrategyCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Scenario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PatchTarget = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PatchAction = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PatchText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProposedChangeSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequiredRegressionTest = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RequiredEval = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EvidenceCount = table.Column<int>(type: "int", nullable: false),
                    LinkedCommitHash = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategyCandidates_LearningFindings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "LearningFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_PlanTier",
                table: "ApiKeys",
                column: "PlanTier");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_UserId_CreatedAt",
                table: "ApiKeys",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyUsages_ApiKeyId_CreatedAt",
                table: "ApiKeyUsages",
                columns: new[] { "ApiKeyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyUsages_CreatedAt",
                table: "ApiKeyUsages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyUsages_Endpoint",
                table: "ApiKeyUsages",
                column: "Endpoint");

            migrationBuilder.CreateIndex(
                name: "IX_LearningFindings_CommonTone",
                table: "LearningFindings",
                column: "CommonTone");

            migrationBuilder.CreateIndex(
                name: "IX_LearningFindings_CreatedAt",
                table: "LearningFindings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LearningFindings_FailureType",
                table: "LearningFindings",
                column: "FailureType");

            migrationBuilder.CreateIndex(
                name: "IX_LearningFindings_PrimaryDiagnosisTag",
                table: "LearningFindings",
                column: "PrimaryDiagnosisTag");

            migrationBuilder.CreateIndex(
                name: "IX_LearningFindings_PromotionRecommendation",
                table: "LearningFindings",
                column: "PromotionRecommendation");

            migrationBuilder.CreateIndex(
                name: "IX_LearningFindings_RunId",
                table: "LearningFindings",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningFindings_Scenario",
                table: "LearningFindings",
                column: "Scenario");

            migrationBuilder.CreateIndex(
                name: "IX_LearningFindings_Severity",
                table: "LearningFindings",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_LearningRuns_PromotionDecision",
                table: "LearningRuns",
                column: "PromotionDecision");

            migrationBuilder.CreateIndex(
                name: "IX_LearningRuns_StartedAt",
                table: "LearningRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LearningRuns_Status",
                table: "LearningRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_RefereeId",
                table: "Referrals",
                column: "RefereeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerId_CreditedAt",
                table: "Referrals",
                columns: new[] { "ReferrerId", "CreditedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCanaryRollbacks_CanaryStrategyVersion_CreatedAt",
                table: "RewriteCanaryRollbacks",
                columns: new[] { "CanaryStrategyVersion", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCanaryRollbacks_ResolvedAt",
                table: "RewriteCanaryRollbacks",
                column: "ResolvedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCanaryRollbacks_Scenario",
                table: "RewriteCanaryRollbacks",
                column: "Scenario");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCanaryRollbacks_State",
                table: "RewriteCanaryRollbacks",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCostLogs_CreatedAt",
                table: "RewriteCostLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCostLogs_RequestId",
                table: "RewriteCostLogs",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCostLogs_Scenario",
                table: "RewriteCostLogs",
                column: "Scenario");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCostLogs_Status",
                table: "RewriteCostLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCostLogs_StrategyVersion_CreatedAt",
                table: "RewriteCostLogs",
                columns: new[] { "StrategyVersion", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCostLogs_TotalEstimatedCostUsd",
                table: "RewriteCostLogs",
                column: "TotalEstimatedCostUsd");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCostLogs_UserId",
                table: "RewriteCostLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCredits_StripeEventId",
                table: "RewriteCredits",
                column: "StripeEventId");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteCredits_UserId_ExpiresAt",
                table: "RewriteCredits",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RewriteLearningSamples_CreatedAt",
                table: "RewriteLearningSamples",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteLearningSamples_Scenario",
                table: "RewriteLearningSamples",
                column: "Scenario");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteLearningSamples_Status",
                table: "RewriteLearningSamples",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteLearningSamples_UserId",
                table: "RewriteLearningSamples",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteProviderCalls_CostLogId",
                table: "RewriteProviderCalls",
                column: "CostLogId");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteProviderCalls_CreatedAt",
                table: "RewriteProviderCalls",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteProviderCalls_Provider",
                table: "RewriteProviderCalls",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_RewriteProviderCalls_Role",
                table: "RewriteProviderCalls",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyCandidates_CreatedAt",
                table: "StrategyCandidates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyCandidates_FindingId",
                table: "StrategyCandidates",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyCandidates_PatchTarget",
                table: "StrategyCandidates",
                column: "PatchTarget");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyCandidates_RiskLevel",
                table: "StrategyCandidates",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyCandidates_Scenario",
                table: "StrategyCandidates",
                column: "Scenario");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyCandidates_Status",
                table: "StrategyCandidates",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeyUsages");

            migrationBuilder.DropTable(
                name: "Referrals");

            migrationBuilder.DropTable(
                name: "RewriteCanaryRollbacks");

            migrationBuilder.DropTable(
                name: "RewriteCredits");

            migrationBuilder.DropTable(
                name: "RewriteLearningSamples");

            migrationBuilder.DropTable(
                name: "RewriteProviderCalls");

            migrationBuilder.DropTable(
                name: "StrategyCandidates");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "RewriteCostLogs");

            migrationBuilder.DropTable(
                name: "LearningFindings");

            migrationBuilder.DropTable(
                name: "LearningRuns");
        }
    }
}
