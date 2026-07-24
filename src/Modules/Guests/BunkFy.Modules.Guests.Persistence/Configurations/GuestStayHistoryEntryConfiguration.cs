namespace BunkFy.Modules.Guests.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestStayHistoryEntryConfiguration : IEntityTypeConfiguration<GuestStayHistoryEntry>
{
    public void Configure(EntityTypeBuilder<GuestStayHistoryEntry> builder)
    {
        builder.ToTable("stay_history", table =>
        {
            table.HasCheckConstraint("CK_guests_stay_history_range", "\"Arrival\" < \"Departure\"");
            table.HasCheckConstraint("CK_guests_stay_history_version", "\"ReservationVersion\" >= 1");
        });
        builder.HasKey(stay => new { stay.ScopeId, stay.GuestId, stay.ReservationId });
        builder.Property(stay => stay.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(stay => stay.Role).HasConversion<int>().IsRequired();
        builder.Property(stay => stay.Status).HasConversion<int>().IsRequired();
        builder.Property(stay => stay.ReservationVersion).IsConcurrencyToken().IsRequired();
        builder.HasIndex(stay => new { stay.ScopeId, stay.PropertyId, stay.IsCurrentParticipant, stay.GuestId, stay.Arrival });
        builder.HasIndex(stay => new { stay.ScopeId, stay.PropertyId, stay.GuestId });
        builder.HasIndex(stay => new { stay.ScopeId, stay.ReservationId, stay.GuestId });
    }
}
