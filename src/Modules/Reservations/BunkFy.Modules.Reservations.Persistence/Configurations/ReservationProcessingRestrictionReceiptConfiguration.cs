namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationProcessingRestrictionReceiptConfiguration
    : IEntityTypeConfiguration<ReservationProcessingRestrictionReceipt>
{
    public void Configure(
        EntityTypeBuilder<ReservationProcessingRestrictionReceipt> builder)
    {
        builder.ToTable("reservation_processing_restriction_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_reservation_processing_restriction_receipts_versions",
                "\"ApprovalRevision\" >= 1 AND " +
                "\"SelectedReservationVersion\" >= 1 AND " +
                "\"ContractVersion\" >= 1 AND " +
                "\"ResultingProjectionRevision\" >= 1 AND " +
                "((\"Action\" = 1 AND \"ResultingRestrictionVersion\" = 1 AND " +
                "\"EffectiveRestricted\") OR " +
                "(\"Action\" = 2 AND \"ResultingRestrictionVersion\" >= 2))");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.Action)
            .HasConversion<int>()
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.EventId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.PropertyId,
            receipt.ReservationId,
            receipt.CompletedAtUtc
        });
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.PropertyId,
            receipt.CaseId,
            receipt.ApprovalRevision
        });
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
