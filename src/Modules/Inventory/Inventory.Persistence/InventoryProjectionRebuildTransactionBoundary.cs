namespace Inventory.Persistence;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using Inventory.Contracts;

internal sealed class InventoryProjectionRebuildTransactionBoundary(InventoryDbContext dbContext)
    : EfProjectionRebuildTransactionBoundary<InventoryDbContext>(dbContext, InventoryModuleMetadata.Name);
