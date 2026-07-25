namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationDataHoldReceiptConfiguration
    : IEntityTypeConfiguration<ReservationDataHoldReceipt>
{
    public void Configure(EntityTypeBuilder<ReservationDataHoldReceipt> builder)
    {
        builder.ToTable("reservation_data_hold_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_reservation_data_hold_receipts_versions",
                "\"SelectedReservationVersion\" >= 1 AND " +
                "\"SelectedDetailsRevision\" >= 1 AND " +
                "((\"Action\" = 1 AND \"ResultingHoldVersion\" = 1) OR " +
                "(\"Action\" = 2 AND \"ResultingHoldVersion\" = 2))");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.Id
        });
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.Action)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(receipt => receipt.ReasonCode)
            .HasMaxLength(ReservationDataHold.ReasonCodeMaxLength)
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.HoldId,
            receipt.Action
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.PropertyId,
            receipt.ReservationId,
            receipt.HoldId,
            receipt.CompletedAtUtc
        });
        builder.HasOne<ReservationDataHold>()
            .WithMany()
            .HasPrincipalKey(hold => new { hold.ScopeId, hold.Id })
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.HoldId
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
