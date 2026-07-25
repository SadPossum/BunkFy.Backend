namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationProcessingRestrictionProjectionConfiguration
    : IEntityTypeConfiguration<ReservationProcessingRestrictionProjection>
{
    public void Configure(
        EntityTypeBuilder<ReservationProcessingRestrictionProjection> builder)
    {
        builder.ToTable("reservation_processing_restriction_state", table =>
        {
            table.HasCheckConstraint(
                "CK_reservation_processing_restriction_state_contract",
                "\"ContractVersion\" >= 1");
            table.HasCheckConstraint(
                "CK_reservation_processing_restriction_state_revision",
                "\"Revision\" >= 0 AND \"ActiveRestrictionCount\" >= 0 AND " +
                "\"ActiveRestrictionCount\" <= \"Revision\"");
            table.HasCheckConstraint(
                "CK_reservation_processing_restriction_state_effective",
                "(\"ActiveRestrictionCount\" = 0 AND NOT \"IsRestricted\") OR " +
                "(\"ActiveRestrictionCount\" > 0 AND \"IsRestricted\")");
        });
        builder.HasKey(projection => new
        {
            projection.ScopeId,
            projection.PropertyId,
            projection.ReservationId
        });
        builder.Property(projection => projection.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(projection => projection.ProjectionOrdinal)
            .ValueGeneratedOnAdd()
            .IsRequired();
        builder.Property(projection => projection.Revision).IsConcurrencyToken();
        builder.HasIndex(projection => projection.ProjectionOrdinal).IsUnique();
        builder.HasIndex(projection => new
        {
            projection.ScopeId,
            projection.PropertyId,
            projection.IsRestricted,
            projection.ReservationId
        });
    }
}
