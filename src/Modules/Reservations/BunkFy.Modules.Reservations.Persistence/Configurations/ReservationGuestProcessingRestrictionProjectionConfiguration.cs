namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationGuestProcessingRestrictionProjectionConfiguration
    : IEntityTypeConfiguration<ReservationGuestProcessingRestrictionProjection>
{
    public void Configure(
        EntityTypeBuilder<ReservationGuestProcessingRestrictionProjection> builder)
    {
        builder.ToTable("guest_processing_restriction_projection", table =>
        {
            table.HasCheckConstraint(
                "CK_reservations_guest_restriction_contract_version",
                "\"ContractVersion\" >= 1");
            table.HasCheckConstraint(
                "CK_reservations_guest_restriction_revision",
                "\"Revision\" >= 0");
        });
        builder.HasKey(projection => new
        {
            projection.ScopeId,
            projection.PropertyId,
            projection.GuestId
        });
        builder.Property(projection => projection.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(projection => projection.Revision).IsConcurrencyToken();
        builder.HasIndex(projection => new
        {
            projection.ScopeId,
            projection.PropertyId,
            projection.IsRestricted,
            projection.GuestId
        }).HasDatabaseName("IX_guest_restriction_linkable");
    }
}
