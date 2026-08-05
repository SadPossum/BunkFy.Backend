namespace BunkFy.Modules.Retention.Persistence;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class RetentionInboxStore(
    RetentionDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : EfInboxStore<RetentionDbContext>(
        dbContext,
        clock,
        idGenerator,
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
