namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using BunkFy.Modules.Inventory.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    InventoryAllocationAnonymisationRestoreReceiptConfiguration
    : IEntityTypeConfiguration<
        InventoryAllocationAnonymisationRestoreReceipt>
{
    public void Configure(
        EntityTypeBuilder<
            InventoryAllocationAnonymisationRestoreReceipt> builder)
    {
        builder.ToTable(
            "allocation_anonymisation_restore_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_restore_receipts_contract",
                    $"\"ContractVersion\" = {InventoryAllocationAnonymisationRestoreReceipt.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_restore_receipts_identity",
                    "\"LedgerEntryId\" = \"Id\"");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_restore_receipts_coordinates",
                    "\"TenantSequence\" >= 1 AND " +
                    "\"OwnerReceiptContractVersion\" >= 1 AND " +
                    "\"ResultingAllocationVersion\" >= 1 AND " +
                    "\"TombstoneRevision\" >= 1");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_restore_receipts_times",
                    "\"ReplayedAtUtc\" >= " +
                    "\"OriginallyCompletedAtUtc\"");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_restore_receipts_digests",
                    $"char_length(\"LedgerEntrySha256\") = {InventoryAllocationAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"OwnerReceiptSha256\") = {InventoryAllocationAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"CanonicalSha256\") = {InventoryAllocationAnonymisationReceipt.Sha256Length}");
            });

        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id)
            .ValueGeneratedNever();
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.Id
        });
        builder.Property(receipt => receipt.LedgerEntrySha256)
            .HasMaxLength(
                InventoryAllocationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.OwnerReceiptSha256)
            .HasMaxLength(
                InventoryAllocationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(
                InventoryAllocationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.PropertyId,
            receipt.AllocationId,
            receipt.LedgerEntryId
        }).IsUnique();
        builder.HasOne<
                InventoryAllocationAnonymisationTombstone>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.AllocationId
            })
            .HasPrincipalKey(tombstone => new
            {
                tombstone.ScopeId,
                tombstone.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
