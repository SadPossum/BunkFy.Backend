namespace Inventory.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Properties.Contracts;

internal sealed class InventoryBedTopologyConfiguration : IEntityTypeConfiguration<InventoryBedTopology>
{
    public void Configure(EntityTypeBuilder<InventoryBedTopology> builder)
    {
        builder.ToTable("bed_topology");
        builder.HasKey(bed => bed.Id);
        builder.Property(bed => bed.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(bed => bed.Label).HasMaxLength(PropertiesContractLimits.BedLabelMaxLength).IsRequired();
        builder.Property(bed => bed.Status).HasConversion<int>();
        builder.HasIndex(bed => new { bed.ScopeId, bed.PropertyId, bed.RoomId, bed.Label });
    }
}
