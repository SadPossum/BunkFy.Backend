using Catalog.AdminApi;
using Gma.Modules.AccessControl.AdminApi;
using Gma.Modules.AccessControl.Persistence;
using Gma.Modules.Administration.AdminApi;
using Gma.Modules.Administration.Persistence;
using Gma.Modules.Auth.AdminApi;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Persistence;
using Gma.Modules.TaskRuntime.AdminApi;
using Gma.Modules.TaskRuntime.Persistence;
using Properties.AdminApi;
using Properties.Persistence;
using Inventory.AdminApi;
using Inventory.Persistence;
using Reservations.AdminApi;
using Reservations.Persistence;
using Catalog.Persistence;
using BunkFy.Host.ServiceDefaults;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.OpenApi;
using Gma.Framework.Api.Production;
using Gma.Framework.Api.Production.EntityFrameworkCore;
using Gma.Framework.Api.Security;
using Gma.Framework.Api.Serilog;
using Gma.Framework.Caching.Cqrs;
using Gma.Framework.Caching.Redis;
using Gma.Framework.Infrastructure;
using Gma.Framework.Logging.Serilog;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Messaging.Nats.Aspire;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy.Api.Serilog;
using Gma.Framework.Tenancy.Caching;
using Gma.Framework.Tenancy.Messaging.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseConfiguredSerilog();

builder.Services.AddGmaAdministrationApi(builder.Configuration);
builder.AddRedisCaching();
builder.AddCachingCqrs();
builder.AddGmaInfrastructure();
builder.AddTenantSerilogRequestLogging();
builder.AddTenantCaching();
builder.AddMessagingInfrastructure();
builder.AddTenantAwareMessaging();
builder.AddConfiguredNatsJetStreamMessaging();
builder.Services.AddApiSecurityDefaults();

builder.AddAdminApiModule<AdministrationAdminApiModule>();
builder.AddAdminApiModule<AccessControlAdminApiModule>();
builder.AddAuthAdminApiModule(AuthProfile.ScopeAware());
builder.AddAdminApiModule<TaskRuntimeAdminApiModule>();
builder.AddAdminApiModule<CatalogAdminApiModule>();
builder.AddAdminApiModule<PropertiesAdminApiModule>();
builder.AddAdminApiModule<InventoryAdminApiModule>();
builder.AddAdminApiModule<ReservationsAdminApiModule>();

builder.AddServiceDefaults();
builder.AddGmaProductionHttp();
builder.Services.AddGmaEntityFrameworkReadinessCheck<AdminDbContext>("administration-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<AccessControlDbContext>("access-control-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<AuthDbContext>("auth-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<TaskRuntimeDbContext>("task-runtime-database");
builder.Services.AddGmaEntityFrameworkReadinessCheck<CatalogDbContext>("catalog-database");
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
app.MapAdminApiModules();

app.Run();
