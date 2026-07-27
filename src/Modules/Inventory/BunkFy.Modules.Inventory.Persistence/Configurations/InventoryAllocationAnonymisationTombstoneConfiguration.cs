namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using BunkFy.Modules.Inventory.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    InventoryAllocationAnonymisationTombstoneConfiguration
    : IEntityTypeConfiguration<
        InventoryAllocationAnonymisationTombstone>
{
    public void Configure(
        EntityTypeBuilder<
            InventoryAllocationAnonymisationTombstone> builder)
    {
        builder.ToTable(
            "allocation_anonymisation_tombstones",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_tombstones_contract",
                    $"\"ContractVersion\" = {InventoryAllocationAnonymisationTombstone.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_tombstones_revision",
                    "\"Revision\" >= 1");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_tombstones_receipt",
                    "\"OwnerReceiptContractVersion\" >= 1 AND " +
                    $"char_length(\"OwnerReceiptSha256\") = {InventoryAllocationAnonymisationReceipt.Sha256Length}");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_tombstones_version",
                    "\"ResultingAllocationVersion\" >= 1");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_tombstones_replay",
                    "(\"LedgerEntryId\" IS NULL AND " +
                    "\"LastReplayedAtUtc\" IS NULL) OR " +
                    "(\"LedgerEntryId\" IS NOT NULL AND " +
                    "\"LastReplayedAtUtc\" IS NOT NULL AND " +
                    "\"LastReplayedAtUtc\" >= \"CompletedAtUtc\")");
            });

        builder.HasKey(tombstone => tombstone.Id);
        builder.Property(tombstone => tombstone.Id)
            .ValueGeneratedNever();
        builder.HasAlternateKey(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.Id
        });
        builder.Property(tombstone => tombstone.Revision)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(tombstone => tombstone.OwnerReceiptSha256)
            .HasMaxLength(
                InventoryAllocationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.PropertyId,
            tombstone.CompletedAtUtc,
            tombstone.Id
        });
        builder.HasIndex(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.LedgerEntryId
        }).IsUnique();
        builder.Ignore(tombstone => tombstone.DomainEvents);
    }
}
