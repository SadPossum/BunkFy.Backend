namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffAnonymisationReceiptConfiguration
    : IEntityTypeConfiguration<StaffAnonymisationReceipt>
{
    public void Configure(
        EntityTypeBuilder<StaffAnonymisationReceipt> builder)
    {
        builder.ToTable("staff_anonymisation_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_anonymisation_receipts_contract",
                $"\"ContractVersion\" = " +
                $"{StaffAnonymisationReceipt.CurrentContractVersion}");
            table.HasCheckConstraint(
                "CK_staff_anonymisation_receipts_revisions",
                "\"ApprovalRevision\" >= 1 AND " +
                "\"OperationRevision\" > \"ApprovalRevision\"");
            table.HasCheckConstraint(
                "CK_staff_anonymisation_receipts_versions",
                "\"SelectedStaffVersion\" >= 1 AND " +
                "\"ResultingStaffVersion\" = " +
                "\"SelectedStaffVersion\" + 1");
            table.HasCheckConstraint(
                "CK_staff_anonymisation_receipts_lock_revisions",
                "\"SelectedOperationLockRevision\" >= 1 AND " +
                "\"ResultingOperationLockRevision\" = " +
                "\"SelectedOperationLockRevision\" + 1");
            table.HasCheckConstraint(
                "CK_staff_anonymisation_receipts_outcome",
                $"\"Disposition\" = " +
                $"{(int)StaffAnonymisationDisposition.Completed} AND " +
                $"\"Reason\" = " +
                $"{(int)StaffAnonymisationReason.ProfileAnonymised}");
            table.HasCheckConstraint(
                "CK_staff_anonymisation_receipts_digests",
                $"char_length(\"ApprovalEvidenceSha256\") = " +
                $"{StaffAnonymisationReceipt.Sha256Length} AND " +
                $"char_length(\"StateBindingsSha256\") = " +
                $"{StaffAnonymisationReceipt.Sha256Length} AND " +
                $"char_length(\"CanonicalSha256\") = " +
                $"{StaffAnonymisationReceipt.Sha256Length}");
            table.HasCheckConstraint(
                "CK_staff_anonymisation_receipts_actor",
                "length(trim(\"ActorId\")) > 0");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.Id
        });
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.CanonicalSha256
        });
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.Disposition)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(receipt => receipt.Reason)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(receipt => receipt.ApprovalEvidenceSha256)
            .HasMaxLength(StaffAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.StateBindingsSha256)
            .HasMaxLength(StaffAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(StaffAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
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
            receipt.CaseId,
            receipt.ApprovalRevision,
            receipt.OperationRevision,
            receipt.StaffMemberId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.StaffMemberId,
            receipt.ResultingStaffVersion
        }).IsUnique();
        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.StaffMemberId
            })
            .HasPrincipalKey(member => new
            {
                member.ScopeId,
                member.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
