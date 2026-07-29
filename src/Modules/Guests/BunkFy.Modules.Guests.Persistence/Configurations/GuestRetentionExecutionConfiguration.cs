namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestRetentionExecutionConfiguration
    : IEntityTypeConfiguration<GuestRetentionExecution>
{
    public void Configure(
        EntityTypeBuilder<GuestRetentionExecution> builder)
    {
        builder.ToTable("guest_retention_executions", table =>
        {
            table.HasCheckConstraint(
                "CK_guest_retention_executions_policy",
                "\"ExecutionPolicyVersion\" >= 1 AND \"Attempt\" >= 1 AND " +
                "\"DeadlineUtc\" > \"StartedAtUtc\"");
            table.HasCheckConstraint(
                "CK_guest_retention_executions_cursor",
                "\"StartingProjectionOrdinal\" >= 0");
            table.HasCheckConstraint(
                "CK_guest_retention_executions_key",
                "length(trim(\"DataClassKey\")) > 0");
            table.HasCheckConstraint(
                "CK_guest_retention_executions_version",
                "\"Version\" >= 1");
            table.HasCheckConstraint(
                "CK_guest_retention_executions_counts",
                "\"AffectedCount\" >= 0 AND " +
                "(\"ScannedCount\" IS NULL OR \"ScannedCount\" >= 0) AND " +
                "(\"RemainingCount\" IS NULL OR \"RemainingCount\" >= 0) AND " +
                "(\"ScannedCount\" IS NULL OR " +
                "\"AffectedCount\" <= \"ScannedCount\")");
            table.HasCheckConstraint(
                "CK_guest_retention_executions_state",
                $"(\"State\" = {(int)GuestRetentionExecutionState.Running} AND " +
                "\"CompletedAtUtc\" IS NULL AND \"ScannedCount\" IS NULL AND " +
                "\"RemainingCount\" IS NULL AND \"OutcomeCode\" IS NULL AND " +
                "\"HoldReviewDueAtUtc\" IS NULL) OR " +
                $"(\"State\" IN ({(int)GuestRetentionExecutionState.Completed}, " +
                $"{(int)GuestRetentionExecutionState.Blocked}, " +
                $"{(int)GuestRetentionExecutionState.Failed}) AND " +
                "\"CompletedAtUtc\" BETWEEN \"StartedAtUtc\" AND \"DeadlineUtc\" AND " +
                "\"ScannedCount\" IS NOT NULL AND \"RemainingCount\" IS NOT NULL AND " +
                "length(trim(\"OutcomeCode\")) > 0 AND " +
                $"((\"State\" = {(int)GuestRetentionExecutionState.Blocked} AND " +
                "\"HoldReviewDueAtUtc\" IS NOT NULL) OR " +
                $"(\"State\" <> {(int)GuestRetentionExecutionState.Blocked} AND " +
                "\"HoldReviewDueAtUtc\" IS NULL)))");
        });
        builder.HasKey(execution => execution.Id);
        builder.HasAlternateKey(execution => new
        {
            execution.ScopeId,
            execution.Id
        });
        builder.Property(execution => execution.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(execution => execution.DataClassKey)
            .HasMaxLength(GuestRetentionExecution.DataClassKeyMaxLength)
            .IsRequired();
        builder.Property(execution => execution.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(execution => execution.OutcomeCode)
            .HasMaxLength(GuestRetentionExecution.OutcomeCodeMaxLength);
        builder.Property(execution => execution.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(execution => new
        {
            execution.ScopeId,
            execution.DataClassKey,
            execution.CompletedAtUtc,
            execution.Id
        });
        builder.Ignore(execution => execution.DomainEvents);
    }
}
