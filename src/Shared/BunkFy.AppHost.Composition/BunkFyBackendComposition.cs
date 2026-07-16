namespace BunkFy.AppHost.Composition;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

public static class BunkFyBackendComposition
{
    private const string MinioAccessKey = "minioadmin";
    private const string MinioSecretKey = "minioadmin";
    private const string FilesBucketName = "bunkfy-files";

    public static BunkFyBackendResources AddBunkFyBackend(
        this IDistributedApplicationBuilder builder,
        BunkFyBackendProjectPaths projectPaths)
    {
        ArgumentNullException.ThrowIfNull(builder);
        projectPaths = projectPaths?.Validate() ?? throw new ArgumentNullException(nameof(projectPaths));

        IResourceBuilder<ParameterResource> postgreSqlPassword = builder.AddParameter(
            "postgres-password",
            "postgres",
            secret: true);
        IResourceBuilder<PostgresDatabaseResource> postgreSql = builder
            .AddPostgres("postgres", password: postgreSqlPassword)
            .WithDataVolume(isReadOnly: false)
            .AddDatabase("PostgreSql");

        bool sqlServerEnabled = IsEnabled(builder, "AppHost:SqlServer:Enabled");
        IResourceBuilder<SqlServerDatabaseResource>? sqlServer = sqlServerEnabled
            ? builder.AddSqlServer("sql").AddDatabase("SqlServer")
            : null;

        IResourceBuilder<NatsServerResource> nats = builder.AddNats("nats")
            .WithJetStream()
            .WithDataVolume(isReadOnly: false);

        IResourceBuilder<ContainerResource> minio = builder
            .AddContainer("minio", "quay.io/minio/minio", "latest")
            .WithEnvironment("MINIO_ROOT_USER", MinioAccessKey)
            .WithEnvironment("MINIO_ROOT_PASSWORD", MinioSecretKey)
            .WithArgs("server", "/data", "--console-address", ":9001")
            .WithHttpEndpoint(targetPort: 9000, name: "api")
            .WithHttpEndpoint(targetPort: 9001, name: "console")
            .WithVolume("bunkfy-minio-data", "/data");

        bool workerEnabled = IsEnabled(builder, "AppHost:Worker:Enabled");
        IResourceBuilder<ProjectResource> api = builder
            .AddProject("bunkfy-host-api", projectPaths.Api)
            .WithReference(postgreSql)
            .WithReference(nats)
            .WaitFor(postgreSql)
            .WaitFor(nats)
            .WaitFor(minio)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithMinioFileStorage()
            .WithEnvironment("Notifications__Enabled", "true")
            .WithEnvironment("NatsJetStream__Enabled", workerEnabled ? "false" : "true")
            .WithHttpHealthCheck("/health");

        if (sqlServer is { } configuredSqlServer)
        {
            api.WithReference(configuredSqlServer).WaitFor(configuredSqlServer);
        }

        IResourceBuilder<ProjectResource>? adminApi = AddAdminApi(
            builder,
            projectPaths.AdminApi,
            workerEnabled,
            postgreSql,
            nats,
            minio,
            sqlServer);
        IResourceBuilder<ProjectResource>? worker = AddWorker(
            builder,
            projectPaths.Worker,
            workerEnabled,
            postgreSql,
            nats,
            minio,
            sqlServer);

        if (IsEnabled(builder, "AppHost:Redis:Enabled"))
        {
            IResourceBuilder<RedisResource> redis = builder.AddRedis("redis");
            api.WithReference(redis).WaitFor(redis).WithRedisCaching();
            adminApi?.WithReference(redis).WaitFor(redis).WithRedisCaching();
            worker?.WithReference(redis).WaitFor(redis).WithRedisCaching();
        }

        return new BunkFyBackendResources(api);
    }

    private static IResourceBuilder<ProjectResource>? AddAdminApi(
        IDistributedApplicationBuilder builder,
        string projectPath,
        bool workerEnabled,
        IResourceBuilder<PostgresDatabaseResource> postgreSql,
        IResourceBuilder<NatsServerResource> nats,
        IResourceBuilder<ContainerResource> minio,
        IResourceBuilder<SqlServerDatabaseResource>? sqlServer)
    {
        if (!IsEnabled(builder, "AppHost:AdminApi:Enabled"))
        {
            return null;
        }

        IResourceBuilder<ProjectResource> adminApi = builder
            .AddProject("bunkfy-host-admin-api", projectPath)
            .WithReference(postgreSql)
            .WithReference(nats)
            .WaitFor(postgreSql)
            .WaitFor(nats)
            .WaitFor(minio)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithMinioFileStorage()
            .WithEnvironment("NatsJetStream__Enabled", workerEnabled ? "false" : "true")
            .WithHttpHealthCheck("/health");
        if (sqlServer is { } configuredSqlServer)
        {
            adminApi.WithReference(configuredSqlServer).WaitFor(configuredSqlServer);
        }

        return adminApi;
    }

    private static IResourceBuilder<ProjectResource>? AddWorker(
        IDistributedApplicationBuilder builder,
        string projectPath,
        bool workerEnabled,
        IResourceBuilder<PostgresDatabaseResource> postgreSql,
        IResourceBuilder<NatsServerResource> nats,
        IResourceBuilder<ContainerResource> minio,
        IResourceBuilder<SqlServerDatabaseResource>? sqlServer)
    {
        if (!workerEnabled)
        {
            return null;
        }

        IResourceBuilder<ProjectResource> worker = builder
            .AddProject("bunkfy-host-worker", projectPath)
            .WithReference(postgreSql)
            .WithReference(nats)
            .WaitFor(postgreSql)
            .WaitFor(nats)
            .WaitFor(minio)
            .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
            .WithMinioFileStorage()
            .WithEnvironment("NatsJetStream__Enabled", "true")
            .WithEnvironment("NatsConsumers__Enabled", "true")
            .WithEnvironment("Tasks__Worker__Enabled", "true")
            .WithEnvironment("Tasks__Scheduler__Enabled", "true")
            .WithEnvironment("Worker__Modules__AccessControl", "true")
            .WithEnvironment("Worker__Modules__Auth", "true")
            .WithEnvironment("Worker__Modules__Notifications", "true")
            .WithEnvironment("Worker__Modules__Organizations", "true")
            .WithEnvironment("Worker__Modules__Properties", "true")
            .WithEnvironment("Worker__Modules__Inventory", "true")
            .WithEnvironment("Worker__Modules__Reservations", "true")
            .WithEnvironment("Worker__Modules__Guests", "true")
            .WithEnvironment("Worker__Modules__Staff", "true")
            .WithEnvironment("Worker__Modules__Ingestion", "true")
            .WithEnvironment("Worker__Modules__TaskRuntime", "true")
            .WithEnvironment("Tasks__Worker__WorkerGroups__0", "default")
            .WithEnvironment("Tasks__Worker__WorkerGroups__1", "projection-workers")
            .WithEnvironment("Tasks__Worker__WorkerGroups__2", "reminder-workers")
            .WithEnvironment("Tasks__Worker__WorkerGroups__3", "ingestion-adapters")
            .WithEnvironment("Tasks__Worker__WorkerGroups__4", "ingestion-maintenance");
        if (sqlServer is { } configuredSqlServer)
        {
            worker.WithReference(configuredSqlServer).WaitFor(configuredSqlServer);
        }

        return worker;
    }

    private static IResourceBuilder<T> WithMinioFileStorage<T>(this IResourceBuilder<T> resource)
        where T : IResourceWithEnvironment => resource
        .WithEnvironment("FileManagement__Provider", "Minio")
        .WithEnvironment("FileManagement__Minio__Endpoint", "minio:9000")
        .WithEnvironment("FileManagement__Minio__AccessKey", MinioAccessKey)
        .WithEnvironment("FileManagement__Minio__SecretKey", MinioSecretKey)
        .WithEnvironment("FileManagement__Minio__BucketName", FilesBucketName)
        .WithEnvironment("FileManagement__Minio__UseSsl", "false")
        .WithEnvironment("FileManagement__Minio__CreateBucketIfMissing", "true");

    private static IResourceBuilder<T> WithRedisCaching<T>(this IResourceBuilder<T> resource)
        where T : IResourceWithEnvironment => resource
        .WithEnvironment("Caching__Enabled", "true")
        .WithEnvironment("Caching__Provider", "Redis");

    private static bool IsEnabled(IDistributedApplicationBuilder builder, string key) =>
        bool.TryParse(builder.Configuration[key], out bool enabled) && enabled;
}
