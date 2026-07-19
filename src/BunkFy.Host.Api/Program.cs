using Gma.Modules.Auth.Api;
using Gma.Modules.Auth.Authenticators.Totp;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.AccessControl.Application;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.AccessControl.Persistence;
using Gma.Modules.Auth.Persistence;
using Gma.Modules.Auth.Providers.OpenIdConnect;
using Gma.Modules.Notifications.Adapters.Email;
using Gma.Modules.Notifications.Persistence;
using Gma.Extensions.Auth.Notifications;
using Gma.Extensions.Auth.Organizations;
using Gma.Extensions.Organizations.Tenancy;
using Gma.Modules.Organizations.Api;
using Gma.Modules.Organizations.Persistence;
using BunkFy.Extensions.Operations.Notifications;
using BunkFy.Extensions.Workspaces;
using BunkFy.Modules.Properties.Persistence;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Inventory.Persistence;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Inventory.Api;
using BunkFy.Modules.Reservations.Api;
using BunkFy.Modules.Guests.Api;
using BunkFy.Modules.Staff.Api;
using BunkFy.Modules.Ingestion.Api;
using BunkFy.Adapters.FakeHttp;
using BunkFy.Adapters.ImapReservationMail;
using BunkFy.Adapters.JsonFileDrop;
using BunkFy.Parsers.ReservationMail;
using BunkFy.Modules.Properties.Api;
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
using BunkFy.Host.Api;
using Microsoft.Extensions.DependencyInjection.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
string authScopeId = builder.Configuration["Auth:GlobalScopeId"] ?? AuthProfile.DefaultGlobalScopeId;
AuthProfile authProfile = AuthProfile.Global(authScopeId);

builder.Host.UseConfiguredSerilog();

builder.AddUserNotificationsCqrs();
builder.AddUserNotificationsRealtime();
builder.AddRedisCaching();
builder.AddCachingCqrs();
builder.AddGmaInfrastructure();
builder.AddBunkFyDataProtection();
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
builder.Services.AddFakeHttpAdapterDescriptor();
builder.Services.AddImapReservationMailAdapterDescriptor();
builder.Services.AddJsonFileDropAdapterDescriptor();
builder.Services.AddReservationMailParserDescriptor();

builder.SelectModuleProfile(AccessControlProfiles.Default, "BunkFy.Host.Api/AccessControl");
builder.Services.AddAccessControlApplication(builder.Configuration);
builder.AddAccessControlPersistence();

builder.AddModule<TenancyModule>();
builder.AddAuthModule(authProfile);
builder.AddAuthTotpAuthenticator();
builder.AddAuthOpenIdConnectProviders();
builder.AddModule<FilesModule>();
builder.AddModule<NotificationsModule>();
builder.AddModule<OrganizationsModule>();
builder.Services.AddAuthNotificationsExtension();
builder.Services.AddAuthOrganizationsExtension(options => options.GlobalAuthScopeId = authScopeId);
builder.Services.AddOrganizationsTenancyExtension();
builder.Services.AddBunkFyWorkspaces(options => options.GlobalAuthScopeId = authScopeId);
builder.Services.Replace(ServiceDescriptor.Scoped<INotificationUserScopeAuthorizer, WorkspaceNotificationUserScopeAuthorizer>());
builder.Services.AddBunkFyOperationsNotifications();
builder.Services.AddBunkFyWorkspaceOwnerNotificationAudience();
builder.Services.AddNotificationEmailAdapter(builder.Configuration);
builder.AddModule<PropertiesModule>();
builder.AddModule<InventoryModule>();
builder.AddModule<ReservationsModule>();
builder.AddModule<GuestsModule>();
builder.AddModule<StaffModule>();
builder.AddModule<IngestionModule>();
// module-scaffold:public-api-modules

builder.AddServiceDefaults();
builder.AddGmaProductionHttp();
builder.Services.AddGmaEntityFrameworkReadinessCheck<AccessControlDbContext>("access-control-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<AuthDbContext>("auth-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<NotificationsDbContext>("notifications-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<OrganizationsDbContext>("organizations-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<PropertiesDbContext>("properties-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<InventoryDbContext>("inventory-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<ReservationsDbContext>("reservations-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<GuestsDbContext>("guests-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<StaffDbContext>("staff-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<IngestionDbContext>("ingestion-database");
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
app.MapBunkFyAccessPermissionEndpoints();
app.MapUserNotificationServerSentEvents();
app.MapUserNotificationSignalR();

app.Run();
