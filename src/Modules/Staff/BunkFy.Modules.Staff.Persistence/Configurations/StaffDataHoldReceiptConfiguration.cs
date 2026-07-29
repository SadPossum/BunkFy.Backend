namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffDataHoldReceiptConfiguration
    : IEntityTypeConfiguration<StaffDataHoldReceipt>
{
    public void Configure(
        EntityTypeBuilder<StaffDataHoldReceipt> builder)
    {
        builder.ToTable("staff_data_hold_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_data_hold_receipts_action",
                "\"Action\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_staff_data_hold_receipts_versions",
                "\"SelectedStaffVersion\" >= 1 AND " +
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
            .HasMaxLength(StaffDataHold.ReasonCodeMaxLength)
            .IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(200)
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
            receipt.StaffMemberId,
            receipt.CompletedAtUtc,
            receipt.Id
        });
        builder.HasOne<StaffDataHold>()
            .WithMany()
            .HasPrincipalKey(hold => new
            {
                hold.ScopeId,
                hold.Id
            })
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.HoldId
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
