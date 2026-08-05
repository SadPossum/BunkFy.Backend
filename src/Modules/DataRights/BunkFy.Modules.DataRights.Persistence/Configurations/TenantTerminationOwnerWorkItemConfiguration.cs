namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class TenantTerminationOwnerWorkItemConfiguration
    : IEntityTypeConfiguration<TenantTerminationOwnerWorkItem>
{
    public void Configure(
        EntityTypeBuilder<TenantTerminationOwnerWorkItem> builder)
    {
        builder.ToTable("tenant_termination_owner_work_items", table =>
        {
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_owner_phase",
                "\"Phase\" BETWEEN 1 AND 5");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_owner_state",
                "\"State\" BETWEEN 1 AND 6");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_owner_coordinates",
                "\"ApprovalRevision\" >= 1 AND " +
                "\"OperationRevision\" >= 1 AND " +
                "\"OwnerContractVersion\" >= 1 AND " +
                "\"CatalogVersion\" >= 1 AND " +
                "length(trim(\"OwnerKey\")) > 0 AND " +
                "\"OwnerKey\" ~ '^[a-z0-9][a-z0-9._-]{0,99}$' AND " +
                "char_length(\"CatalogSha256\") = 64 AND " +
                "\"CatalogSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "char_length(\"PolicyEvidenceSha256\") = 64 AND " +
                "\"PolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_owner_attempt",
                "\"AttemptCount\" >= 0 AND \"LastTaskAttempt\" >= 0 AND " +
                "((\"State\" = 1 AND \"TaskRunId\" IS NULL) OR " +
                "(\"State\" BETWEEN 2 AND 6 AND \"TaskRunId\" IS NOT NULL " +
                "AND \"AttemptCount\" >= 1 AND \"LastTaskAttempt\" >= 1))");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_owner_result",
                "(\"State\" IN (1, 2) AND \"ResultCode\" IS NULL AND " +
                "\"AffectedCount\" IS NULL AND \"RetainedMinimumCount\" IS NULL AND " +
                "\"RemainingActiveCount\" IS NULL AND \"HoldReviewAtUtc\" IS NULL AND " +
                "\"SelectedProofRevision\" IS NULL AND " +
                "\"ResultingProofRevision\" IS NULL AND " +
                "\"ResultRecordedAtUtc\" IS NULL) OR " +
                "(\"State\" = 6 AND \"ResultCode\" IS NOT NULL AND " +
                "\"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND " +
                "\"RemainingActiveCount\" = 0 AND \"HoldReviewAtUtc\" IS NULL AND " +
                "\"SelectedProofRevision\" >= 0 AND " +
                "\"ResultingProofRevision\" >= \"SelectedProofRevision\" AND " +
                "\"ResultRecordedAtUtc\" IS NOT NULL) OR " +
                "(\"State\" = 4 AND \"ResultCode\" IS NOT NULL AND " +
                "\"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND " +
                "\"RemainingActiveCount\" >= 0 AND " +
                "\"HoldReviewAtUtc\" >= \"ResultRecordedAtUtc\" AND " +
                "\"SelectedProofRevision\" IS NULL AND " +
                "\"ResultingProofRevision\" IS NULL AND " +
                "\"ResultRecordedAtUtc\" IS NOT NULL) OR " +
                "(\"State\" IN (3, 5) AND \"ResultCode\" IS NOT NULL AND " +
                "\"AffectedCount\" >= 0 AND \"RetainedMinimumCount\" >= 0 AND " +
                "\"RemainingActiveCount\" >= 0 AND \"HoldReviewAtUtc\" IS NULL AND " +
                "\"SelectedProofRevision\" IS NULL AND " +
                "\"ResultingProofRevision\" IS NULL AND " +
                "\"ResultRecordedAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_owner_result_code",
                "\"ResultCode\" IS NULL OR " +
                "\"ResultCode\" ~ '^[a-z0-9][a-z0-9._-]{0,199}$'");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_owner_timestamps",
                "\"LastChangedAtUtc\" >= \"CreatedAtUtc\" AND " +
                "(\"LastAttemptAtUtc\" IS NULL OR " +
                "\"LastAttemptAtUtc\" BETWEEN \"CreatedAtUtc\" AND \"LastChangedAtUtc\") AND " +
                "(\"ResultRecordedAtUtc\" IS NULL OR " +
                "\"ResultRecordedAtUtc\" BETWEEN \"CreatedAtUtc\" AND \"LastChangedAtUtc\")");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_owner_version",
                "\"Version\" >= 1");
        });

        builder.HasKey(workItem => workItem.Id);
        builder.HasAlternateKey(workItem => new
        {
            workItem.ScopeId,
            workItem.Id
        });
        builder.Property(workItem => workItem.Id).ValueGeneratedNever();
        builder.Property(workItem => workItem.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(workItem => workItem.Phase)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(workItem => workItem.OwnerKey)
            .HasMaxLength(TenantTerminationOwnerWorkItem.OwnerKeyMaxLength)
            .IsRequired();
        builder.Property(workItem => workItem.CatalogSha256)
            .HasMaxLength(TenantTerminationOwnerWorkItem.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(workItem => workItem.PolicyEvidenceSha256)
            .HasMaxLength(TenantTerminationOwnerWorkItem.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(workItem => workItem.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(workItem => workItem.ResultCode)
            .HasMaxLength(TenantTerminationOwnerWorkItem.ResultCodeMaxLength);
        builder.Property(workItem => workItem.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(workItem => new
        {
            workItem.ScopeId,
            workItem.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(workItem => new
        {
            workItem.ScopeId,
            workItem.ProcessId,
            workItem.Phase,
            workItem.OwnerKey,
            workItem.OperationRevision
        }).IsUnique();
        builder.HasIndex(workItem => new
        {
            workItem.ScopeId,
            workItem.ProcessId,
            workItem.Phase,
            workItem.OperationRevision,
            workItem.State,
            workItem.Id
        });
        builder.HasOne<TenantTerminationProcess>()
            .WithMany()
            .HasForeignKey(workItem => new
            {
                workItem.ScopeId,
                workItem.ProcessId,
                workItem.CaseId,
                workItem.ApprovalRevision,
                workItem.TerminationEpoch,
                workItem.PolicyEvidenceSha256
            })
            .HasPrincipalKey(process => new
            {
                process.ScopeId,
                process.Id,
                process.CaseId,
                process.ApprovalRevision,
                process.TerminationEpoch,
                process.PolicyEvidenceSha256
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(workItem => workItem.DomainEvents);
    }
}
