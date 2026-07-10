using Gma.Modules.Auth.Api;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.AccessControl.Application;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.AccessControl.Persistence;
using Gma.Modules.Auth.Persistence;
using Gma.Modules.Notifications.Persistence;
using Catalog.Persistence;
using Ordering.Persistence;
using Properties.Persistence;
using Inventory.Persistence;
using Reservations.Persistence;
using Catalog.Api;
using Inventory.Api;
using Reservations.Api;
using Properties.Api;
using BunkFy.Host.ServiceDefaults;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.OpenApi;
using Gma.Framework.Api.Production;
using Gma.Framework.Api.Production.EntityFrameworkCore;
using Gma.Framework.Api.Security;
using Gma.Framework.Api.Serilog;
using Gma.Framework.Caching.Cqrs;
using Gma.Framework.Caching.Redis;
using Gma.Framework.FileManagement.Minio;
using Gma.Framework.Infrastructure;
using Gma.Framework.Logging.Serilog;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Messaging.Nats.Aspire;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Notifications.Api;
using Gma.Framework.Notifications.Cqrs;
using Gma.Framework.Notifications.SignalR;
using Gma.Framework.Realtime.Notifications;
using Gma.Framework.Tenancy.Api.Serilog;
using Gma.Framework.Tenancy.Caching;
using Gma.Framework.Tenancy.Messaging.Infrastructure;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Gma.Modules.Files.Api;
using Gma.Modules.Notifications.Api;
using Gma.Modules.Tenancy.Api;
using Ordering.Api;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseConfiguredSerilog();

builder.AddUserNotificationsCqrs();
builder.AddUserNotificationsRealtime();
builder.AddRedisCaching();
builder.AddCachingCqrs();
builder.AddGmaInfrastructure();
builder.AddTenantSerilogRequestLogging();
builder.AddTenantCaching();
builder.AddMinioFileStorage();
builder.AddMessagingInfrastructure();
builder.AddTenantAwareMessaging();
builder.AddConfiguredNatsJetStreamMessaging();
builder.AddUserNotificationServerSentEvents();
builder.AddUserNotificationSignalR();
builder.Services.AddApiSecurityDefaults();
builder.Services.AddGmaTenantAccessControlAspNetCore();

builder.SelectModuleProfile(AccessControlProfiles.Default, "BunkFy.Host.Api/AccessControl");
builder.Services.AddAccessControlApplication(builder.Configuration);
builder.AddAccessControlPersistence();

builder.AddModule<TenancyModule>();
builder.AddAuthModule(AuthProfile.ScopeAware());
builder.AddModule<FilesModule>();
builder.AddModule<NotificationsModule>();
builder.AddModule<CatalogModule>();
builder.AddModule<OrderingModule>();
builder.AddModule<PropertiesModule>();
builder.AddModule<InventoryModule>();
builder.AddModule<ReservationsModule>();
// module-scaffold:public-api-modules

builder.AddServiceDefaults();
builder.AddGmaProductionHttp();
builder.Services.AddGmaEntityFrameworkReadinessCheck<AccessControlDbContext>("access-control-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<AuthDbContext>("auth-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<NotificationsDbContext>("notifications-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<CatalogDbContext>("catalog-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<OrderingDbContext>("ordering-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<PropertiesDbContext>("properties-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<InventoryDbContext>("inventory-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<ReservationsDbContext>("reservations-database");
builder.AddGmaOpenApi();
builder.ValidateModuleComposition();

WebApplication app = builder.Build();

app.UseGmaOpenApi();
app.UseGmaProductionHttp();
app.UseGmaSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapGet("/api/smoke", () => Results.Ok(new
{
    Application = "BunkFy",
    Service = "BunkFy.Host.Api",
    Status = "ok",
    TimestampUtc = DateTimeOffset.UtcNow
}));
app.MapModules();
app.MapUserNotificationServerSentEvents();
app.MapUserNotificationSignalR();

app.Run();
