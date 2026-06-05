using Microsoft.EntityFrameworkCore;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UsagePeriod> UsagePeriods => Set<UsagePeriod>();
    public DbSet<RewriteAttempt> RewriteAttempts => Set<RewriteAttempt>();
    public DbSet<UsageReservation> UsageReservations => Set<UsageReservation>();
    public DbSet<StripeEvent> StripeEvents => Set<StripeEvent>();
    public DbSet<StripeInvoice> StripeInvoices => Set<StripeInvoice>();
    public DbSet<StripeReconciliationRun> StripeReconciliationRuns => Set<StripeReconciliationRun>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<RewriteCredit> RewriteCredits => Set<RewriteCredit>();
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<LearningRun> LearningRuns => Set<LearningRun>();
    public DbSet<LearningFinding> LearningFindings => Set<LearningFinding>();
    public DbSet<StrategyCandidate> StrategyCandidates => Set<StrategyCandidate>();
    public DbSet<RewriteLearningSample> RewriteLearningSamples => Set<RewriteLearningSample>();
    public DbSet<RewriteCostLog> RewriteCostLogs => Set<RewriteCostLog>();
    public DbSet<RewriteCanaryRollback> RewriteCanaryRollbacks => Set<RewriteCanaryRollback>();
    public DbSet<RewriteProviderCall> RewriteProviderCalls => Set<RewriteProviderCall>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ApiKeyUsage> ApiKeyUsages => Set<ApiKeyUsage>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<PromoCodeRedemption> PromoCodeRedemptions => Set<PromoCodeRedemption>();
    public DbSet<BillingSupportRequest> BillingSupportRequests => Set<BillingSupportRequest>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampConsentForAddedRewriteAttempts();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await StampConsentForAddedRewriteAttemptsAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ExternalAuthUserId).IsUnique();
            entity.HasIndex(x => x.StripeCustomerId).IsUnique().HasFilter("[StripeCustomerId] IS NOT NULL");
            entity.Property(x => x.ExternalAuthUserId).HasMaxLength(160);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.StripeCustomerId).HasMaxLength(160);
            entity.Property(x => x.StripeSubscriptionId).HasMaxLength(160);
            entity.Property(x => x.SubscriptionStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.PaymentFailedAt).IsRequired(false);
            entity.Property(x => x.PaymentGraceEndsAt).IsRequired(false);
            entity.Property(x => x.PaymentGraceReminderSentAt).IsRequired(false);
            entity.Property(x => x.SuspendedAt).IsRequired(false);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<UsagePeriod>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.PeriodKey }).IsUnique();
            entity.Property(x => x.PeriodKey).HasMaxLength(220);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany(x => x.UsagePeriods)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RewriteAttempt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.UserId, x.DeletedAt, x.CreatedAt });
            entity.HasIndex(x => new { x.Status, x.ExpiresAt });
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120);
            entity.Property(x => x.RequestHash).HasMaxLength(128);
            entity.Property(x => x.RequestJson).IsRequired(false);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.ErrorCode).HasMaxLength(120);
            entity.Property(x => x.ErrorMessage).HasMaxLength(512);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany(x => x.RewriteAttempts)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UsageReservation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.RewriteAttemptId).IsUnique();
            entity.HasIndex(x => x.RewriteCreditId);
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.HasIndex(x => new { x.Status, x.ExpiresAt });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany(x => x.UsageReservations)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.UsagePeriod)
                .WithMany(x => x.Reservations)
                .HasForeignKey(x => x.UsagePeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RewriteAttempt)
                .WithOne(x => x.Reservation)
                .HasForeignKey<UsageReservation>(x => x.RewriteAttemptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RewriteCredit)
                .WithMany()
                .HasForeignKey(x => x.RewriteCreditId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StripeEvent>(entity =>
        {
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventId).HasMaxLength(160);
            entity.Property(x => x.Type).HasMaxLength(160);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.LastError).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasIndex(x => new { x.Status, x.LockedUntil });
        });

        modelBuilder.Entity<StripeInvoice>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.Property(x => x.Id).HasMaxLength(160);
            entity.Property(x => x.SubscriptionId).HasMaxLength(160);
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.Currency).HasMaxLength(12);
            entity.Property(x => x.HostedInvoiceUrl).HasMaxLength(2048);
            entity.Property(x => x.InvoicePdf).HasMaxLength(2048);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StripeReconciliationRun>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CompletedAt);
            entity.HasIndex(x => new { x.WindowStart, x.WindowEnd });
            entity.Property(x => x.ReportJson).IsRequired();
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
            entity.HasIndex(x => new { x.Status, x.LockedUntil });
            entity.Property(x => x.MessageType).HasMaxLength(160);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.LockedBy).HasMaxLength(160);
            entity.Property(x => x.CorrelationId).HasMaxLength(160);
            entity.Property(x => x.LastError).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        // ----- Reconciled Postgres-only models (Workstream E schema landing) -----
        // Faithful EF port of the 11 Prisma models that previously lived only in Neon.
        // userId references resolve to AppUser.Id (canonical Azure SQL user). String
        // PKs in Prisma are mapped to Guid to match the existing EF schema convention.

        modelBuilder.Entity<RewriteCredit>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.ExpiresAt });
            entity.HasIndex(x => x.StripeEventId).IsUnique().HasFilter("[StripeEventId] IS NOT NULL");
            entity.HasIndex(x => x.StripePaymentIntentId).HasFilter("[StripePaymentIntentId] IS NOT NULL");
            entity.Property(x => x.Source).HasMaxLength(60);
            entity.Property(x => x.StripeEventId).HasMaxLength(160);
            entity.Property(x => x.StripePaymentIntentId).HasMaxLength(160);
            entity.Property(x => x.StripeReceiptUrl).HasMaxLength(2048);
            entity.Property(x => x.StripeSku).HasMaxLength(120);
            entity.Property(x => x.StripeCurrency).HasMaxLength(12);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Referral>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.RefereeId).IsUnique();
            entity.HasIndex(x => new { x.ReferrerId, x.CreditedAt });
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.SignupIpHash).HasMaxLength(128);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.Referrer)
                .WithMany()
                .HasForeignKey(x => x.ReferrerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LearningRun>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.StartedAt);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.PromotionDecision);
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.PromotionDecision).HasMaxLength(60);
            entity.Property(x => x.ValidationStatus).HasMaxLength(60);
            entity.Property(x => x.DigestPath).HasMaxLength(500);
            entity.Property(x => x.ErrorMessage).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<LearningFinding>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Scenario);
            entity.HasIndex(x => x.CommonTone);
            entity.HasIndex(x => x.PrimaryDiagnosisTag);
            entity.HasIndex(x => x.FailureType);
            entity.HasIndex(x => x.Severity);
            entity.HasIndex(x => x.PromotionRecommendation);
            entity.HasIndex(x => x.RunId);
            entity.Property(x => x.Scenario).HasMaxLength(200);
            entity.Property(x => x.CommonTone).HasMaxLength(120);
            entity.Property(x => x.PrimaryDiagnosisTag).HasMaxLength(120);
            entity.Property(x => x.FailureType).HasMaxLength(80);
            entity.Property(x => x.Severity).HasMaxLength(40);
            entity.Property(x => x.PromotionRecommendation).HasMaxLength(60);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.Run)
                .WithMany(x => x.Findings)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StrategyCandidate>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Scenario);
            entity.HasIndex(x => x.PatchTarget);
            entity.HasIndex(x => x.RiskLevel);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.FindingId);
            entity.Property(x => x.Title).HasMaxLength(300);
            entity.Property(x => x.Scenario).HasMaxLength(200);
            entity.Property(x => x.PatchTarget).HasMaxLength(200);
            entity.Property(x => x.PatchAction).HasMaxLength(60);
            entity.Property(x => x.RiskLevel).HasMaxLength(40);
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.RequiredRegressionTest).HasMaxLength(300);
            entity.Property(x => x.RequiredEval).HasMaxLength(300);
            entity.Property(x => x.LinkedCommitHash).HasMaxLength(80);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.Finding)
                .WithMany(x => x.Candidates)
                .HasForeignKey(x => x.FindingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RewriteLearningSample>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Scenario);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.Scenario).HasMaxLength(200);
            entity.Property(x => x.TonePreset).HasMaxLength(80);
            entity.Property(x => x.Label).HasMaxLength(60);
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.ErrorCode).HasMaxLength(120);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RewriteCostLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.RequestId).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Scenario);
            entity.HasIndex(x => new { x.StrategyVersion, x.CreatedAt });
            entity.HasIndex(x => x.TotalEstimatedCostUsd);
            entity.Property(x => x.RequestId).HasMaxLength(120);
            entity.Property(x => x.StrategyVersion).HasMaxLength(80);
            entity.Property(x => x.Scenario).HasMaxLength(200);
            entity.Property(x => x.TonePreset).HasMaxLength(80);
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.ErrorCode).HasMaxLength(120);
            entity.Property(x => x.OpenAiCostUsd).HasPrecision(18, 6);
            entity.Property(x => x.SaplingCostUsd).HasPrecision(18, 6);
            entity.Property(x => x.TotalEstimatedCostUsd).HasPrecision(18, 6);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RewriteCanaryRollback>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CanaryStrategyVersion, x.CreatedAt });
            entity.HasIndex(x => x.Scenario);
            entity.HasIndex(x => x.State);
            entity.HasIndex(x => x.ResolvedAt);
            entity.Property(x => x.CanaryStrategyVersion).HasMaxLength(80);
            entity.Property(x => x.ControlStrategyVersion).HasMaxLength(80);
            entity.Property(x => x.Scenario).HasMaxLength(200);
            entity.Property(x => x.State).HasMaxLength(40);
            entity.Property(x => x.AdminEmailStatus).HasMaxLength(40);
            entity.Property(x => x.GithubIssueStatus).HasMaxLength(40);
            entity.Property(x => x.GithubIssueUrl).HasMaxLength(500);
            entity.Property(x => x.LastError).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<RewriteProviderCall>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CostLogId);
            entity.HasIndex(x => x.Provider);
            entity.HasIndex(x => x.Role);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Provider).HasMaxLength(60);
            entity.Property(x => x.Role).HasMaxLength(60);
            entity.Property(x => x.Model).HasMaxLength(120);
            entity.Property(x => x.ErrorCode).HasMaxLength(120);
            entity.Property(x => x.EstimatedCostUsd).HasPrecision(18, 6);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.CostLog)
                .WithMany(x => x.ProviderCalls)
                .HasForeignKey(x => x.CostLogId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.KeyHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasIndex(x => x.PlanTier);
            entity.Property(x => x.KeyHash).HasMaxLength(200);
            entity.Property(x => x.Last4).HasMaxLength(4).IsRequired(false);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.PlanTier).HasMaxLength(40);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKeyUsage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ApiKeyId, x.CreatedAt });
            entity.HasIndex(x => x.Endpoint);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.RequestId).HasMaxLength(120);
            entity.Property(x => x.Endpoint).HasMaxLength(200);
            entity.Property(x => x.CostUsdEstimate).HasPrecision(18, 6);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.ApiKey)
                .WithMany(x => x.ApiKeyUsages)
                .HasForeignKey(x => x.ApiKeyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminAuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.AdminExternalAuthUserId, x.CreatedAt });
            entity.HasIndex(x => new { x.TargetUserId, x.CreatedAt });
            entity.HasIndex(x => new { x.Action, x.CreatedAt });
            entity.Property(x => x.AdminExternalAuthUserId).HasMaxLength(160);
            entity.Property(x => x.AdminEmail).HasMaxLength(320);
            entity.Property(x => x.Action).HasMaxLength(120);
        });

        modelBuilder.Entity<PromoCode>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.ToTable(x =>
            {
                x.HasCheckConstraint("CK_PromoCodes_CreditsGranted_Positive", "[CreditsGranted] > 0");
                x.HasCheckConstraint("CK_PromoCodes_GrantTtlDays_Positive", "[GrantTtlDays] > 0");
                x.HasCheckConstraint("CK_PromoCodes_MaxRedemptionsPerUser_Minimum", "[MaxRedemptionsPerUser] >= 1");
                x.HasCheckConstraint(
                    "CK_PromoCodes_MaxRedemptionsGlobal_PositiveOrNull",
                    "[MaxRedemptionsGlobal] IS NULL OR [MaxRedemptionsGlobal] > 0");
                x.HasCheckConstraint("CK_PromoCodes_ValidUntil_After_ValidFrom", "[ValidUntil] > [ValidFrom]");
                x.HasCheckConstraint("CK_PromoCodes_RedemptionCount_NonNegative", "[RedemptionCount] >= 0");
            });
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(40);
            entity.Property(x => x.DisplayCode).HasMaxLength(40);
            entity.Property(x => x.Description).HasMaxLength(200);
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<PromoCodeRedemption>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PromoCodeId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.PromoCodeId);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.RewriteCreditId);
            entity.HasIndex(x => x.RedeemIpHash);
            entity.HasIndex(x => x.RedeemedAt);
            entity.Property(x => x.CodeSnapshot).HasMaxLength(40);
            entity.Property(x => x.RedeemIpHash).HasMaxLength(128);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.PromoCode)
                .WithMany(x => x.Redemptions)
                .HasForeignKey(x => x.PromoCodeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BillingSupportRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasIndex(x => x.RelatedPaymentIntentId)
                .HasFilter("[RelatedPaymentIntentId] IS NOT NULL");
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.RelatedPaymentIntentId).HasMaxLength(160);
            entity.Property(x => x.Message).HasMaxLength(2000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany(x => x.BillingSupportRequests)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void StampConsentForAddedRewriteAttempts()
    {
        var firstAttemptByUser = GetFirstAddedRewriteAttemptByUser();
        if (firstAttemptByUser.Count == 0)
        {
            return;
        }

        var trackedUserIds = new HashSet<Guid>();
        foreach (var userEntry in ChangeTracker.Entries<AppUser>()
            .Where(x => firstAttemptByUser.ContainsKey(x.Entity.Id)))
        {
            trackedUserIds.Add(userEntry.Entity.Id);
            if (userEntry.Entity.ConsentAcceptedAt is null)
            {
                StampConsent(userEntry.Entity, firstAttemptByUser[userEntry.Entity.Id]);
            }
        }

        var missingUserIds = firstAttemptByUser.Keys
            .Where(x => !trackedUserIds.Contains(x))
            .ToArray();

        if (missingUserIds.Length == 0)
        {
            return;
        }

        foreach (var user in AppUsers.Where(x => missingUserIds.Contains(x.Id) && x.ConsentAcceptedAt == null).ToList())
        {
            StampConsent(user, firstAttemptByUser[user.Id]);
        }
    }

    private async Task StampConsentForAddedRewriteAttemptsAsync(CancellationToken cancellationToken)
    {
        var firstAttemptByUser = GetFirstAddedRewriteAttemptByUser();
        if (firstAttemptByUser.Count == 0)
        {
            return;
        }

        var trackedUserIds = new HashSet<Guid>();
        foreach (var userEntry in ChangeTracker.Entries<AppUser>()
            .Where(x => firstAttemptByUser.ContainsKey(x.Entity.Id)))
        {
            trackedUserIds.Add(userEntry.Entity.Id);
            if (userEntry.Entity.ConsentAcceptedAt is null)
            {
                StampConsent(userEntry.Entity, firstAttemptByUser[userEntry.Entity.Id]);
            }
        }

        var missingUserIds = firstAttemptByUser.Keys
            .Where(x => !trackedUserIds.Contains(x))
            .ToArray();

        if (missingUserIds.Length == 0)
        {
            return;
        }

        var users = await AppUsers
            .Where(x => missingUserIds.Contains(x.Id) && x.ConsentAcceptedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            StampConsent(user, firstAttemptByUser[user.Id]);
        }
    }

    private Dictionary<Guid, DateTimeOffset> GetFirstAddedRewriteAttemptByUser() =>
        ChangeTracker.Entries<RewriteAttempt>()
            .Where(x => x.State == EntityState.Added)
            .GroupBy(x => x.Entity.UserId)
            .ToDictionary(
                x => x.Key,
                x => x.Min(entry => entry.Entity.CreatedAt));

    private static void StampConsent(AppUser user, DateTimeOffset consentAcceptedAt)
    {
        user.ConsentAcceptedAt = consentAcceptedAt;
        user.UpdatedAt = consentAcceptedAt;
        user.RowVersion = Guid.NewGuid();
    }
}
