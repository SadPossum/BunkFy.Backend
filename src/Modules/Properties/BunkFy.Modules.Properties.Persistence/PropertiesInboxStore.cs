namespace BunkFy.Modules.Properties.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class PropertiesInboxStore(
    PropertiesDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator,
    IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventInboxStore<PropertiesDbContext>(
        dbContext,
        clock,
        idGenerator,
        domainEventDispatcher,
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
