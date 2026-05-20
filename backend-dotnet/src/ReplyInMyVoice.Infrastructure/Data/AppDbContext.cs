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
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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
            entity.HasIndex(x => new { x.Status, x.ExpiresAt });
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120);
            entity.Property(x => x.RequestHash).HasMaxLength(128);
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
    }
}
