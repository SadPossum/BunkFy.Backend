namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class TenantTerminationExportArtifactConfiguration
    : IEntityTypeConfiguration<TenantTerminationExportArtifact>
{
    public void Configure(
        EntityTypeBuilder<TenantTerminationExportArtifact> builder)
    {
        builder.ToTable("tenant_termination_export_artifacts", table =>
        {
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_artifact_state",
                $"\"State\" BETWEEN " +
                $"{(int)TenantTerminationExportArtifactState.Requested} AND " +
                (int)TenantTerminationExportArtifactState.Deleted);
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_artifact_coordinates",
                "\"ApprovalRevision\" >= 1 AND " +
                "\"FreezeOperationRevision\" >= 1 AND " +
                "\"ExportOperationRevision\" > \"FreezeOperationRevision\" AND " +
                "\"ExpectedFragmentCount\" BETWEEN 1 AND 100 AND " +
                "char_length(\"FrozenRevisionSha256\") = 64 AND " +
                "\"FrozenRevisionSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "char_length(\"PolicyEvidenceSha256\") = 64 AND " +
                "\"PolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "char_length(\"FragmentSetSha256\") = 64 AND " +
                "\"FragmentSetSha256\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_artifact_timestamps",
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
                "CK_data_rights_tenant_termination_export_artifact_generation",
                "(\"GenerationRunId\" IS NULL AND " +
                "\"GenerationAttempt\" IS NULL AND " +
                "\"GenerationStartedAtUtc\" IS NULL) OR " +
                "(\"GenerationRunId\" IS NOT NULL AND " +
                "\"GenerationAttempt\" > 0 AND " +
                "\"GenerationStartedAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_artifact_fragments",
                "(\"FragmentCount\" IS NULL AND \"RecordCount\" IS NULL) OR " +
                "(\"FragmentCount\" = \"ExpectedFragmentCount\" AND " +
                $"\"RecordCount\" BETWEEN 0 AND " +
                TenantTerminationExportArtifact.MaximumRecordCount + ")");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_artifact_storage",
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
                "CK_data_rights_tenant_termination_export_artifact_lifecycle",
                $"(\"State\" = {(int)TenantTerminationExportArtifactState.Requested} AND " +
                "\"GenerationRunId\" IS NULL AND \"FailureCode\" IS NULL AND " +
                "\"FragmentCount\" IS NULL AND \"StorageKey\" IS NULL AND " +
                "\"DeletionRunId\" IS NULL AND " +
                "\"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = {(int)TenantTerminationExportArtifactState.Generating} AND " +
                "\"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NULL AND " +
                "\"FragmentCount\" IS NULL AND \"StorageKey\" IS NULL AND " +
                "\"DeletionRunId\" IS NULL AND " +
                "\"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = {(int)TenantTerminationExportArtifactState.Available} AND " +
                "\"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NULL AND " +
                "\"FragmentCount\" IS NOT NULL AND \"StorageKey\" IS NOT NULL AND " +
                "\"DeletionRunId\" IS NULL AND " +
                "\"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = {(int)TenantTerminationExportArtifactState.Failed} AND " +
                "\"GenerationRunId\" IS NOT NULL AND \"FailureCode\" IS NOT NULL AND " +
                "\"FragmentCount\" IS NULL AND \"StorageKey\" IS NULL AND " +
                "\"DeletionRunId\" IS NULL AND " +
                "\"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = {(int)TenantTerminationExportArtifactState.Expired} AND " +
                "\"FailureCode\" IS NULL AND \"DeletionRunId\" IS NULL AND " +
                "\"DeletionStartedAtUtc\" IS NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = {(int)TenantTerminationExportArtifactState.Deleting} AND " +
                "\"FailureCode\" IS NULL AND \"DeletionRunId\" IS NOT NULL AND " +
                "\"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = {(int)TenantTerminationExportArtifactState.Deleted} AND " +
                "\"FailureCode\" IS NULL AND \"DeletionRunId\" IS NOT NULL AND " +
                "\"DeletionStartedAtUtc\" IS NOT NULL AND \"DeletedAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_artifact_failure",
                $"(\"State\" = {(int)TenantTerminationExportArtifactState.Failed} AND " +
                "\"FailureCode\" IS NOT NULL) OR " +
                $"(\"State\" <> {(int)TenantTerminationExportArtifactState.Failed} AND " +
                "\"FailureCode\" IS NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_export_artifact_version",
                "\"Version\" >= 1");
        });

        builder.HasKey(artifact => artifact.Id);
        builder.HasAlternateKey(artifact => new
        {
            artifact.ScopeId,
            artifact.Id
        });
        builder.Property(artifact => artifact.Id).ValueGeneratedNever();
        builder.Property(artifact => artifact.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(artifact => artifact.FrozenRevisionSha256)
            .HasMaxLength(TenantTerminationExportArtifact.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(artifact => artifact.PolicyEvidenceSha256)
            .HasMaxLength(TenantTerminationExportArtifact.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(artifact => artifact.FragmentSetSha256)
            .HasMaxLength(TenantTerminationExportArtifact.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(artifact => artifact.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(artifact => artifact.FailureCode)
            .HasMaxLength(TenantTerminationExportArtifact.FailureCodeMaxLength);
        builder.Property(artifact => artifact.StorageKey)
            .HasMaxLength(TenantTerminationExportArtifact.StorageKeyMaxLength);
        builder.Property(artifact => artifact.PlaintextSha256)
            .HasMaxLength(TenantTerminationExportArtifact.Sha256Length)
            .IsFixedLength();
        builder.Property(artifact => artifact.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(artifact => new
        {
            artifact.ScopeId,
            artifact.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(artifact => new
        {
            artifact.ScopeId,
            artifact.ProcessId,
            artifact.ExportOperationRevision
        }).IsUnique();
        builder.HasIndex(artifact => new
        {
            artifact.ScopeId,
            artifact.State,
            artifact.ExpiresAtUtc,
            artifact.Id
        });
        builder.HasOne<TenantTerminationProcess>()
            .WithMany()
            .HasForeignKey(artifact => new
            {
                artifact.ScopeId,
                artifact.ProcessId,
                artifact.CaseId,
                artifact.ApprovalRevision,
                artifact.TerminationEpoch,
                artifact.PolicyEvidenceSha256
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
        builder.Ignore(artifact => artifact.DomainEvents);
    }
}
