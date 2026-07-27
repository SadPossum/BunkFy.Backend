namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionRetentionExecutionConfiguration
    : IEntityTypeConfiguration<IngestionRetentionExecution>
{
    public void Configure(EntityTypeBuilder<IngestionRetentionExecution> builder)
    {
        builder.ToTable("retention_executions", table =>
        {
            table.HasCheckConstraint(
                "CK_ingestion_retention_executions_versions",
                "\"ExecutionPolicyVersion\" >= 1 AND \"Attempt\" >= 1 AND " +
                "\"Version\" >= 1");
            table.HasCheckConstraint(
                "CK_ingestion_retention_executions_time",
                "\"DeadlineUtc\" > \"StartedAtUtc\"");
            table.HasCheckConstraint(
                "CK_ingestion_retention_executions_state",
                $"(\"State\" = {(int)IngestionRetentionExecutionState.Running} AND " +
                "\"CompletedAtUtc\" IS NULL AND \"RemainingCount\" IS NULL AND " +
                "\"OutcomeCode\" IS NULL AND \"HoldReviewDueAtUtc\" IS NULL) OR " +
                $"(\"State\" IN ({(int)IngestionRetentionExecutionState.Completed}, " +
                $"{(int)IngestionRetentionExecutionState.Blocked}) AND " +
                "\"CompletedAtUtc\" BETWEEN \"StartedAtUtc\" AND \"DeadlineUtc\" AND " +
                "\"RemainingCount\" >= 0 AND length(trim(\"OutcomeCode\")) > 0 AND " +
                $"((\"State\" = {(int)IngestionRetentionExecutionState.Blocked} AND " +
                "\"HoldReviewDueAtUtc\" IS NOT NULL) OR " +
                $"(\"State\" = {(int)IngestionRetentionExecutionState.Completed} AND " +
                "\"HoldReviewDueAtUtc\" IS NULL)))");
        });
        builder.HasKey(execution => execution.Id);
        builder.HasAlternateKey(execution => new
        {
            execution.ScopeId,
            execution.Id
        });
        builder.Property(execution => execution.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(execution => execution.DataClassKey)
            .HasMaxLength(IngestionRetentionExecution.DataClassKeyMaxLength)
            .IsRequired();
        builder.Property(execution => execution.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(execution => execution.OutcomeCode)
            .HasMaxLength(IngestionRetentionExecution.OutcomeCodeMaxLength);
        builder.Property(execution => execution.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(execution => new
        {
            execution.ScopeId,
            execution.DataClassKey,
            execution.ExecutionPolicyVersion,
            execution.StartedAtUtc
        });
        builder.Ignore(execution => execution.DomainEvents);
    }
}
