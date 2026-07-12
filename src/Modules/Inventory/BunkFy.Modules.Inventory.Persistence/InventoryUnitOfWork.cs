namespace BunkFy.Modules.Inventory.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class InventoryUnitOfWork(InventoryDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<InventoryDbContext>(InventoryMigrations.Schema, dbContext, domainEventDispatcher)
{
}
