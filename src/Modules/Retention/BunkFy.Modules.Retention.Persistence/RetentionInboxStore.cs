namespace BunkFy.Modules.Retention.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class RetentionInboxStore(
    RetentionDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator,
    IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventInboxStore<RetentionDbContext>(
        dbContext,
        clock,
        idGenerator,
        domainEventDispatcher,
        RetentionMigrations.Schema)
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
