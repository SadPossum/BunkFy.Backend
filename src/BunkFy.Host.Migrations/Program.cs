using BunkFy.Host.Migrations;
using Gma.Modules.Auth.Contracts;
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

MigrationsHostOptions migrationsOptions =
    MigrationsHostOptions.FromConfiguration(builder.Configuration);
MigrationsProductionAdmissionOptions productionAdmission =
    MigrationsProductionAdmissionOptions.FromConfiguration(builder.Configuration);
bool isProduction = builder.Environment.IsProduction();
MigrationsProductionAdmission.ValidateConfigurationOrThrow(
    productionAdmission,
    migrationsOptions,
    isProduction);

builder.AddBunkFyMigrationPersistence(authScopeId);

using IHost host = builder.Build();
IHostApplicationLifetime lifetime = host.Services
    .GetRequiredService<IHostApplicationLifetime>();
using CancellationTokenSource operationTimeout = new(
    TimeSpan.FromSeconds(migrationsOptions.OperationTimeoutSeconds));
using CancellationTokenSource operation = CancellationTokenSource
    .CreateLinkedTokenSource(
        operationTimeout.Token,
        lifetime.ApplicationStopping);
bool started = false;
try
{
    await host.StartAsync(operation.Token).ConfigureAwait(false);
    started = true;
    await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
    ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("BunkFy.Host.Migrations");
    IReadOnlyList<BunkFyMigrationModule> migrations =
        BunkFyMigrationCatalog.Resolve(scope.ServiceProvider);
    BunkFyMigrationCoordinator coordinator = new(
        logger,
        migrationsOptions,
        productionAdmission,
        isProduction);
    await coordinator.RunAsync(migrations, operation.Token).ConfigureAwait(false);
}
catch (OperationCanceledException) when (
    operationTimeout.IsCancellationRequested &&
    !lifetime.ApplicationStopping.IsCancellationRequested)
{
    throw new TimeoutException(
        $"BunkFy migration operation exceeded {migrationsOptions.OperationTimeoutSeconds} seconds.");
}
finally
{
    if (started)
    {
        using CancellationTokenSource stopTimeout = new(TimeSpan.FromSeconds(10));
        await host.StopAsync(stopTimeout.Token).ConfigureAwait(false);
    }
}
