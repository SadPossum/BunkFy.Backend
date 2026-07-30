namespace BunkFy.Modules.Workspaces.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Microsoft.Extensions.Options;

internal sealed class WorkspacesOutboxStore(
    WorkspacesDbContext dbContext,
    IOptions<OutboxOptions> options)
    : EfOutboxStore<WorkspacesDbContext>(
        dbContext,
        options,
        WorkspacesMigrations.Schema);
