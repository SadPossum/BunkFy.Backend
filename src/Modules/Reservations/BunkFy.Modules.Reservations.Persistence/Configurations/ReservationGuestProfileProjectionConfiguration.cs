namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationGuestProfileProjectionConfiguration
    : IEntityTypeConfiguration<ReservationGuestProfileProjection>
{
    public void Configure(EntityTypeBuilder<ReservationGuestProfileProjection> builder)
    {
        builder.ToTable("guest_profile_projection", table => table.HasCheckConstraint(
            "CK_reservations_guest_profile_projection_version",
            "\"Version\" >= 1"));
        builder.HasKey(profile => new { profile.ScopeId, profile.Id });
        builder.Property(profile => profile.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(profile => profile.Status).HasConversion<int>().IsRequired();
        builder.Property(profile => profile.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(profile => new { profile.ScopeId, profile.OriginPropertyId, profile.Status, profile.Id });
    }
}
