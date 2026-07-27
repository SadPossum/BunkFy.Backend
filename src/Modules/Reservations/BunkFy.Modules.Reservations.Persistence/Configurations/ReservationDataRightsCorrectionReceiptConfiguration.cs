namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationDataRightsCorrectionReceiptConfiguration
    : IEntityTypeConfiguration<ReservationDataRightsCorrectionReceipt>
{
    public void Configure(EntityTypeBuilder<ReservationDataRightsCorrectionReceipt> builder)
    {
        builder.ToTable("reservation_data_rights_correction_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_reservation_data_rights_correction_receipts_contract",
                $"\"ContractVersion\" = {ReservationDataRightsCorrectionReceipt.CurrentContractVersion}");
            table.HasCheckConstraint(
                "CK_reservation_data_rights_correction_receipts_approval",
                "\"ApprovalRevision\" >= 1");
            table.HasCheckConstraint(
                "CK_reservation_data_rights_correction_receipts_versions",
                "\"SelectedRecordVersion\" >= 1 AND " +
                "\"CurrentRecordVersion\" = \"SelectedRecordVersion\" + 1 AND " +
                "\"SelectedDetailsRevision\" >= 0 AND " +
                "\"CurrentDetailsRevision\" = \"SelectedDetailsRevision\" + 1");
            table.HasCheckConstraint(
                "CK_reservation_data_rights_correction_receipts_fields",
                $"\"ChangedFieldsMask\" BETWEEN 1 AND " +
                $"{ReservationDataRightsCorrectionReceipt.AllChangedFieldsMask}");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(receipt => receipt.ContractVersion).IsRequired();
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.IdempotencyKey }).IsUnique();
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.EventId }).IsUnique();
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.CompletionEventId }).IsUnique();
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.DetailsChangeEventId }).IsUnique();
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
            receipt.ReservationId,
            receipt.CurrentRecordVersion
        });
        builder.Ignore(receipt => receipt.ChangedFields);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
