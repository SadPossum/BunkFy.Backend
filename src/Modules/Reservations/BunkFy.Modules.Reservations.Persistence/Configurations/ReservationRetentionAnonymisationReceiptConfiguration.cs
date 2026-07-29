namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    ReservationRetentionAnonymisationReceiptConfiguration
    : IEntityTypeConfiguration<
        ReservationRetentionAnonymisationReceipt>
{
    public void Configure(
        EntityTypeBuilder<
            ReservationRetentionAnonymisationReceipt> builder)
    {
        builder.ToTable(
            "reservation_retention_anonymisation_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_reservation_retention_receipts_contract",
                    $"\"ContractVersion\" = {ReservationRetentionAnonymisationReceipt.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_reservation_retention_receipts_actor",
                    $"\"ActorId\" = '{ReservationRetentionAnonymisationReceipt.SystemActorId}'");
                table.HasCheckConstraint(
                    "CK_reservation_retention_receipts_versions",
                    "\"SelectedReservationVersion\" >= 1 AND " +
                    "\"ResultingReservationVersion\" = \"SelectedReservationVersion\" + 1 AND " +
                    "\"SelectedDetailsRevision\" >= 1 AND " +
                    "\"ResultingDetailsRevision\" = \"SelectedDetailsRevision\" + 1");
                table.HasCheckConstraint(
                    "CK_reservation_retention_receipts_counts",
                    "\"RedactedHistoryCount\" >= 1 AND " +
                    "\"RemovedGuestLinkCount\" >= 0 AND " +
                    "\"ReducedExternalOperationCount\" >= 0 AND " +
                    "\"SuppressedReminderCount\" >= 0");
                table.HasCheckConstraint(
                    "CK_reservation_retention_receipts_digests",
                    $"char_length(\"PolicyEvidenceSha256\") = {ReservationRetentionAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"CanonicalSha256\") = {ReservationRetentionAnonymisationReceipt.Sha256Length}");
                table.HasCheckConstraint(
                    "CK_reservation_retention_receipts_deadline",
                    "\"RetentionDeadlineUtc\" >= \"TerminalAtUtc\" AND " +
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
                ReservationRetentionAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(
                ReservationRetentionAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(Reservation.ActorIdMaxLength)
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ReservationId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ExecutionId,
            receipt.ReservationId
        }).IsUnique();
        builder.HasOne<ReservationRetentionExecution>()
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
        builder.HasOne<Reservation>()
            .WithOne()
            .HasForeignKey<
                ReservationRetentionAnonymisationReceipt>(
                receipt => new
                {
                    receipt.ScopeId,
                    receipt.ReservationId
                })
            .HasPrincipalKey<Reservation>(
                reservation => new
                {
                    reservation.ScopeId,
                    reservation.Id
                })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
