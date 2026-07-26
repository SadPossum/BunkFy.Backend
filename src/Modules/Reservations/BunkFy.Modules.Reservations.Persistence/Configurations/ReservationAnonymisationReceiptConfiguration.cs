namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationAnonymisationReceiptConfiguration
    : IEntityTypeConfiguration<ReservationAnonymisationReceipt>
{
    public void Configure(
        EntityTypeBuilder<ReservationAnonymisationReceipt> builder)
    {
        builder.ToTable("reservation_anonymisation_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_reservation_anonymisation_receipts_contract",
                $"\"ContractVersion\" = " +
                $"{ReservationAnonymisationReceipt.CurrentContractVersion}");
            table.HasCheckConstraint(
                "CK_reservation_anonymisation_receipts_revisions",
                "\"ApprovalRevision\" >= 1 AND " +
                "\"OperationRevision\" > \"ApprovalRevision\"");
            table.HasCheckConstraint(
                "CK_reservation_anonymisation_receipts_versions",
                "\"SelectedReservationVersion\" >= 1 AND " +
                "\"ResultingReservationVersion\" = " +
                "\"SelectedReservationVersion\" + 1 AND " +
                "\"SelectedDetailsRevision\" >= 1 AND " +
                "\"ResultingDetailsRevision\" = " +
                "\"SelectedDetailsRevision\" + 1");
            table.HasCheckConstraint(
                "CK_reservation_anonymisation_receipts_outcome",
                $"\"Disposition\" = " +
                $"{(int)ReservationAnonymisationDisposition.Completed} AND " +
                $"\"Reason\" = " +
                $"{(int)ReservationAnonymisationReason.ReservationOwnerDataRedacted}");
            table.HasCheckConstraint(
                "CK_reservation_anonymisation_receipts_counts",
                "\"RedactedHistoryCount\" >= 1 AND " +
                "\"RemovedGuestLinkCount\" >= 0 AND " +
                "\"ReducedExternalOperationCount\" >= 0 AND " +
                "\"SuppressedReminderCount\" >= 0");
            table.HasCheckConstraint(
                "CK_reservation_anonymisation_receipts_digests",
                $"char_length(\"ApprovalEvidenceSha256\") = " +
                $"{ReservationAnonymisationReceipt.Sha256Length} AND " +
                $"char_length(\"PolicyEvidenceSha256\") = " +
                $"{ReservationAnonymisationReceipt.Sha256Length} AND " +
                $"char_length(\"CanonicalSha256\") = " +
                $"{ReservationAnonymisationReceipt.Sha256Length}");
            table.HasCheckConstraint(
                "CK_reservation_anonymisation_receipts_actor",
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
            .HasMaxLength(ReservationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.PolicyEvidenceSha256)
            .HasMaxLength(ReservationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(ReservationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(Reservation.ActorIdMaxLength)
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
            receipt.ReservationId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.PropertyId,
            receipt.ReservationId,
            receipt.ResultingReservationVersion
        }).IsUnique();
        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.ReservationId
            })
            .HasPrincipalKey(reservation => new
            {
                reservation.ScopeId,
                reservation.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
