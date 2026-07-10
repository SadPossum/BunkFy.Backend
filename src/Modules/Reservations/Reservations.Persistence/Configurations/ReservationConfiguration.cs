namespace Reservations.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reservations.Domain.Aggregates;
using Reservations.Domain.Entities;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");
        builder.HasKey(reservation => reservation.Id);
        builder.HasAlternateKey(reservation => new { reservation.ScopeId, reservation.Id });
        builder.Property(reservation => reservation.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(reservation => reservation.PrimaryGuestName).HasMaxLength(Reservation.PrimaryGuestNameMaxLength).IsRequired();
        builder.Property(reservation => reservation.Email).HasMaxLength(Reservation.EmailMaxLength);
        builder.Property(reservation => reservation.Phone).HasMaxLength(Reservation.PhoneMaxLength);
        builder.Property(reservation => reservation.SourceSystem).HasMaxLength(Reservation.SourceSystemMaxLength);
        builder.Property(reservation => reservation.SourceReference).HasMaxLength(Reservation.SourceReferenceMaxLength);
        builder.Property(reservation => reservation.Notes).HasMaxLength(Reservation.NotesMaxLength);
        builder.Property(reservation => reservation.Source).HasConversion<int>().IsRequired();
        builder.Property(reservation => reservation.Status).HasConversion<int>().IsRequired();
        builder.Property(reservation => reservation.AllocationRejection).HasConversion<int?>();
        builder.Property(reservation => reservation.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(reservation => new { reservation.ScopeId, reservation.AllocationRequestId }).IsUnique();
        builder.HasIndex(reservation => new { reservation.ScopeId, reservation.SourceSystem, reservation.SourceReference }).IsUnique();
        builder.HasIndex(reservation => new
        {
            reservation.ScopeId,
            reservation.PropertyId,
            reservation.Status,
            reservation.Arrival,
            reservation.Departure
        });
        builder.HasMany(reservation => reservation.RequestedUnits)
            .WithOne()
            .HasForeignKey(unit => new { unit.ScopeId, unit.ReservationId })
            .HasPrincipalKey(reservation => new { reservation.ScopeId, reservation.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(reservation => reservation.RequestedUnits).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(reservation => reservation.DomainEvents);
    }
}

internal sealed class RequestedInventoryUnitConfiguration : IEntityTypeConfiguration<RequestedInventoryUnit>
{
    public void Configure(EntityTypeBuilder<RequestedInventoryUnit> builder)
    {
        builder.ToTable("requested_inventory_units");
        builder.HasKey(unit => new { unit.ScopeId, unit.ReservationId, unit.Id });
        builder.Property(unit => unit.ScopeId).HasMaxLength(128).IsRequired();
        builder.Ignore(unit => unit.InventoryUnitId);
        builder.HasIndex(unit => new { unit.ScopeId, unit.Id, unit.ReservationId });
    }
}
