namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationDataHoldConfiguration
    : IEntityTypeConfiguration<ReservationDataHold>
{
    public void Configure(EntityTypeBuilder<ReservationDataHold> builder)
    {
        builder.ToTable("reservation_data_holds", table =>
        {
            table.HasCheckConstraint(
                "CK_reservation_data_holds_version",
                "\"Version\" >= 1");
            table.HasCheckConstraint(
                "CK_reservation_data_holds_lifecycle",
                "(\"State\" = 1 AND \"ReleasedBy\" IS NULL AND " +
                "\"ReleasedAtUtc\" IS NULL AND \"Version\" = 1) OR " +
                "(\"State\" = 2 AND \"ReleasedBy\" IS NOT NULL AND " +
                "\"ReleasedAtUtc\" IS NOT NULL AND " +
                "\"ReleasedAtUtc\" >= \"PlacedAtUtc\" AND \"Version\" = 2)");
        });
        builder.HasKey(hold => hold.Id);
        builder.HasAlternateKey(hold => new { hold.ScopeId, hold.Id });
        builder.Property(hold => hold.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(hold => hold.ReasonCode)
            .HasMaxLength(ReservationDataHold.ReasonCodeMaxLength)
            .IsRequired();
        builder.Property(hold => hold.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(hold => hold.PlacedBy)
            .HasMaxLength(Reservation.ActorIdMaxLength)
            .IsRequired();
        builder.Property(hold => hold.ReleasedBy)
            .HasMaxLength(Reservation.ActorIdMaxLength);
        builder.Property(hold => hold.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(hold => new
        {
            hold.ScopeId,
            hold.ReservationId,
            hold.State,
            hold.PropertyId,
            hold.Id
        });
        builder.HasIndex(hold => new
        {
            hold.ScopeId,
            hold.PropertyId,
            hold.ReservationId,
            hold.PlacedAtUtc,
            hold.Id
        });
        builder.HasIndex(hold => new
        {
            hold.ScopeId,
            hold.PropertyId,
            hold.ReservationId,
            hold.State,
            hold.PlacedAtUtc,
            hold.Id
        });
    }
}
