namespace BunkFy.Modules.Inventory.Persistence;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using BunkFy.Modules.Inventory.Contracts;

internal sealed class InventoryProjectionRebuildCheckpointStore(InventoryDbContext dbContext)
    : EfProjectionRebuildCheckpointStore<InventoryDbContext, InventoryProjectionRebuildCheckpoint>(
        dbContext,
        InventoryModuleMetadata.Name,
        scopeAware: true,
        InventoryProjectionRebuildCheckpoint.CreateEmpty);
