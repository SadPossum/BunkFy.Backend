using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Inventory.Persistence;
using BunkFy.Modules.Properties.Persistence;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Modules.AccessControl.Persistence;
using Gma.Modules.Administration.Persistence;
using Gma.Modules.Auth.Persistence;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Notifications.Persistence;
using Gma.Modules.Organizations.Persistence;
using Gma.Modules.TaskRuntime.Persistence;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

string provider = builder.Configuration["Persistence:Provider"] ?? "PostgreSql";
string authScopeId = builder.Configuration["Auth:GlobalScopeId"] ?? AuthProfile.DefaultGlobalScopeId;
if (!string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("BunkFy.Host.Migrations supports the PostgreSQL deployment provider only.");
}

builder.Services.AddScoped<IScopeContext, DesignTimeScopeContext>();
builder.AddAdministrationPersistence();
builder.AddAccessControlPersistence();
builder.AddAuthPersistence(AuthProfile.Global(authScopeId));
builder.AddNotificationsPersistence();
builder.AddOrganizationsPersistence();
builder.AddTaskRuntimePersistence();
builder.AddPropertiesPersistence();
builder.AddInventoryPersistence();
builder.AddReservationsPersistence();
builder.AddGuestsPersistence();
builder.AddStaffPersistence();
builder.AddWorkspacesPersistence();
builder.AddIngestionPersistence();

using IHost host = builder.Build();
await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
    .CreateLogger("BunkFy.Host.Migrations");

(string Name, Type ContextType)[] migrations =
[
    ("administration", typeof(AdminDbContext)),
    ("access-control", typeof(AccessControlDbContext)),
    ("auth", typeof(AuthDbContext)),
    ("notifications", typeof(NotificationsDbContext)),
    ("organizations", typeof(OrganizationsDbContext)),
    ("task-runtime", typeof(TaskRuntimeDbContext)),
    ("properties", typeof(PropertiesDbContext)),
    ("inventory", typeof(InventoryDbContext)),
    ("reservations", typeof(ReservationsDbContext)),
    ("guests", typeof(GuestsDbContext)),
    ("staff", typeof(StaffDbContext)),
    ("workspaces", typeof(WorkspacesDbContext)),
    ("ingestion", typeof(IngestionDbContext))
];

foreach ((string name, Type contextType) in migrations)
{
    logger.LogInformation("Applying {ModuleName} database migrations.", name);
    DbContext context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
    await context.Database.MigrateAsync(CancellationToken.None).ConfigureAwait(false);
}

logger.LogInformation("All BunkFy PostgreSQL migrations are current.");
