namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.GuestRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationGuestRecordLinkProcessConfiguration
    : IEntityTypeConfiguration<ReservationGuestRecordLinkProcess>
{
    public void Configure(
        EntityTypeBuilder<ReservationGuestRecordLinkProcess> builder)
    {
        builder.ToTable("reservation_guest_record_link_processes", table =>
        {
            table.HasCheckConstraint(
                "CK_reservation_guest_record_link_processes_versions",
                "\"ExpectedReservationVersion\" >= 1 AND " +
                "\"Revision\" >= 1 AND \"DispatchRevision\" >= 0");
            table.HasCheckConstraint(
                "CK_reservation_guest_record_link_processes_timestamps",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
            table.HasCheckConstraint(
                "CK_reservation_guest_record_link_processes_lifecycle",
                "(\"State\" = 1 AND \"ReviewReason\" = 1 AND " +
                "\"DispatchRevision\" = 0 AND \"RequestedBy\" IS NOT NULL) OR " +
                "(\"State\" = 2 AND \"ReviewReason\" = 1 AND " +
                "\"DispatchRevision\" >= 1 AND \"RequestedBy\" IS NOT NULL) OR " +
                "(\"State\" = 3 AND \"ReviewReason\" = 1 AND " +
                "\"DispatchRevision\" >= 1 AND \"RequestedBy\" IS NULL) OR " +
                "(\"State\" = 4 AND \"ReviewReason\" >= 2 AND " +
                "\"DispatchRevision\" >= 0 AND \"RequestedBy\" IS NOT NULL)");
        });
        builder.HasKey(process => process.Id);
        builder.HasAlternateKey(process => new { process.ScopeId, process.Id });
        builder.Property(process => process.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(process => process.RequestedBy)
            .HasMaxLength(Reservation.ActorIdMaxLength);
        builder.Property(process => process.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(process => process.ReviewReason)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(process => process.Revision)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(process => new
        {
            process.ScopeId,
            process.ReservationId
        }).IsUnique();
        builder.HasIndex(process => new
        {
            process.ScopeId,
            process.CreationConfirmationId
        }).IsUnique();
        builder.HasIndex(process => new
        {
            process.ScopeId,
            process.PropertyId,
            process.State,
            process.UpdatedAtUtc,
            process.Id
        });
        builder.HasIndex(process => new
        {
            process.ScopeId,
            process.PropertyId,
            process.ReservationId,
            process.State
        });
    }
}
