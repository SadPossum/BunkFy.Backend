namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffDataRightsCorrectionReceiptConfiguration
    : IEntityTypeConfiguration<StaffDataRightsCorrectionReceipt>
{
    public void Configure(
        EntityTypeBuilder<StaffDataRightsCorrectionReceipt> builder)
    {
        builder.ToTable("data_rights_correction_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_data_rights_correction_receipts_contract",
                $"\"ContractVersion\" = " +
                $"{StaffDataRightsCorrectionReceipt.CurrentContractVersion}");
            table.HasCheckConstraint(
                "CK_staff_data_rights_correction_receipts_approval",
                "\"ApprovalRevision\" >= 1");
            table.HasCheckConstraint(
                "CK_staff_data_rights_correction_receipts_versions",
                "\"SelectedRecordVersion\" >= 1 AND " +
                "\"CurrentRecordVersion\" = \"SelectedRecordVersion\" + 1");
            table.HasCheckConstraint(
                "CK_staff_data_rights_correction_receipts_fields",
                $"\"ChangedFieldsMask\" BETWEEN 1 AND " +
                $"{StaffDataRightsCorrectionReceipt.AllChangedFieldsMask}");
            table.HasCheckConstraint(
                "CK_staff_data_rights_correction_receipts_digest",
                $"char_length(\"RequestSha256\") = " +
                $"{StaffDataRightsCorrectionReceipt.DigestLength}");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(receipt => receipt.ContractVersion).IsRequired();
        builder.Property(receipt => receipt.RequestSha256)
            .HasMaxLength(StaffDataRightsCorrectionReceipt.DigestLength)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ExecutionId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ProfileEventId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.CompletionEventId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.CaseId,
            receipt.ApprovalRevision
        });
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.StaffMemberId,
            receipt.CurrentRecordVersion
        });
        builder.Ignore(receipt => receipt.ChangedFields);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
