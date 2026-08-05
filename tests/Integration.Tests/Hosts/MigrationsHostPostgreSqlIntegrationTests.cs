namespace Integration.Tests.Hosts;

using BunkFy.Host.Migrations;
using Gma.Modules.Auth.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class MigrationsHostPostgreSqlIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Host_resumes_a_partial_catalogue_plans_idempotently_and_serializes_apply()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_migrations_host_tests")
                .Build();
        await postgreSql.StartAsync();

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            postgreSql.GetConnectionString();
        builder.AddBunkFyMigrationPersistence(AuthProfile.DefaultGlobalScopeId);

        using IHost host = builder.Build();
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IReadOnlyList<BunkFyMigrationModule> modules =
            BunkFyMigrationCatalog.Resolve(scope.ServiceProvider);

        await modules[0].Context.Database.MigrateAsync();
        string[] partiallyApplied =
        [.. await modules[0].Context.Database.GetAppliedMigrationsAsync()];
        Assert.NotEmpty(partiallyApplied);

        MigrationsProductionAdmissionOptions admission =
            MigrationsProductionAdmissionOptions.FromConfiguration(
                new ConfigurationBuilder().Build());
        BunkFyMigrationPlan completed = await new BunkFyMigrationCoordinator(
                NullLogger.Instance,
                Runtime(MigrationExecutionMode.Apply),
                admission,
                isProduction: false)
            .RunAsync(modules, CancellationToken.None);

        Assert.Equal(15, completed.Modules.Count);
        Assert.True(completed.TargetMigrationCount > partiallyApplied.Length);
        Assert.Equal(completed.TargetMigrationCount, completed.AppliedMigrationCount);
        Assert.Equal(0, completed.PendingMigrationCount);

        BunkFyMigrationPlan planned = await new BunkFyMigrationCoordinator(
                NullLogger.Instance,
                Runtime(MigrationExecutionMode.Plan),
                admission,
                isProduction: false)
            .RunAsync(modules, CancellationToken.None);

        Assert.Equal(completed.TargetCatalogSha256, planned.TargetCatalogSha256);
        Assert.Equal(completed.CurrentStateSha256, planned.CurrentStateSha256);
        Assert.Equal(0, planned.PendingMigrationCount);

        await using NpgsqlConnection competingLock = new(
            postgreSql.GetConnectionString());
        await competingLock.OpenAsync();
        await using (NpgsqlCommand acquire = new(
            "SELECT pg_advisory_lock(@key);",
            competingLock))
        {
            acquire.Parameters.AddWithValue(
                "key",
                PostgreSqlMigrationLock.MigrationLockKey);
            await acquire.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<TimeoutException>(
            () => new BunkFyMigrationCoordinator(
                    NullLogger.Instance,
                    Runtime(MigrationExecutionMode.Plan, lockTimeoutSeconds: 1),
                    admission,
                    isProduction: false)
                .RunAsync(modules, CancellationToken.None));
    }

    private static MigrationsHostOptions Runtime(
        MigrationExecutionMode mode,
        int lockTimeoutSeconds = 60)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Migrations:Mode"] = mode.ToString(),
                ["Migrations:LockAcquireTimeoutSeconds"] =
                    lockTimeoutSeconds.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                ["Migrations:OperationTimeoutSeconds"] = "1800",
                ["Migrations:CommandTimeoutSeconds"] = "300"
            })
            .Build();
        return MigrationsHostOptions.FromConfiguration(configuration);
    }
}
