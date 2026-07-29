namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffProcessingRestrictionReceiptConfiguration
    : IEntityTypeConfiguration<StaffProcessingRestrictionReceipt>
{
    public void Configure(
        EntityTypeBuilder<StaffProcessingRestrictionReceipt> builder)
    {
        builder.ToTable(
            "staff_processing_restriction_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_staff_processing_restriction_receipts_versions",
                    "\"ApprovalRevision\" >= 1 AND " +
                    "\"SelectedStaffVersion\" >= 1 AND " +
                    "\"ResultingProjectionRevision\" >= 1 AND " +
                    "((\"Action\" = 1 AND " +
                    "\"ResultingRestrictionVersion\" = 1 AND " +
                    "\"EffectiveRestricted\") OR " +
                    "(\"Action\" = 2 AND " +
                    "\"ResultingRestrictionVersion\" >= 2))");
            });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(
            receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.Action)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(StaffMember.ActorIdMaxLength)
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.StaffMemberId,
            receipt.CompletedAtUtc
        });
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.CaseId,
            receipt.ApprovalRevision
        });
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
