namespace Inventory.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class InventoryInboxStore(InventoryDbContext dbContext, ISystemClock clock, IIdGenerator idGenerator)
    : EfInboxStore<InventoryDbContext>(dbContext, clock, idGenerator, InventoryMigrations.Schema);
