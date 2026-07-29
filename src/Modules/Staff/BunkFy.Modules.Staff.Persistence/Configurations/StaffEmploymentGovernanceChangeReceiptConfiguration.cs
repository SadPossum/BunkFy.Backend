namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffEmploymentGovernanceChangeReceiptConfiguration
    : IEntityTypeConfiguration<
        StaffEmploymentGovernanceChangeReceipt>
{
    public void Configure(
        EntityTypeBuilder<StaffEmploymentGovernanceChangeReceipt>
            builder)
    {
        builder.ToTable(
            "staff_employment_governance_change_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_staff_employment_governance_receipts_contract",
                    "\"GovernanceContractVersion\" = 1");
                table.HasCheckConstraint(
                    "CK_staff_employment_governance_receipts_versions",
                    "\"SelectedStaffVersion\" >= 1 AND " +
                    "\"PreviousGovernanceVersion\" >= 0 AND " +
                    "\"ResultingGovernanceVersion\" = " +
                    "\"PreviousGovernanceVersion\" + 1");
                table.HasCheckConstraint(
                    "CK_staff_employment_governance_receipts_digests",
                    "char_length(\"PolicyContentSha256\") = 64 AND " +
                    "char_length(\"AcknowledgementsSha256\") = 64 AND " +
                    "char_length(\"RequestSha256\") = 64 AND " +
                    "char_length(\"ReceiptSha256\") = 64");
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
        builder.Property(receipt => receipt.GovernanceContractVersion)
            .IsRequired();
        builder.Property(receipt => receipt.PolicyContentSha256)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(receipt => receipt.AcknowledgementsSha256)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(receipt => receipt.RequestSha256)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(receipt => receipt.ReceiptSha256)
            .HasMaxLength(64)
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
            receipt.StaffMemberId,
            receipt.CompletedAtUtc,
            receipt.Id
        });
    }
}
