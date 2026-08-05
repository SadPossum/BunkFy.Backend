namespace BunkFy.Modules.Workspaces.Persistence;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class WorkspacesInboxStore(
    WorkspacesDbContext dbContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : EfInboxStore<WorkspacesDbContext>(
        dbContext,
        clock,
        idGenerator,
        WorkspacesMigrations.Schema)
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
