namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationPropertyProjectionConfiguration
    : IEntityTypeConfiguration<ReservationPropertyProjection>
{
    public void Configure(EntityTypeBuilder<ReservationPropertyProjection> builder)
    {
        builder.ToTable("property_projection");
        builder.HasKey(property => property.Id);
        builder.HasAlternateKey(property => new { property.ScopeId, property.Id });
        builder.Property(property => property.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(property => property.TimeZoneId)
            .HasMaxLength(ReservationsContractLimits.TimeZoneIdMaxLength);
        builder.Property(property => property.SourceVersion).IsConcurrencyToken().IsRequired();
        builder.HasIndex(property => new { property.ScopeId, property.IsActive });
    }
}
