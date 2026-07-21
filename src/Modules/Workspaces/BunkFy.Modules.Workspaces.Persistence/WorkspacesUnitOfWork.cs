namespace BunkFy.Modules.Workspaces.Persistence;

using Gma.Framework.Application.Events;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class WorkspacesUnitOfWork(
    WorkspacesDbContext dbContext,
    IDomainEventDispatcher domainEventDispatcher)
    : EfDomainEventUnitOfWork<WorkspacesDbContext>(
        WorkspacesMigrations.Schema,
        dbContext,
        domainEventDispatcher);
