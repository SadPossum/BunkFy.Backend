namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    WorkspaceStaffOnboardingProcessingRestrictionReceiptConfiguration
    : IEntityTypeConfiguration<
        WorkspaceStaffOnboardingProcessingRestrictionReceipt>
{
    public void Configure(
        EntityTypeBuilder<
            WorkspaceStaffOnboardingProcessingRestrictionReceipt> builder)
    {
        builder.ToTable(
            "staff_onboarding_processing_restriction_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ws_onboarding_restriction_receipt_versions",
                    "\"ApprovalRevision\" >= 1 AND " +
                    "\"SelectedOnboardingVersion\" >= 1 AND " +
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
            .HasMaxLength(WorkspaceStaffOnboardingRules.ActorIdMaxLength)
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ApplicationId,
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
