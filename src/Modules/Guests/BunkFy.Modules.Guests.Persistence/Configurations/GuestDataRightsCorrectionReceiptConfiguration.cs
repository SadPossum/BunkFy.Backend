namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestDataRightsCorrectionReceiptConfiguration
    : IEntityTypeConfiguration<GuestDataRightsCorrectionReceipt>
{
    public void Configure(EntityTypeBuilder<GuestDataRightsCorrectionReceipt> builder)
    {
        builder.ToTable("guest_data_rights_correction_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_guest_data_rights_correction_receipts_approval_revision",
                "\"ApprovalRevision\" >= 1");
            table.HasCheckConstraint(
                "CK_guest_data_rights_correction_receipts_versions",
                "\"SelectedRecordVersion\" >= 1 AND " +
                "\"CurrentRecordVersion\" = \"SelectedRecordVersion\" + 1");
            table.HasCheckConstraint(
                "CK_guest_data_rights_correction_receipts_changed_fields",
                $"\"ChangedFieldsMask\" BETWEEN 1 AND {GuestDataRightsCorrectionReceipt.AllChangedFieldsMask}");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId).HasMaxLength(128).IsRequired();
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.IdempotencyKey }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.PropertyId,
            receipt.CaseId,
            receipt.ApprovalRevision
        });
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.GuestId,
            receipt.CurrentRecordVersion
        });
        builder.Ignore(receipt => receipt.ChangedFields);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
