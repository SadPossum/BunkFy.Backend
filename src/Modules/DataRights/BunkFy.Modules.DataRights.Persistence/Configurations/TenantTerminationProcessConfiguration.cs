namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class TenantTerminationProcessConfiguration
    : IEntityTypeConfiguration<TenantTerminationProcess>
{
    public void Configure(EntityTypeBuilder<TenantTerminationProcess> builder)
    {
        builder.ToTable("tenant_termination_processes", table =>
        {
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_phase",
                "\"Phase\" BETWEEN 1 AND 6");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_status",
                "\"Status\" BETWEEN 1 AND 6");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_completion",
                "(\"Phase\" = 5 AND \"Status\" = 5) OR " +
                "(\"Phase\" = 6 AND \"Status\" IN (1, 2, 3, 4, 6)) OR " +
                "(\"Phase\" BETWEEN 1 AND 4 AND \"Status\" BETWEEN 1 AND 4)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_approval",
                "\"ApprovalRevision\" >= 1 AND " +
                "char_length(\"PolicyEvidenceSha256\") = 64 AND " +
                "\"PolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "length(trim(\"ApprovedBy\")) > 0 AND " +
                "\"ApprovedAtUtc\" <= \"CreatedAtUtc\"");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_operation",
                "\"OperationRevision\" >= \"ApprovalRevision\" AND " +
                "((\"Status\" = 1 AND " +
                "\"OperationRevision\" >= \"ApprovalRevision\") OR " +
                "(\"Status\" BETWEEN 2 AND 6 AND " +
                "\"OperationRevision\" > \"ApprovalRevision\"))");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_outcome",
                "(\"Status\" = 3 AND \"OutcomeCode\" IS NOT NULL AND " +
                "(\"HoldReviewAtUtc\" IS NULL OR " +
                "\"HoldReviewAtUtc\" >= \"LastChangedAtUtc\")) OR " +
                "(\"Status\" = 4 AND \"OutcomeCode\" IS NOT NULL AND " +
                "\"HoldReviewAtUtc\" IS NULL) OR " +
                "(\"Status\" IN (1, 2, 5, 6) AND \"OutcomeCode\" IS NULL AND " +
                "\"HoldReviewAtUtc\" IS NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_outcome_code",
                "\"OutcomeCode\" IS NULL OR " +
                "\"OutcomeCode\" ~ '^[a-z0-9][a-z0-9._-]{0,199}$'");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_attribution",
                "length(trim(\"CreatedBy\")) > 0 AND " +
                "length(trim(\"LastChangedBy\")) > 0");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_timestamps",
                "\"LastChangedAtUtc\" >= \"CreatedAtUtc\"");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_freeze_checkpoint",
                "((\"Phase\" = 1 AND " +
                "\"FreezeOperationRevision\" IS NULL AND " +
                "\"WorkspaceFenceRevision\" IS NULL AND " +
                "\"FrozenRevisionSha256\" IS NULL AND " +
                "\"FrozenBy\" IS NULL AND " +
                "\"FrozenAtUtc\" IS NULL) OR " +
                "(\"Phase\" BETWEEN 2 AND 6 AND " +
                "\"FreezeOperationRevision\" > \"ApprovalRevision\" AND " +
                "\"FreezeOperationRevision\" <= \"OperationRevision\" AND " +
                "\"WorkspaceFenceRevision\" >= 1 AND " +
                "char_length(\"FrozenRevisionSha256\") = 64 AND " +
                "\"FrozenRevisionSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "length(trim(\"FrozenBy\")) > 0 AND " +
                "\"FrozenAtUtc\" >= \"CreatedAtUtc\" AND " +
                "\"FrozenAtUtc\" <= \"LastChangedAtUtc\"))");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_export_confirmation",
                "\"ExportConfirmationRevision\" >= 0 AND " +
                "((\"ExportConfirmedOperationRevision\" IS NULL AND " +
                "\"ExportArtifactId\" IS NULL AND " +
                "\"ExportArtifactVersion\" IS NULL AND " +
                "\"ExportFrozenRevisionSha256\" IS NULL AND " +
                "\"ExportFragmentSetSha256\" IS NULL AND " +
                "\"ExportConfirmedBy\" IS NULL AND " +
                "\"ExportConfirmedAtUtc\" IS NULL) OR " +
                "(\"ExportConfirmationRevision\" >= 1 AND " +
                "\"ExportConfirmedOperationRevision\" >= 1 AND " +
                "\"ExportConfirmedOperationRevision\" <= \"OperationRevision\" AND " +
                "\"ExportArtifactId\" IS NOT NULL AND " +
                "\"ExportArtifactVersion\" >= 1 AND " +
                "char_length(\"ExportFrozenRevisionSha256\") = 64 AND " +
                "\"ExportFrozenRevisionSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "\"ExportFrozenRevisionSha256\" = " +
                "\"FrozenRevisionSha256\" AND " +
                "char_length(\"ExportFragmentSetSha256\") = 64 AND " +
                "\"ExportFragmentSetSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "length(trim(\"ExportConfirmedBy\")) > 0 AND " +
                "\"ExportConfirmedAtUtc\" >= \"CreatedAtUtc\" AND " +
                "\"ExportConfirmedAtUtc\" <= \"LastChangedAtUtc\")) AND " +
                "((\"ExportRequested\" = FALSE AND " +
                "\"ExportConfirmationRevision\" = 0 AND " +
                "\"ExportConfirmedOperationRevision\" IS NULL) OR " +
                "\"ExportRequested\" = TRUE) AND " +
                "(\"ExportRequested\" = FALSE OR \"Phase\" IN (1, 2, 6) OR " +
                "\"ExportConfirmedOperationRevision\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_destroy_checkpoint",
                "((\"Phase\" IN (1, 2, 3, 6) AND " +
                "\"DestroyCompletedOperationRevision\" IS NULL AND " +
                "\"DestroyedAtUtc\" IS NULL) OR " +
                "(\"Phase\" IN (4, 5) AND " +
                "\"DestroyCompletedOperationRevision\" > " +
                "\"ApprovalRevision\" AND " +
                "\"DestroyCompletedOperationRevision\" <= " +
                "\"OperationRevision\" AND " +
                "\"DestroyedAtUtc\" >= \"CreatedAtUtc\" AND " +
                "\"DestroyedAtUtc\" <= \"LastChangedAtUtc\"))");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_verification_confirmation",
                "\"VerificationConfirmationRevision\" >= 0 AND " +
                "((\"VerificationConfirmedOperationRevision\" IS NULL AND " +
                "\"TerminalReceiptId\" IS NULL AND " +
                "\"TerminalReceiptVersion\" IS NULL AND " +
                "\"VerificationOwnerProofSetSha256\" IS NULL AND " +
                "\"VerificationConfirmedBy\" IS NULL AND " +
                "\"VerificationConfirmedAtUtc\" IS NULL) OR " +
                "(\"Phase\" IN (4, 5) AND " +
                "\"VerificationConfirmationRevision\" >= 1 AND " +
                "\"VerificationConfirmedOperationRevision\" = " +
                "\"OperationRevision\" AND " +
                "\"VerificationConfirmedOperationRevision\" > " +
                "\"DestroyCompletedOperationRevision\" AND " +
                "\"TerminalReceiptId\" IS NOT NULL AND " +
                "\"TerminalReceiptVersion\" >= 1 AND " +
                "char_length(\"VerificationOwnerProofSetSha256\") = 64 AND " +
                "\"VerificationOwnerProofSetSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "length(trim(\"VerificationConfirmedBy\")) > 0 AND " +
                "\"VerificationConfirmedAtUtc\" >= \"DestroyedAtUtc\" AND " +
                "\"VerificationConfirmedAtUtc\" <= \"LastChangedAtUtc\")) AND " +
                "(\"Phase\" <> 5 OR " +
                "\"VerificationConfirmedOperationRevision\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_version",
                "\"Version\" >= 1");
        });

        builder.HasKey(process => process.Id);
        builder.HasAlternateKey(process => new { process.ScopeId, process.Id });
        builder.HasAlternateKey(process => new
        {
            process.ScopeId,
            process.Id,
            process.CaseId,
            process.ApprovalRevision,
            process.TerminationEpoch,
            process.PolicyEvidenceSha256
        });
        builder.Property(process => process.Id).ValueGeneratedNever();
        builder.Property(process => process.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(process => process.PolicyEvidenceSha256)
            .HasMaxLength(TenantTerminationProcess.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(process => process.ApprovedBy)
            .HasMaxLength(TenantTerminationProcess.ActorIdMaxLength)
            .IsRequired();
        builder.Property(process => process.Phase)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(process => process.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(process => process.OutcomeCode)
            .HasMaxLength(TenantTerminationProcess.OutcomeCodeMaxLength);
        builder.Property(process => process.FrozenRevisionSha256)
            .HasMaxLength(TenantTerminationProcess.Sha256Length)
            .IsFixedLength();
        builder.Property(process => process.FrozenBy)
            .HasMaxLength(TenantTerminationProcess.ActorIdMaxLength);
        builder.Property(process => process.ExportFrozenRevisionSha256)
            .HasMaxLength(TenantTerminationProcess.Sha256Length)
            .IsFixedLength();
        builder.Property(process => process.ExportFragmentSetSha256)
            .HasMaxLength(TenantTerminationProcess.Sha256Length)
            .IsFixedLength();
        builder.Property(process => process.ExportConfirmedBy)
            .HasMaxLength(TenantTerminationProcess.ActorIdMaxLength);
        builder.Property(process => process.VerificationOwnerProofSetSha256)
            .HasMaxLength(TenantTerminationProcess.Sha256Length)
            .IsFixedLength();
        builder.Property(process => process.VerificationConfirmedBy)
            .HasMaxLength(TenantTerminationProcess.ActorIdMaxLength);
        builder.Property(process => process.CreatedBy)
            .HasMaxLength(TenantTerminationProcess.ActorIdMaxLength)
            .IsRequired();
        builder.Property(process => process.LastChangedBy)
            .HasMaxLength(TenantTerminationProcess.ActorIdMaxLength)
            .IsRequired();
        builder.Property(process => process.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(process => new
        {
            process.ScopeId,
            process.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(process => new
        {
            process.ScopeId,
            process.CaseId
        }).IsUnique();
        builder.HasIndex(process => process.ScopeId)
            .IsUnique()
            .HasFilter("\"Status\" IN (1, 2, 3, 4)");
        builder.HasIndex(process => new
        {
            process.ScopeId,
            process.Status,
            process.Phase,
            process.LastChangedAtUtc,
            process.Id
        });
        builder.HasOne<DataRightsCase>()
            .WithOne()
            .HasForeignKey<TenantTerminationProcess>(process => new
            {
                process.ScopeId,
                process.CaseId
            })
            .HasPrincipalKey<DataRightsCase>(dataRightsCase => new
            {
                dataRightsCase.ScopeId,
                dataRightsCase.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TenantTerminationTerminalReceipt>()
            .WithOne()
            .HasForeignKey<TenantTerminationProcess>(process => new
            {
                process.ScopeId,
                process.Id,
                process.TerminalReceiptId,
                process.TerminalReceiptVersion
            })
            .HasPrincipalKey<TenantTerminationTerminalReceipt>(receipt => new
            {
                receipt.ScopeId,
                receipt.ProcessId,
                receipt.Id,
                receipt.Version
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(process => process.FrozenExportOwners, owners =>
        {
            owners.ToTable("tenant_termination_frozen_export_owners", table =>
            {
                table.HasCheckConstraint(
                    "CK_data_rights_tenant_termination_frozen_owner_ordinal",
                    $"\"Ordinal\" BETWEEN 1 AND " +
                    TenantTerminationProcess.MaximumFrozenOwners);
                table.HasCheckConstraint(
                    "CK_data_rights_tenant_termination_frozen_owner_key",
                    "length(trim(\"OwnerKey\")) > 0 AND " +
                    "\"OwnerKey\" ~ '^[a-z0-9][a-z0-9._-]{0,99}$'");
                table.HasCheckConstraint(
                    "CK_data_rights_tenant_termination_frozen_owner_catalog",
                    "\"ContractVersion\" >= 1 AND " +
                    "\"CatalogVersion\" >= 1 AND " +
                    "char_length(\"CatalogSha256\") = 64 AND " +
                    "\"CatalogSha256\" ~ '^[0-9a-f]{64}$'");
            });
            owners.WithOwner().HasForeignKey("ProcessId");
            owners.Property<Guid>("ProcessId");
            owners.HasKey(
                "ProcessId",
                nameof(TenantTerminationFrozenOwner.Ordinal));
            owners.Property(owner => owner.Ordinal).ValueGeneratedNever();
            owners.Property(owner => owner.OwnerKey)
                .HasMaxLength(TenantTerminationFrozenOwner.OwnerKeyMaxLength)
                .IsRequired();
            owners.Property(owner => owner.ContractVersion).IsRequired();
            owners.Property(owner => owner.CatalogVersion).IsRequired();
            owners.Property(owner => owner.CatalogSha256)
                .HasMaxLength(TenantTerminationProcess.Sha256Length)
                .IsFixedLength()
                .IsRequired();
            owners.HasIndex("ProcessId", nameof(TenantTerminationFrozenOwner.OwnerKey))
                .IsUnique();
        });
        builder.Navigation(process => process.FrozenExportOwners)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(process => process.DomainEvents);
    }
}
