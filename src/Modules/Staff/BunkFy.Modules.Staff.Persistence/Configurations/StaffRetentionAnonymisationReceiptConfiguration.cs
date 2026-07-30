namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffRetentionAnonymisationReceiptConfiguration
    : IEntityTypeConfiguration<StaffRetentionAnonymisationReceipt>
{
    public void Configure(
        EntityTypeBuilder<StaffRetentionAnonymisationReceipt> builder)
    {
        builder.ToTable(
            "staff_retention_anonymisation_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_staff_retention_receipts_contract",
                    $"\"ContractVersion\" = {StaffRetentionAnonymisationReceipt.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_staff_retention_receipts_actor",
                    $"\"ActorId\" = '{StaffRetentionAnonymisationReceipt.SystemActorId}'");
                table.HasCheckConstraint(
                    "CK_staff_retention_receipts_versions",
                    "\"SelectedStaffVersion\" >= 1 AND " +
                    "\"ResultingStaffVersion\" = \"SelectedStaffVersion\" + 1 AND " +
                    "\"SelectedOperationLockRevision\" >= 1 AND " +
                    "\"ResultingOperationLockRevision\" = \"SelectedOperationLockRevision\" + 1");
                table.HasCheckConstraint(
                    "CK_staff_retention_receipts_digests",
                    $"char_length(\"PolicyEvidenceSha256\") = {StaffRetentionAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"CanonicalSha256\") = {StaffRetentionAnonymisationReceipt.Sha256Length}");
                table.HasCheckConstraint(
                    "CK_staff_retention_receipts_deadline",
                    "\"RetentionDeadlineUtc\" >= \"DepartedAtUtc\" AND " +
                    "\"CompletedAtUtc\" >= \"RetentionDeadlineUtc\"");
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
        builder.Property(receipt => receipt.PolicyEvidenceSha256)
            .HasMaxLength(
                StaffRetentionAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(
                StaffRetentionAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(StaffMember.ActorIdMaxLength)
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.StaffMemberId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ExecutionId,
            receipt.StaffMemberId
        }).IsUnique();
        builder.HasOne<StaffRetentionExecution>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.ExecutionId
            })
            .HasPrincipalKey(execution => new
            {
                execution.ScopeId,
                execution.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StaffMember>()
            .WithOne()
            .HasForeignKey<StaffRetentionAnonymisationReceipt>(
                receipt => new
                {
                    receipt.ScopeId,
                    receipt.StaffMemberId
                })
            .HasPrincipalKey<StaffMember>(
                member => new
                {
                    member.ScopeId,
                    member.Id
                })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
