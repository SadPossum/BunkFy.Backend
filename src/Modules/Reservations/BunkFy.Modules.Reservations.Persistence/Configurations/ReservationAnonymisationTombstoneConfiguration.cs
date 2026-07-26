namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationAnonymisationTombstoneConfiguration
    : IEntityTypeConfiguration<ReservationAnonymisationTombstone>
{
    public void Configure(
        EntityTypeBuilder<ReservationAnonymisationTombstone> builder)
    {
        builder.ToTable(
            "reservation_anonymisation_tombstones",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_reservation_anonymisation_tombstones_contract",
                    $"\"ContractVersion\" = {ReservationAnonymisationTombstone.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_reservation_anonymisation_tombstones_revision",
                    "\"Revision\" >= 1");
                table.HasCheckConstraint(
                    "CK_reservation_anonymisation_tombstones_receipt",
                    "\"OwnerReceiptContractVersion\" >= 1 AND " +
                    $"char_length(\"OwnerReceiptSha256\") = {ReservationAnonymisationReceipt.Sha256Length}");
                table.HasCheckConstraint(
                    "CK_reservation_anonymisation_tombstones_versions",
                    "\"ResultingReservationVersion\" >= 1 AND " +
                    "(\"ResultingDetailsRevision\" IS NULL OR " +
                    "\"ResultingDetailsRevision\" >= 1)");
                table.HasCheckConstraint(
                    "CK_reservation_anonymisation_tombstones_replay",
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
            .HasMaxLength(ReservationAnonymisationReceipt.Sha256Length)
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
