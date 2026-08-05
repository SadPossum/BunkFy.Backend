namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class TenantTerminationTerminalReceiptConfiguration
    : IEntityTypeConfiguration<TenantTerminationTerminalReceipt>
{
    public void Configure(
        EntityTypeBuilder<TenantTerminationTerminalReceipt> builder)
    {
        builder.ToTable("tenant_termination_terminal_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_terminal_receipt_coordinates",
                "\"ApprovalRevision\" >= 1 AND " +
                "\"DestroyOperationRevision\" > \"ApprovalRevision\" AND " +
                "\"VerificationOperationRevision\" > " +
                "\"DestroyOperationRevision\" AND " +
                "\"VerificationTaskRunId\" IS NOT NULL AND " +
                "\"VerificationTaskAttempt\" > 0 AND " +
                "char_length(\"PolicyEvidenceSha256\") = 64 AND " +
                "\"PolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "char_length(\"FrozenRevisionSha256\") = 64 AND " +
                "\"FrozenRevisionSha256\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_terminal_receipt_export",
                "(\"ExportRequested\" = FALSE AND " +
                "\"ExportArtifactId\" IS NULL AND " +
                "\"ExportArtifactVersion\" IS NULL AND " +
                "\"ExportFragmentSetSha256\" IS NULL) OR " +
                "(\"ExportRequested\" = TRUE AND " +
                "\"ExportArtifactId\" IS NOT NULL AND " +
                "\"ExportArtifactVersion\" >= 1 AND " +
                "char_length(\"ExportFragmentSetSha256\") = 64 AND " +
                "\"ExportFragmentSetSha256\" ~ '^[0-9a-f]{64}$')");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_terminal_receipt_owners",
                "\"OwnerCount\" BETWEEN 1 AND 64 AND " +
                "char_length(\"OwnerProofSetSha256\") = 64 AND " +
                "\"OwnerProofSetSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "length(trim(\"TerminalOwnerKey\")) > 0 AND " +
                "\"TerminalOwnerKey\" ~ " +
                "'^[a-z0-9][a-z0-9._-]{0,99}$' AND " +
                "\"TerminalOwnerSelectedProofRevision\" >= 0 AND " +
                "\"TerminalOwnerResultingProofRevision\" >= " +
                "\"TerminalOwnerSelectedProofRevision\"");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_terminal_receipt_replay",
                "\"ReplayCheckpointSequence\" > 0 AND " +
                "char_length(\"ReplayCheckpointRecordSha256\") = 64 AND " +
                "\"ReplayCheckpointRecordSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "\"ReplayIntegrityKeyVersion\" > 0 AND " +
                "char_length(\"ReplayCheckpointProofSha256\") = 64 AND " +
                "\"ReplayCheckpointProofSha256\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_terminal_receipt_seal",
                "length(trim(\"SealedBy\")) > 0 AND " +
                "\"SealedAtUtc\" >= \"ReplayCheckpointFlushedAtUtc\"");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_terminal_receipt_version",
                "\"Version\" = 1");
        });

        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.Id
        });
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.ProcessId,
            receipt.Id,
            receipt.Version
        });
        builder.Property(receipt => receipt.Id).ValueGeneratedNever();
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.PolicyEvidenceSha256)
            .HasMaxLength(TenantTerminationTerminalReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.FrozenRevisionSha256)
            .HasMaxLength(TenantTerminationTerminalReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.ExportFragmentSetSha256)
            .HasMaxLength(TenantTerminationTerminalReceipt.Sha256Length)
            .IsFixedLength();
        builder.Property(receipt => receipt.OwnerProofSetSha256)
            .HasMaxLength(TenantTerminationTerminalReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.TerminalOwnerKey)
            .HasMaxLength(TenantTerminationTerminalReceipt.OwnerKeyMaxLength)
            .IsRequired();
        builder.Property(receipt => receipt.ReplayCheckpointRecordSha256)
            .HasMaxLength(TenantTerminationTerminalReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.ReplayCheckpointProofSha256)
            .HasMaxLength(TenantTerminationTerminalReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.SealedBy)
            .HasMaxLength(TenantTerminationProcess.ActorIdMaxLength)
            .IsRequired();
        builder.Property(receipt => receipt.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ProcessId,
            receipt.VerificationOperationRevision
        }).IsUnique();
        builder.HasOne<TenantTerminationProcess>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.ProcessId,
                receipt.CaseId,
                receipt.ApprovalRevision,
                receipt.TerminationEpoch,
                receipt.PolicyEvidenceSha256
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
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
