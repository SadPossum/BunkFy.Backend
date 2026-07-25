namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestAnonymisationRestoreReceiptConfiguration
    : IEntityTypeConfiguration<GuestAnonymisationRestoreReceipt>
{
    public void Configure(
        EntityTypeBuilder<GuestAnonymisationRestoreReceipt> builder)
    {
        builder.ToTable(
            "guest_anonymisation_restore_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_guest_anonymisation_restore_receipts_contract",
                    $"\"ContractVersion\" = {GuestAnonymisationRestoreReceipt.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_guest_anonymisation_restore_receipts_identity",
                    "\"LedgerEntryId\" = \"Id\"");
                table.HasCheckConstraint(
                    "CK_guest_anonymisation_restore_receipts_versions",
                    "\"OwnerReceiptContractVersion\" >= 1 AND " +
                    "\"ResultingGuestVersion\" >= 1 AND " +
                    "\"TombstoneRevision\" >= 1");
                table.HasCheckConstraint(
                    "CK_guest_anonymisation_restore_receipts_digests",
                    $"char_length(\"OwnerReceiptSha256\") = {GuestAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"CanonicalSha256\") = {GuestAnonymisationReceipt.Sha256Length}");
            });

        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).ValueGeneratedNever();
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.Id
        });
        builder.Property(receipt => receipt.OwnerReceiptSha256)
            .HasMaxLength(GuestAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(GuestAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.GuestId,
            receipt.LedgerEntryId
        }).IsUnique();
        builder.HasOne<GuestAnonymisationTombstone>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.GuestId
            })
            .HasPrincipalKey(tombstone => new
            {
                tombstone.ScopeId,
                tombstone.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
