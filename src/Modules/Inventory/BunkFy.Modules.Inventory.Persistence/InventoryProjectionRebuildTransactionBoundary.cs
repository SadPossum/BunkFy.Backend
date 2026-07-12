namespace BunkFy.Modules.Inventory.Persistence;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using BunkFy.Modules.Inventory.Contracts;

internal sealed class InventoryProjectionRebuildTransactionBoundary(InventoryDbContext dbContext)
    : EfProjectionRebuildTransactionBoundary<InventoryDbContext>(dbContext, InventoryModuleMetadata.Name);
