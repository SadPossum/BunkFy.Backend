namespace BunkFy.Modules.Workspaces.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Options;

internal sealed class WorkspacesOutboxWriter(
    WorkspacesDbContext dbContext,
    ISystemClock clock,
    IOptions<ApplicationIdentityOptions> applicationIdentity)
    : EfOutboxWriter<WorkspacesDbContext>(
        dbContext,
        clock,
        applicationIdentity,
        WorkspacesMigrations.Schema);
