namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestDataHoldReceiptConfiguration
    : IEntityTypeConfiguration<GuestDataHoldReceipt>
{
    public void Configure(EntityTypeBuilder<GuestDataHoldReceipt> builder)
    {
        builder.ToTable("data_hold_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_guest_data_hold_receipts_versions",
                "\"SelectedGuestVersion\" >= 1 AND " +
                "((\"Action\" = 1 AND \"ResultingHoldVersion\" = 1) OR " +
                "(\"Action\" = 2 AND \"ResultingHoldVersion\" >= 2))");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(receipt => receipt.Action).HasConversion<int>().IsRequired();
        builder.Property(receipt => receipt.ReasonCode)
            .HasMaxLength(GuestDataHold.ReasonCodeMaxLength)
            .IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(GuestProfile.ActorIdMaxLength)
            .IsRequired();
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.IdempotencyKey })
            .IsUnique();
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
            receipt.GuestId,
            receipt.HoldId,
            receipt.CompletedAtUtc
        });
        builder.HasOne<GuestDataHold>()
            .WithMany()
            .HasPrincipalKey(hold => new { hold.ScopeId, hold.Id })
            .HasForeignKey(receipt => new { receipt.ScopeId, receipt.HoldId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
