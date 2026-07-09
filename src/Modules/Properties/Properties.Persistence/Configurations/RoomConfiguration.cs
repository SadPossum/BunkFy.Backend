namespace Properties.Persistence.Configurations;

using Properties.Domain.Aggregates;
using Properties.Domain.Entities;
using Properties.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");
        builder.HasKey(room => room.Id);
        builder.Property(room => room.PropertyId).IsRequired();
        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(room => new { room.TenantId, room.PropertyId })
            .HasPrincipalKey(property => new { property.TenantId, property.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.Property(room => room.Name)
            .HasConversion(name => name.Value, value => RoomName.Create(value).Value)
            .HasMaxLength(Room.RoomNameMaxLength)
            .IsRequired();
        builder.Property(room => room.BuildingLabel)
            .HasConversion(
                label => label.HasValue ? label.Value.Value : null,
                value => string.IsNullOrWhiteSpace(value) ? null : PhysicalLabel.Create(value).Value)
            .HasMaxLength(Room.PhysicalLabelMaxLength);
        builder.Property(room => room.FloorLabel)
            .HasConversion(
                label => label.HasValue ? label.Value.Value : null,
                value => string.IsNullOrWhiteSpace(value) ? null : PhysicalLabel.Create(value).Value)
            .HasMaxLength(Room.PhysicalLabelMaxLength);
        builder.Property(room => room.Status).HasConversion<int>().IsRequired();
        builder.HasIndex(room => new { room.TenantId, room.PropertyId, room.Name }).IsUnique();

        builder.OwnsMany(
            room => room.Beds,
            beds =>
            {
                beds.ToTable("beds");
                beds.WithOwner().HasForeignKey(bed => bed.RoomId);
                beds.HasKey(bed => bed.Id);
                beds.Property(bed => bed.Id).ValueGeneratedNever();
                beds.Property(bed => bed.TenantId).HasMaxLength(128).IsRequired();
                beds.Property(bed => bed.PropertyId).IsRequired();
                beds.Property(bed => bed.Label)
                    .HasConversion(label => label.Value, value => BedLabel.Create(value).Value)
                    .HasMaxLength(Room.BedLabelMaxLength)
                    .IsRequired();
                beds.Property(bed => bed.Status).HasConversion<int>().IsRequired();
                beds.HasIndex(bed => new { bed.TenantId, bed.RoomId, bed.Label }).IsUnique();
            });

        builder.Navigation(room => room.Beds).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
