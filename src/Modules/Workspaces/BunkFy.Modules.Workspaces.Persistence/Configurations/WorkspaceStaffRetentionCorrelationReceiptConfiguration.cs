namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    WorkspaceStaffRetentionCorrelationReceiptConfiguration
    : IEntityTypeConfiguration<
        WorkspaceStaffRetentionCorrelationReceipt>
{
    public void Configure(
        EntityTypeBuilder<
            WorkspaceStaffRetentionCorrelationReceipt> builder)
    {
        builder.ToTable(
            "staff_retention_correlation_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_staff_retention_correlation_receipt_contract",
                    $"\"ContractVersion\" = " +
                    $"{WorkspaceStaffRetentionCorrelationReceipt.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_staff_retention_correlation_receipt_version",
                    "\"SelectedStaffVersion\" > 0");
                table.HasCheckConstraint(
                    "CK_staff_retention_correlation_receipt_counts",
                    "\"OnboardingRecordsScrubbed\" >= 0 AND " +
                    "\"AccessProcessRecordsScrubbed\" >= 0 AND " +
                    "\"AccessPlanRecordsScrubbed\" >= 0");
                table.HasCheckConstraint(
                    "CK_staff_retention_correlation_receipt_hash",
                    $"char_length(\"CanonicalSha256\") = " +
                    $"{WorkspaceStaffRetentionCorrelationReceipt.Sha256Length}");
            });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(
            receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.ContractVersion)
            .IsRequired();
        builder.Property(receipt => receipt.ExecutionId)
            .IsRequired();
        builder.Property(receipt => receipt.StaffMemberId)
            .IsRequired();
        builder.Property(receipt => receipt.SelectedStaffVersion)
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(
                WorkspaceStaffRetentionCorrelationReceipt
                    .Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.StaffMemberId,
            receipt.SelectedStaffVersion
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ExecutionId,
            receipt.Id
        });
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
