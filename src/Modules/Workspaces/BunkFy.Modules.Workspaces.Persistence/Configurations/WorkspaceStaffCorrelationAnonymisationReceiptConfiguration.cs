namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    WorkspaceStaffCorrelationAnonymisationReceiptConfiguration
    : IEntityTypeConfiguration<
        WorkspaceStaffCorrelationAnonymisationReceipt>
{
    public void Configure(
        EntityTypeBuilder<
            WorkspaceStaffCorrelationAnonymisationReceipt> builder)
    {
        builder.ToTable(
            "staff_correlation_anonymisation_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_receipt_contract",
                    $"\"ContractVersion\" = " +
                    $"{WorkspaceStaffCorrelationAnonymisationReceipt.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_receipt_revisions",
                    "\"ApprovalRevision\" > 0 AND " +
                    "\"OperationRevision\" > \"ApprovalRevision\"");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_receipt_versions",
                    "\"SelectedStaffVersion\" > 0 AND " +
                    "\"SelectedAnchorVersion\" > 0 AND " +
                    "\"ResultingAnchorVersion\" = " +
                    "\"SelectedAnchorVersion\" + 1");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_receipt_counts",
                    "\"OnboardingRecordsScrubbed\" >= 0 AND " +
                    "\"AccessProcessRecordsScrubbed\" > 0 AND " +
                    "\"AccessPlanRecordsScrubbed\" >= 0");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_receipt_outcome",
                    "\"Disposition\" = 1 AND \"Reason\" = 1");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_receipt_hashes",
                    $"char_length(\"ApprovalEvidenceSha256\") = " +
                    $"{WorkspaceStaffCorrelationAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"StateBindingSha256\") = " +
                    $"{WorkspaceStaffCorrelationAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"ResultingStateSha256\") = " +
                    $"{WorkspaceStaffCorrelationAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"CanonicalSha256\") = " +
                    $"{WorkspaceStaffCorrelationAnonymisationReceipt.Sha256Length}");
            });
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id)
            .ValueGeneratedNever();
        builder.HasAlternateKey(
            receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.ContractVersion)
            .IsRequired();
        builder.Property(receipt => receipt.Disposition)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(receipt => receipt.Reason)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(
                WorkspaceStaffAccessProcess.ActorIdMaxLength)
            .IsRequired();
        ConfigureSha(
            builder.Property(
                receipt => receipt.ApprovalEvidenceSha256));
        ConfigureSha(
            builder.Property(
                receipt => receipt.StateBindingSha256));
        ConfigureSha(
            builder.Property(
                receipt => receipt.ResultingStateSha256));
        ConfigureSha(
            builder.Property(
                receipt => receipt.CanonicalSha256));
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.AnchorProcessId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.StaffMemberId,
            receipt.SelectedStaffVersion
        }).IsUnique();
        builder.Ignore(receipt => receipt.DomainEvents);
    }

    private static void ConfigureSha(
        PropertyBuilder<string> property) =>
        property
            .HasMaxLength(
                WorkspaceStaffCorrelationAnonymisationReceipt
                    .Sha256Length)
            .IsFixedLength()
            .IsRequired();
}
