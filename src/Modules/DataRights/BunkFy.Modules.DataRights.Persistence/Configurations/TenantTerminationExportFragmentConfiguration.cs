namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class TenantTerminationExportFragmentConfiguration
    : IEntityTypeConfiguration<TenantTerminationExportFragment>
{
    public void Configure(
        EntityTypeBuilder<TenantTerminationExportFragment> builder)
    {
        builder.ToTable("tenant_termination_export_fragments", table =>
        {
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_fragment_state",
                $"\"State\" BETWEEN " +
                $"{(int)TenantTerminationExportFragmentState.Requested} AND " +
                (int)TenantTerminationExportFragmentState.Deleted);
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_fragment_coordinates",
                "\"ApprovalRevision\" >= 1 AND " +
                "\"FreezeOperationRevision\" >= 1 AND " +
                "\"ExportOperationRevision\" > \"FreezeOperationRevision\" AND " +
                "\"OwnerContractVersion\" >= 1 AND " +
                "\"CatalogVersion\" >= 1 AND " +
                "length(trim(\"OwnerKey\")) > 0 AND " +
                "\"OwnerKey\" ~ '^[a-z0-9][a-z0-9._-]{0,99}$' AND " +
                "char_length(\"CatalogSha256\") = 64 AND " +
                "\"CatalogSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "char_length(\"FrozenRevisionSha256\") = 64 AND " +
                "\"FrozenRevisionSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "char_length(\"PolicyEvidenceSha256\") = 64 AND " +
                "\"PolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_fragment_timestamps",
                "\"ExpiresAtUtc\" > \"RequestedAtUtc\" AND " +
                "(\"GenerationStartedAtUtc\" IS NULL OR " +
                "(\"GenerationStartedAtUtc\" >= \"RequestedAtUtc\" AND " +
                "\"GenerationStartedAtUtc\" < \"ExpiresAtUtc\")) AND " +
                "(\"AvailableAtUtc\" IS NULL OR " +
                "(\"GenerationStartedAtUtc\" IS NOT NULL AND " +
                "\"AvailableAtUtc\" >= \"GenerationStartedAtUtc\" AND " +
                "\"AvailableAtUtc\" < \"ExpiresAtUtc\")) AND " +
                "(\"DeletionStartedAtUtc\" IS NULL OR " +
                "\"DeletionStartedAtUtc\" >= \"ExpiresAtUtc\") AND " +
                "(\"DeletedAtUtc\" IS NULL OR " +
                "(\"DeletionStartedAtUtc\" IS NOT NULL AND " +
                "\"DeletedAtUtc\" >= \"DeletionStartedAtUtc\"))");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_fragment_generation",
                "(\"GenerationRunId\" IS NULL AND " +
                "\"GenerationAttempt\" IS NULL AND " +
                "\"GenerationStartedAtUtc\" IS NULL) OR " +
                "(\"GenerationRunId\" IS NOT NULL AND " +
                "\"GenerationAttempt\" > 0 AND " +
                "\"GenerationStartedAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_fragment_proof",
                "(\"RecordCount\" IS NULL AND " +
                "\"SelectedProofRevision\" IS NULL AND " +
                "\"ResultingProofRevision\" IS NULL AND " +
                "\"ResultCode\" IS NULL) OR " +
                $"(\"RecordCount\" BETWEEN 0 AND " +
                TenantTerminationExportFragment.MaximumRecordCount +
                " AND \"SelectedProofRevision\" >= 0 AND " +
                "\"ResultingProofRevision\" = \"SelectedProofRevision\" AND " +
                "\"ResultCode\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_fragment_storage",
                "(\"StorageKey\" IS NULL AND " +
                "\"EncryptedByteLength\" IS NULL AND " +
                "\"PlaintextSha256\" IS NULL AND " +
                "\"EncryptionKeyVersion\" IS NULL AND " +
                "\"FormatVersion\" IS NULL AND " +
                "\"AvailableAtUtc\" IS NULL) OR " +
                "(\"StorageKey\" IS NOT NULL AND " +
                "\"EncryptedByteLength\" > 0 AND " +
                "char_length(\"PlaintextSha256\") = 64 AND " +
                "\"PlaintextSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "\"EncryptionKeyVersion\" > 0 AND " +
                "\"FormatVersion\" > 0 AND " +
                "\"AvailableAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_fragment_lifecycle",
                $"(\"State\" = " +
                $"{(int)TenantTerminationExportFragmentState.Requested} AND " +
                "\"GenerationRunId\" IS NULL AND \"FailureCode\" IS NULL AND " +
                "\"RecordCount\" IS NULL AND \"StorageKey\" IS NULL AND " +
                "\"DeletionRunId\" IS NULL AND " +
                "\"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = " +
                $"{(int)TenantTerminationExportFragmentState.Generating} AND " +
                "\"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NULL AND " +
                "\"RecordCount\" IS NULL AND \"StorageKey\" IS NULL AND " +
                "\"DeletionRunId\" IS NULL AND " +
                "\"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = " +
                $"{(int)TenantTerminationExportFragmentState.Available} AND " +
                "\"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NULL AND " +
                "\"RecordCount\" IS NOT NULL AND \"StorageKey\" IS NOT NULL AND " +
                "\"DeletionRunId\" IS NULL AND " +
                "\"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = " +
                $"{(int)TenantTerminationExportFragmentState.Failed} AND " +
                "\"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NOT NULL AND " +
                "\"RecordCount\" IS NULL AND \"StorageKey\" IS NULL AND " +
                "\"DeletionRunId\" IS NULL AND " +
                "\"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = " +
                $"{(int)TenantTerminationExportFragmentState.Expired} AND " +
                "\"FailureCode\" IS NULL AND \"DeletionRunId\" IS NULL AND " +
                "\"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = " +
                $"{(int)TenantTerminationExportFragmentState.Deleting} AND " +
                "\"FailureCode\" IS NULL AND \"DeletionRunId\" IS NOT NULL AND " +
                "\"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = " +
                $"{(int)TenantTerminationExportFragmentState.Deleted} AND " +
                "\"FailureCode\" IS NULL AND \"DeletionRunId\" IS NOT NULL AND " +
                "\"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_fragment_failure",
                $"(\"State\" = " +
                $"{(int)TenantTerminationExportFragmentState.Failed} AND " +
                "\"FailureCode\" IS NOT NULL) OR " +
                $"(\"State\" <> " +
                $"{(int)TenantTerminationExportFragmentState.Failed} AND " +
                "\"FailureCode\" IS NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_fragment_version",
                "\"Version\" >= 1");
        });

        builder.HasKey(fragment => fragment.Id);
        builder.HasAlternateKey(fragment => new
        {
            fragment.ScopeId,
            fragment.Id
        });
        builder.Property(fragment => fragment.Id).ValueGeneratedNever();
        builder.Property(fragment => fragment.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(fragment => fragment.OwnerKey)
            .HasMaxLength(TenantTerminationExportFragment.OwnerKeyMaxLength)
            .IsRequired();
        builder.Property(fragment => fragment.CatalogSha256)
            .HasMaxLength(TenantTerminationExportFragment.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(fragment => fragment.FrozenRevisionSha256)
            .HasMaxLength(TenantTerminationExportFragment.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(fragment => fragment.PolicyEvidenceSha256)
            .HasMaxLength(TenantTerminationExportFragment.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(fragment => fragment.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(fragment => fragment.FailureCode)
            .HasMaxLength(TenantTerminationExportFragment.FailureCodeMaxLength);
        builder.Property(fragment => fragment.ResultCode)
            .HasMaxLength(TenantTerminationExportFragment.ResultCodeMaxLength);
        builder.Property(fragment => fragment.StorageKey)
            .HasMaxLength(TenantTerminationExportFragment.StorageKeyMaxLength);
        builder.Property(fragment => fragment.PlaintextSha256)
            .HasMaxLength(TenantTerminationExportFragment.Sha256Length)
            .IsFixedLength();
        builder.Property(fragment => fragment.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(fragment => new
        {
            fragment.ScopeId,
            fragment.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(fragment => new
        {
            fragment.ScopeId,
            fragment.ProcessId,
            fragment.ExportOperationRevision,
            fragment.OwnerKey
        }).IsUnique();
        builder.HasIndex(fragment => new
        {
            fragment.ScopeId,
            fragment.ProcessId,
            fragment.ExportOperationRevision,
            fragment.State,
            fragment.OwnerKey,
            fragment.Id
        });
        builder.HasIndex(fragment => new
        {
            fragment.ScopeId,
            fragment.State,
            fragment.ExpiresAtUtc,
            fragment.Id
        });
        builder.HasOne<TenantTerminationOwnerWorkItem>()
            .WithOne()
            .HasForeignKey<TenantTerminationExportFragment>(fragment => new
            {
                fragment.ScopeId,
                fragment.Id
            })
            .HasPrincipalKey<TenantTerminationOwnerWorkItem>(workItem => new
            {
                workItem.ScopeId,
                workItem.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(fragment => fragment.DomainEvents);
    }
}
