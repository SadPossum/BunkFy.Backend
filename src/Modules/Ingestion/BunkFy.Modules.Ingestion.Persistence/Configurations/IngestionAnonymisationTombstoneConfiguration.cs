namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionAnonymisationTombstoneConfiguration
    : IEntityTypeConfiguration<IngestionAnonymisationTombstone>
{
    public void Configure(
        EntityTypeBuilder<IngestionAnonymisationTombstone> builder)
    {
        builder.ToTable("anonymisation_tombstones", table =>
        {
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_tombstones_contract",
                $"\"ContractVersion\" = " +
                IngestionAnonymisationTombstone.CurrentContractVersion);
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_tombstones_revision",
                "\"Revision\" >= 1");
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_tombstones_state",
                "\"State\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_tombstones_versions",
                "\"SelectedSourceLinkVersion\" >= 1 AND " +
                "\"ResultingSourceLinkVersion\" = " +
                "\"SelectedSourceLinkVersion\" + 1");
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_tombstones_counts",
                "\"GraphRecordCount\" >= 1 AND " +
                "\"FingerprintCount\" >= 1 AND " +
                "\"RawPayloadCount\" >= 0");
        });
        builder.HasKey(tombstone => tombstone.Id);
        builder.HasAlternateKey(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.Id
        });
        builder.Property(tombstone => tombstone.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(tombstone => tombstone.Revision)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(tombstone => tombstone.OwnerReceiptSha256)
            .HasMaxLength(IngestionAnonymisationTombstone.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(tombstone => tombstone.LedgerEntrySha256)
            .HasMaxLength(IngestionAnonymisationTombstone.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.LedgerEntryId
        }).IsUnique();
        builder.HasIndex(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.OwnerReceiptId
        }).IsUnique();
        builder.HasIndex(tombstone => tombstone.State);
        builder.HasIndex(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.ConnectionId,
            tombstone.State
        });
    }
}
