using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Security;
using Gma.Framework.Infrastructure;
using Gma.Framework.ModuleComposition;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Api;
using Gma.Modules.Files.Api;
using Gma.Modules.Notifications.Api;
using Gma.Modules.Tenancy.Api;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddGmaInfrastructure();
builder.Services.AddApiSecurityDefaults();

builder.AddModule<TenancyModule>();
builder.AddAuthModule(AuthProfile.TenantScoped());
builder.AddModule<FilesModule>();
builder.AddModule<NotificationsModule>();

builder.ValidateModuleComposition();

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    Application = "BunkFy",
    Service = "BunkFy.Host.Api"
}));

app.MapGet("/api/system/version", () => Results.Ok(new
{
    Application = "BunkFy",
    Service = "BunkFy.Host.Api",
    Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"
}));

app.MapModules();

app.Run();
