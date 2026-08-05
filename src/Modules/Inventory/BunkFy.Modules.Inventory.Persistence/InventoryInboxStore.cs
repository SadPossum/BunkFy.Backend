namespace BunkFy.Modules.Inventory.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class InventoryInboxStore(
    InventoryDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : EfInboxStore<InventoryDbContext>(
        dbContext,
        clock,
        idGenerator,
        InventoryMigrations.Schema)
{
    protected override ValueTask<bool> IsAdmittedAsync(
        InboxMessageRecord message,
        CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(message.ScopeId)
            ? ValueTask.FromResult(true)
            : this.DbContext.TryAdmitMessageMutationAsync(
                message.ScopeId,
                cancellationToken);
}
