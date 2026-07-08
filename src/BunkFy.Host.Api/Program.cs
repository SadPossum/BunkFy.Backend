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
bool enableGmaModules = IsGmaModulesEnabled(builder.Configuration);

builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        string[] configuredOrigins = builder.Configuration
            .GetSection("BunkFy:AllowedCorsOrigins")
            .Get<string[]>() ?? [];

        if (builder.Environment.IsDevelopment() || configuredOrigins.Length == 0)
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy
                .WithOrigins(configuredOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

if (enableGmaModules)
{
    builder.AddGmaInfrastructure();
    builder.Services.AddApiSecurityDefaults();

    builder.AddModule<TenancyModule>();
    builder.AddAuthModule(AuthProfile.TenantScoped());
    builder.AddModule<FilesModule>();
    builder.AddModule<NotificationsModule>();

    builder.ValidateModuleComposition();
}

WebApplication app = builder.Build();

app.UseCors();

if (enableGmaModules)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapGet("/", () => Results.Ok(new
{
    Application = "BunkFy",
    Service = "BunkFy.Host.Api",
    GmaModulesEnabled = enableGmaModules
}));

app.MapHealthChecks("/health");

app.MapGet("/api/smoke", () => Results.Ok(new
{
    Application = "BunkFy",
    Service = "BunkFy.Host.Api",
    Status = "ok",
    GmaModulesEnabled = enableGmaModules,
    TimestampUtc = DateTimeOffset.UtcNow
}));

if (enableGmaModules)
{
    app.MapModules();
}

app.Run();

static bool IsGmaModulesEnabled(IConfiguration configuration)
{
    return bool.TryParse(configuration["BunkFy:EnableGmaModules"], out bool enabled) && enabled;
}
