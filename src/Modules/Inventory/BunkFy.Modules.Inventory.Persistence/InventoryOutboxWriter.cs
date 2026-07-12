namespace BunkFy.Modules.Inventory.Persistence;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Options;

internal sealed class InventoryOutboxWriter(
    InventoryDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity,
    IEnumerable<IIntegrationEventScopeResolver> scopeResolvers)
    : EfOutboxWriter<InventoryDbContext>(
        dbContext,
        clock,
        applicationIdentity,
        InventoryMigrations.Schema,
        scopeResolvers);
