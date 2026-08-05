namespace BunkFy.Modules.Workspaces.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using BunkFy.Modules.Workspaces.Domain.Termination;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal sealed class WorkspacesOutboxStore(
    WorkspacesDbContext dbContext,
    IOptions<OutboxOptions> options)
    : EfOutboxStore<WorkspacesDbContext>(
        dbContext,
        options,
        WorkspacesMigrations.Schema)
{
    protected override IQueryable<OutboxMessage> ApplyClaimAdmission(
        IQueryable<OutboxMessage> candidates) =>
        candidates.Where(message =>
            message.ScopeId == null ||
            !this.DbContext.WorkspaceTerminationFences
                .IgnoreQueryFilters()
                .Any(fence =>
                    fence.ScopeId == message.ScopeId &&
                    fence.State != WorkspaceTerminationFenceState.Released));
}
