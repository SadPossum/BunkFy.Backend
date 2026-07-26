namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationAnonymisationRestoreReceiptConfiguration
    : IEntityTypeConfiguration<ReservationAnonymisationRestoreReceipt>
{
    public void Configure(
        EntityTypeBuilder<ReservationAnonymisationRestoreReceipt> builder)
    {
        builder.ToTable(
            "reservation_anonymisation_restore_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_reservation_anonymisation_restore_receipts_contract",
                    $"\"ContractVersion\" = {ReservationAnonymisationRestoreReceipt.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_reservation_anonymisation_restore_receipts_identity",
                    "\"LedgerEntryId\" = \"Id\"");
                table.HasCheckConstraint(
                    "CK_reservation_anonymisation_restore_receipts_coordinates",
                    "\"TenantSequence\" >= 1 AND " +
                    "\"OwnerReceiptContractVersion\" >= 1 AND " +
                    "\"ResultingReservationVersion\" >= 1 AND " +
                    "\"TombstoneRevision\" >= 1");
                table.HasCheckConstraint(
                    "CK_reservation_anonymisation_restore_receipts_details",
                    "\"ResultingDetailsRevision\" IS NULL OR " +
                    "\"ResultingDetailsRevision\" >= 1");
                table.HasCheckConstraint(
                    "CK_reservation_anonymisation_restore_receipts_times",
                    "\"ReplayedAtUtc\" >= \"OriginallyCompletedAtUtc\"");
                table.HasCheckConstraint(
                    "CK_reservation_anonymisation_restore_receipts_digests",
                    $"char_length(\"LedgerEntrySha256\") = {ReservationAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"OwnerReceiptSha256\") = {ReservationAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"CanonicalSha256\") = {ReservationAnonymisationReceipt.Sha256Length}");
            });

        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).ValueGeneratedNever();
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.Id
        });
        builder.Property(receipt => receipt.LedgerEntrySha256)
            .HasMaxLength(ReservationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.OwnerReceiptSha256)
            .HasMaxLength(ReservationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(ReservationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.PropertyId,
            receipt.ReservationId,
            receipt.LedgerEntryId
        }).IsUnique();
        builder.HasOne<ReservationAnonymisationTombstone>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.ReservationId
            })
            .HasPrincipalKey(tombstone => new
            {
                tombstone.ScopeId,
                tombstone.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
