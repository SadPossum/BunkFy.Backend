namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffAnonymisationRestoreReceiptConfiguration
    : IEntityTypeConfiguration<StaffAnonymisationRestoreReceipt>
{
    public void Configure(
        EntityTypeBuilder<StaffAnonymisationRestoreReceipt> builder)
    {
        builder.ToTable(
            "staff_anonymisation_restore_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_staff_anonymisation_restore_receipts_contract",
                    $"\"ContractVersion\" = " +
                    $"{StaffAnonymisationRestoreReceipt.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_staff_anonymisation_restore_receipts_identity",
                    "\"LedgerEntryId\" = \"Id\"");
                table.HasCheckConstraint(
                    "CK_staff_anonymisation_restore_receipts_versions",
                    "\"OwnerReceiptContractVersion\" >= 1 AND " +
                    "\"ResultingStaffVersion\" >= 1 AND " +
                    "\"TombstoneRevision\" >= 1");
                table.HasCheckConstraint(
                    "CK_staff_anonymisation_restore_receipts_digests",
                    $"char_length(\"OwnerReceiptSha256\") = " +
                    $"{StaffAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"CanonicalSha256\") = " +
                    $"{StaffAnonymisationReceipt.Sha256Length}");
            });

        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).ValueGeneratedNever();
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.Id
        });
        builder.Property(receipt => receipt.OwnerReceiptSha256)
            .HasMaxLength(StaffAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(StaffAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.StaffMemberId,
            receipt.LedgerEntryId
        }).IsUnique();
        builder.HasOne<StaffAnonymisationTombstone>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.StaffMemberId
            })
            .HasPrincipalKey(tombstone => new
            {
                tombstone.ScopeId,
                tombstone.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
