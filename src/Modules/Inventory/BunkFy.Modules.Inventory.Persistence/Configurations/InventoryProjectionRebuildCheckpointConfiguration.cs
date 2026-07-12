namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class InventoryProjectionRebuildCheckpointConfiguration
    : IEntityTypeConfiguration<InventoryProjectionRebuildCheckpoint>
{
    public void Configure(EntityTypeBuilder<InventoryProjectionRebuildCheckpoint> builder) =>
        builder.ConfigureProjectionRebuildCheckpointState();
}
