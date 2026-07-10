using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var postgreSql = builder.AddPostgres("postgres")
    .AddDatabase("PostgreSql");

bool sqlServerEnabled = bool.TryParse(
    builder.Configuration["AppHost:SqlServer:Enabled"],
    out bool configuredSqlServerEnabled) && configuredSqlServerEnabled;

var sqlServer = sqlServerEnabled
    ? builder.AddSqlServer("sql").AddDatabase("SqlServer")
    : null;

var nats = builder.AddNats("nats")
    .WithJetStream()
    .WithDataVolume(isReadOnly: false);

const string minioAccessKey = "minioadmin";
const string minioSecretKey = "minioadmin";
const string filesBucketName = "bunkfy-files";

var minio = builder.AddContainer("minio", "quay.io/minio/minio", "latest")
    .WithEnvironment("MINIO_ROOT_USER", minioAccessKey)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioSecretKey)
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithHttpEndpoint(targetPort: 9000, name: "api")
    .WithHttpEndpoint(targetPort: 9001, name: "console")
    .WithVolume("bunkfy-minio-data", "/data");

bool workerEnabled = bool.TryParse(
    builder.Configuration["AppHost:Worker:Enabled"],
    out bool configuredWorkerEnabled) && configuredWorkerEnabled;

var api = builder.AddProject<Projects.BunkFy_Host_Api>("bunkfy-host-api")
    .WithReference(postgreSql)
    .WithReference(nats)
    .WaitFor(postgreSql)
    .WaitFor(nats)
    .WaitFor(minio)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("FileManagement__Provider", "Minio")
    .WithEnvironment("FileManagement__Minio__Endpoint", "minio:9000")
    .WithEnvironment("FileManagement__Minio__AccessKey", minioAccessKey)
    .WithEnvironment("FileManagement__Minio__SecretKey", minioSecretKey)
    .WithEnvironment("FileManagement__Minio__BucketName", filesBucketName)
    .WithEnvironment("FileManagement__Minio__UseSsl", "false")
    .WithEnvironment("FileManagement__Minio__CreateBucketIfMissing", "true")
    .WithEnvironment("NatsJetStream__Enabled", workerEnabled ? "false" : "true");

if (sqlServer is { } configuredSqlServer)
{
    api.WithReference(configuredSqlServer)
        .WaitFor(configuredSqlServer);
}

bool adminApiEnabled = bool.TryParse(
    builder.Configuration["AppHost:AdminApi:Enabled"],
    out bool configuredAdminApiEnabled) && configuredAdminApiEnabled;

IResourceBuilder<ProjectResource>? adminApi = null;
if (adminApiEnabled)
{
    adminApi = builder.AddProject<Projects.BunkFy_Host_AdminApi>("bunkfy-host-admin-api")
        .WithReference(postgreSql)
        .WithReference(nats)
        .WaitFor(postgreSql)
        .WaitFor(nats)
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithEnvironment("NatsJetStream__Enabled", workerEnabled ? "false" : "true");

    if (sqlServer is { } configuredAdminSqlServer)
    {
        adminApi.WithReference(configuredAdminSqlServer)
            .WaitFor(configuredAdminSqlServer);
    }
}

IResourceBuilder<ProjectResource>? worker = null;
if (workerEnabled)
{
    worker = builder.AddProject<Projects.BunkFy_Host_Worker>("bunkfy-host-worker")
        .WithReference(postgreSql)
        .WithReference(nats)
        .WaitFor(postgreSql)
        .WaitFor(nats)
        .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
        .WithEnvironment("NatsJetStream__Enabled", "true")
        .WithEnvironment("NatsConsumers__Enabled", "true")
        .WithEnvironment("Tasks__Worker__Enabled", "true")
        .WithEnvironment("Worker__Modules__Auth", "true")
        .WithEnvironment("Worker__Modules__Properties", "true")
        .WithEnvironment("Worker__Modules__Inventory", "true")
        .WithEnvironment("Worker__Modules__Reservations", "true")
        .WithEnvironment("Worker__Modules__TaskRuntime", "true");

    if (sqlServer is { } configuredWorkerSqlServer)
    {
        worker.WithReference(configuredWorkerSqlServer)
            .WaitFor(configuredWorkerSqlServer);
    }
}

bool redisEnabled = bool.TryParse(
    builder.Configuration["AppHost:Redis:Enabled"],
    out bool configuredRedisEnabled) && configuredRedisEnabled;

if (redisEnabled)
{
    var redis = builder.AddRedis("redis");
    api.WithReference(redis)
        .WaitFor(redis)
        .WithEnvironment("Caching__Enabled", "true")
        .WithEnvironment("Caching__Provider", "Redis");

    if (adminApi is { } configuredAdminApi)
    {
        configuredAdminApi.WithReference(redis)
            .WaitFor(redis)
            .WithEnvironment("Caching__Enabled", "true")
            .WithEnvironment("Caching__Provider", "Redis");
    }

    if (worker is { } configuredWorker)
    {
        configuredWorker.WithReference(redis)
            .WaitFor(redis)
            .WithEnvironment("Caching__Enabled", "true")
            .WithEnvironment("Caching__Provider", "Redis");
    }
}

builder.Build().Run();
