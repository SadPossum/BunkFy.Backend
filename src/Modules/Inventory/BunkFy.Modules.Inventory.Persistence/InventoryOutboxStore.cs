namespace BunkFy.Modules.Inventory.Persistence;

using Microsoft.Extensions.Options;
using Gma.Framework.Messaging.Infrastructure;

internal sealed class InventoryOutboxStore(InventoryDbContext dbContext, IOptions<OutboxOptions> options)
    : EfOutboxStore<InventoryDbContext>(dbContext, options, InventoryMigrations.Schema);
