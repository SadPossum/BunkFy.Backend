namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestProcessingRestrictionReceiptConfiguration
    : IEntityTypeConfiguration<GuestProcessingRestrictionReceipt>
{
    public void Configure(EntityTypeBuilder<GuestProcessingRestrictionReceipt> builder)
    {
        builder.ToTable("guest_processing_restriction_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_guest_processing_restriction_receipts_versions",
                "\"ApprovalRevision\" >= 1 AND \"SelectedGuestVersion\" >= 1 AND " +
                "\"ResultingProjectionRevision\" >= 1 AND " +
                "((\"Action\" = 1 AND \"ResultingRestrictionVersion\" = 1 AND " +
                "\"EffectiveRestricted\") OR " +
                "(\"Action\" = 2 AND \"ResultingRestrictionVersion\" >= 2))");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(receipt => receipt.Action).HasConversion<int>().IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(GuestProfile.ActorIdMaxLength)
            .IsRequired();
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.IdempotencyKey }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.PropertyId,
            receipt.GuestId,
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
