namespace BunkFy.Modules.Properties.Persistence.Configurations;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Naming;
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
            .HasForeignKey(room => new { room.ScopeId, room.PropertyId })
            .HasPrincipalKey(property => new { property.ScopeId, property.Id })
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
        builder.Property(room => room.Version)
            .HasDefaultValue(1L)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(room => new { room.ScopeId, room.PropertyId, room.Name }).IsUnique();

        builder.OwnsMany(
            room => room.Beds,
            beds =>
            {
                beds.ToTable("beds");
                beds.WithOwner().HasForeignKey(bed => bed.RoomId);
                beds.HasKey(bed => bed.Id);
                beds.Property(bed => bed.Id).ValueGeneratedNever();
                beds.Property(bed => bed.ScopeId).HasMaxLength(ScopeIds.MaxLength).IsRequired();
                beds.Property(bed => bed.PropertyId).IsRequired();
                beds.Property(bed => bed.Label)
                    .HasConversion(label => label.Value, value => BedLabel.Create(value).Value)
                    .HasMaxLength(Room.BedLabelMaxLength)
                    .IsRequired();
                beds.Property(bed => bed.Status).HasConversion<int>().IsRequired();
                beds.Property(bed => bed.Version)
                    .HasDefaultValue(1L)
                    .IsConcurrencyToken()
                    .IsRequired();
                beds.HasIndex(bed => new { bed.ScopeId, bed.RoomId, bed.Label }).IsUnique();
            });

        builder.Navigation(room => room.Beds).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
