namespace Integration.Tests.Support;

using BunkFy.Host.Api;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Modules.Auth.Application.Ports;
using Gma.Modules.AccessControl.Persistence;
using Gma.Modules.Auth.Persistence;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Inventory.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NATS.Client.Core;
using BunkFy.Modules.Properties.Persistence;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Staff.Persistence;

internal sealed class AuthTestApplication(
    string provider,
    string providerConnectionString,
    string natsConnectionString,
    bool disableOutboxPublisher = true,
    bool enablePrometheus = false,
    string minioEndpoint = "localhost:9000",
    string minioAccessKey = "minioadmin",
    string minioSecretKey = "minioadmin",
    string minioBucketName = "integration-test-files",
    bool minioCreateBucketIfMissing = false)
    : WebApplicationFactory<ApiAssemblyReference>
{
    private const string JwtIssuer = "BunkFy";
    private const string JwtAudience = "BunkFy";
    private const string JwtSigningKey = "integration-test-signing-key-change-me-000000000000000000";
    private const string RefreshTokenPepper = "integration-test-refresh-token-pepper-change-me-000000000000000000";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Integration");
        builder.UseSetting("Persistence:Provider", provider);
        builder.UseSetting("ConnectionStrings:SqlServer", provider == "SqlServer" ? providerConnectionString : string.Empty);
        builder.UseSetting("ConnectionStrings:PostgreSql", provider == "PostgreSql" ? providerConnectionString : string.Empty);
        builder.UseSetting("ConnectionStrings:nats", natsConnectionString);
        builder.UseSetting("NatsJetStream:Enabled", disableOutboxPublisher ? "false" : "true");
        builder.UseSetting("Tenancy:Enabled", "true");
        builder.UseSetting("Outbox:PollIntervalMilliseconds", "100");
        builder.UseSetting("Outbox:LockDurationMilliseconds", "1000");
        builder.UseSetting("Observability:Prometheus:Enabled", enablePrometheus.ToString());
        builder.UseSetting("Caching:Enabled", "false");
        builder.UseSetting("FileManagement:Minio:Endpoint", minioEndpoint);
        builder.UseSetting("FileManagement:Minio:AccessKey", minioAccessKey);
        builder.UseSetting("FileManagement:Minio:SecretKey", minioSecretKey);
        builder.UseSetting("FileManagement:Minio:BucketName", minioBucketName);
        builder.UseSetting("FileManagement:Minio:UseSsl", "false");
        builder.UseSetting("FileManagement:Minio:CreateBucketIfMissing", minioCreateBucketIfMissing.ToString());
        builder.UseSetting("FileManagement:AllowedContentTypes:0", "application/json");
        builder.UseSetting("FileManagement:AllowedContentTypes:1", "message/rfc822");
        builder.UseSetting("Auth:Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Auth:Jwt:Audience", JwtAudience);
        builder.UseSetting("Auth:Jwt:SigningKey", JwtSigningKey);
        builder.UseSetting("Auth:Jwt:AccessTokenLifetimeMinutes", "15");
        builder.UseSetting("Auth:RefreshTokens:Pepper", RefreshTokenPepper);
        builder.UseSetting("Auth:SelfRegistration:PasswordEnabled", "true");
        builder.UseSetting("Auth:SelfRegistration:ExternalEnabled", "true");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Persistence:Provider"] = provider,
                ["ConnectionStrings:SqlServer"] = provider == "SqlServer" ? providerConnectionString : string.Empty,
                ["ConnectionStrings:PostgreSql"] = provider == "PostgreSql" ? providerConnectionString : string.Empty,
                ["ConnectionStrings:nats"] = natsConnectionString,
                ["Auth:RefreshTokens:Pepper"] = RefreshTokenPepper,
                ["Auth:Jwt:Issuer"] = JwtIssuer,
                ["Auth:Jwt:Audience"] = JwtAudience,
                ["Auth:Jwt:SigningKey"] = JwtSigningKey,
                ["Auth:Jwt:AccessTokenLifetimeMinutes"] = "15",
                ["Auth:SelfRegistration:PasswordEnabled"] = "true",
                ["Auth:SelfRegistration:ExternalEnabled"] = "true",
                ["NatsJetStream:Enabled"] = disableOutboxPublisher ? "false" : "true",
                ["Tenancy:Enabled"] = "true",
                ["Outbox:PollIntervalMilliseconds"] = "100",
                ["Outbox:LockDurationMilliseconds"] = "1000",
                ["Observability:Prometheus:Enabled"] = enablePrometheus.ToString(),
                ["FileManagement:Minio:Endpoint"] = minioEndpoint,
                ["FileManagement:Minio:AccessKey"] = minioAccessKey,
                ["FileManagement:Minio:SecretKey"] = minioSecretKey,
                ["FileManagement:Minio:BucketName"] = minioBucketName,
                ["FileManagement:Minio:UseSsl"] = "false",
                ["FileManagement:Minio:CreateBucketIfMissing"] = minioCreateBucketIfMissing.ToString(),
                ["FileManagement:AllowedContentTypes:0"] = "application/json",
                ["FileManagement:AllowedContentTypes:1"] = "message/rfc822",
            };

            configuration.AddInMemoryCollection(values);
        });

        builder.ConfigureServices(services =>
        {
            if (disableOutboxPublisher)
            {
                ServiceDescriptor[] hostedServicesToRemove = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType?.Name == "OutboxPublisherService")
                    .ToArray();

                foreach (ServiceDescriptor hostedService in hostedServicesToRemove)
                {
                    services.Remove(hostedService);
                }
            }

            services.RemoveAll<INatsConnection>();
            services.AddSingleton<INatsConnection>(_ => new NatsConnection(new NatsOpts
            {
                Url = natsConnectionString,
            }));
        });
    }

    public async Task MigrateDatabaseAsync()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = provider,
                ["ConnectionStrings:SqlServer"] = provider == "SqlServer" ? providerConnectionString : string.Empty,
                ["ConnectionStrings:PostgreSql"] = provider == "PostgreSql" ? providerConnectionString : string.Empty,
            })
            .Build();
        DbContextOptionsBuilder<AuthDbContext> options = new();
        options.UseConfiguredProvider(
            configuration,
            AuthMigrations.SqlServerAssembly,
            AuthMigrations.PostgreSqlAssembly,
            AuthMigrations.Schema,
            AuthMigrations.HistoryTable);

        await using AuthDbContext dbContext = new(options.Options, DisabledTenantContext.Instance);
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task MigratePropertiesAuthorizationDatabaseAsync()
    {
        using IServiceScope scope = this.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AccessControlDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<AuthDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<PropertiesDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task MigrateInventoryAuthorizationDatabaseAsync()
    {
        await this.MigratePropertiesAuthorizationDatabaseAsync().ConfigureAwait(false);
        using IServiceScope scope = this.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<InventoryDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task MigrateReservationsAuthorizationDatabaseAsync()
    {
        await this.MigrateInventoryAuthorizationDatabaseAsync().ConfigureAwait(false);
        using IServiceScope scope = this.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReservationsDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task MigrateGuestRecordsAuthorizationDatabaseAsync()
    {
        await this.MigrateReservationsAuthorizationDatabaseAsync().ConfigureAwait(false);
        using IServiceScope scope = this.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<GuestsDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task MigrateStaffAuthorizationDatabaseAsync()
    {
        await this.MigratePropertiesAuthorizationDatabaseAsync().ConfigureAwait(false);
        using IServiceScope scope = this.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StaffDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task MigrateIngestionDatabaseAsync()
    {
        using IServiceScope scope = this.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IngestionDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task<Guid> AddOutboxMessageAsync(DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        AuthDbContext dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Guid id = Guid.NewGuid();

        dbContext.OutboxMessages.Add(new Gma.Framework.Messaging.Infrastructure.OutboxMessage(
            id,
            "gma.auth.test.v1",
            "Integration.Tests.TestEvent",
            1,
            "tenant-outbox",
            nowUtc,
            "{}",
            nowUtc));

        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        return id;
    }

    public async Task<IReadOnlyList<OutboxMessageRecord>> ClaimOutboxAsync(
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan lockDuration)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IOutboxStore store = scope.ServiceProvider.GetServices<IOutboxStore>().Single(item => item.ModuleName == "auth");

        return await store.ClaimPendingAsync(25, workerId, nowUtc, lockDuration, CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task MarkOutboxProcessedAsync(Guid id, string workerId, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IOutboxStore store = scope.ServiceProvider.GetServices<IOutboxStore>().Single(item => item.ModuleName == "auth");

        await store.MarkProcessedAsync(id, workerId, nowUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task MarkOutboxFailedAsync(Guid id, string workerId, string error, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IOutboxStore store = scope.ServiceProvider.GetServices<IOutboxStore>().Single(item => item.ModuleName == "auth");

        await store.MarkFailedAsync(id, workerId, error, nowUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task<int> CountPendingOutboxMessagesAsync()
    {
        using IServiceScope scope = this.Services.CreateScope();
        AuthDbContext dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        return await dbContext.OutboxMessages
            .CountAsync(message => message.ProcessedAtUtc == null)
            .ConfigureAwait(false);
    }

    public async Task<int> CountProcessedOutboxMessagesAsync()
    {
        using IServiceScope scope = this.Services.CreateScope();
        AuthDbContext dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        return await dbContext.OutboxMessages
            .CountAsync(message => message.ProcessedAtUtc != null)
            .ConfigureAwait(false);
    }

    public async Task<int> WaitForProcessedOutboxMessagesAsync(int expectedCount, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            int processed = await this.CountProcessedOutboxMessagesAsync().ConfigureAwait(false);

            if (processed >= expectedCount)
            {
                return processed;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        return await this.CountProcessedOutboxMessagesAsync().ConfigureAwait(false);
    }

    public async Task<OutboxSnapshot> GetOutboxSnapshotAsync(Guid id)
    {
        using IServiceScope scope = this.Services.CreateScope();
        AuthDbContext dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        OutboxSnapshot? snapshot = await dbContext.OutboxMessages
            .Where(message => message.Id == id)
            .Select(message => new OutboxSnapshot(
                message.Id,
                message.ProcessedAtUtc,
                message.LockedBy,
                message.LockedUntilUtc,
                message.NextAttemptAtUtc,
                message.Attempts))
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);

        Xunit.Assert.NotNull(snapshot);
        return snapshot;
    }

    private sealed class DisabledTenantContext : IAuthScopeContext
    {
        public static readonly DisabledTenantContext Instance = new();

        public bool IsEnabled => false;
        public string? ScopeId => null;
        public bool TryRestoreScope(string? scopeId) => true;
    }
}
