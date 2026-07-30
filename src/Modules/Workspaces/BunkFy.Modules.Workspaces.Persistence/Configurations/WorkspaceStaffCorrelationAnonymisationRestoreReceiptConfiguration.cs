namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    WorkspaceStaffCorrelationAnonymisationRestoreReceiptConfiguration
    : IEntityTypeConfiguration<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt>
{
    public void Configure(
        EntityTypeBuilder<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>
            builder)
    {
        builder.ToTable(
            "staff_correlation_anonymisation_restore_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_restore_contract",
                    $"\"ContractVersion\" = " +
                    $"{WorkspaceStaffCorrelationAnonymisationRestoreReceipt.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_restore_identity",
                    "\"LedgerEntryId\" = \"Id\"");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_restore_receipt",
                    "\"TenantSequence\" > 0 AND " +
                    "\"OwnerReceiptContractVersion\" > 0");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_restore_version",
                    "\"ResultingAnchorVersion\" > 1");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_restore_counts",
                    "\"OnboardingRecordsScrubbed\" >= 0 AND " +
                    "\"AccessProcessRecordsScrubbed\" > 0 AND " +
                    "\"AccessPlanRecordsScrubbed\" >= 0");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_restore_revision",
                    "\"TombstoneRevision\" > 0");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_restore_times",
                    "\"ReplayedAtUtc\" >= " +
                    "\"OriginallyCompletedAtUtc\"");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_restore_hashes",
                    $"char_length(\"LedgerEntrySha256\") = " +
                    $"{WorkspaceStaffCorrelationAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"OwnerReceiptSha256\") = " +
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
        ConfigureSha(
            builder.Property(
                receipt => receipt.LedgerEntrySha256));
        ConfigureSha(
            builder.Property(
                receipt => receipt.OwnerReceiptSha256));
        ConfigureSha(
            builder.Property(
                receipt => receipt.ResultingStateSha256));
        ConfigureSha(
            builder.Property(
                receipt => receipt.CanonicalSha256));
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.AnchorProcessId,
            receipt.LedgerEntryId
        }).IsUnique();
        builder.HasOne<
                WorkspaceStaffCorrelationAnonymisationTombstone>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.AnchorProcessId
            })
            .HasPrincipalKey(tombstone => new
            {
                tombstone.ScopeId,
                tombstone.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
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
