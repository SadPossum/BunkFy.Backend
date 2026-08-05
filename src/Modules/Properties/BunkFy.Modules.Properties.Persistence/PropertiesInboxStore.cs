namespace BunkFy.Modules.Properties.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class PropertiesInboxStore(
    PropertiesDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : EfInboxStore<PropertiesDbContext>(
        dbContext,
        clock,
        idGenerator,
        PropertiesMigrations.Schema)
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
