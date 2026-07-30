namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    WorkspaceStaffOnboardingCorrectionReceiptConfiguration
    : IEntityTypeConfiguration<
        WorkspaceStaffOnboardingCorrectionReceipt>
{
    public void Configure(
        EntityTypeBuilder<WorkspaceStaffOnboardingCorrectionReceipt> builder)
    {
        builder.ToTable(
            "staff_onboarding_correction_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_workspaces_staff_onboarding_correction_receipts_contract",
                    $"\"ContractVersion\" = " +
                    $"{WorkspaceStaffOnboardingCorrectionReceipt.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_workspaces_staff_onboarding_correction_receipts_approval",
                    "\"ApprovalRevision\" >= 1");
                table.HasCheckConstraint(
                    "CK_workspaces_staff_onboarding_correction_receipts_versions",
                    "\"SelectedRecordVersion\" >= 1 AND " +
                    "\"CurrentRecordVersion\" = \"SelectedRecordVersion\" + 1");
                table.HasCheckConstraint(
                    "CK_workspaces_staff_onboarding_correction_receipts_fields",
                    $"\"ChangedFieldsMask\" BETWEEN 1 AND " +
                    $"{WorkspaceStaffOnboardingCorrectionReceipt.AllChangedFieldsMask}");
                table.HasCheckConstraint(
                    "CK_workspaces_staff_onboarding_correction_receipts_digest",
                    $"char_length(\"RequestSha256\") = " +
                    $"{WorkspaceStaffOnboardingCorrectionReceipt.DigestLength}");
            });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(
            receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.ContractVersion).IsRequired();
        builder.Property(receipt => receipt.RequestSha256)
            .HasMaxLength(
                WorkspaceStaffOnboardingCorrectionReceipt.DigestLength)
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
            receipt.ApplicantEventId
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
            receipt.ApplicationId,
            receipt.CompletedAtUtc,
            receipt.Id
        });
        builder.Ignore(receipt => receipt.ChangedFields);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
