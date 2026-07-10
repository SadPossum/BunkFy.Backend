namespace Inventory.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Properties.Contracts;

internal sealed class InventoryRoomTopologyConfiguration : IEntityTypeConfiguration<InventoryRoomTopology>
{
    public void Configure(EntityTypeBuilder<InventoryRoomTopology> builder)
    {
        builder.ToTable("room_topology");
        builder.HasKey(room => room.Id);
        builder.Property(room => room.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(room => room.Name).HasMaxLength(PropertiesContractLimits.RoomNameMaxLength).IsRequired();
        builder.Property(room => room.BuildingLabel).HasMaxLength(PropertiesContractLimits.PhysicalLabelMaxLength);
        builder.Property(room => room.FloorLabel).HasMaxLength(PropertiesContractLimits.PhysicalLabelMaxLength);
        builder.Property(room => room.Status).HasConversion<int>();
        builder.HasIndex(room => new { room.ScopeId, room.PropertyId, room.Name });
    }
}
