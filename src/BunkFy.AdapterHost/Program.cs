using BunkFy.Adapter.Abstractions;
using BunkFy.Adapter.Runtime;
using BunkFy.Adapters.FakeHttp;
using BunkFy.Adapters.Http;
using BunkFy.Adapters.ImapReservationMail;
using BunkFy.Adapters.JsonFileDrop;
using BunkFy.AdapterHost;

WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
AdapterHostOptions options = AdapterHostOptions.FromConfiguration(builder.Configuration);
builder.WebHost.UseUrls(options.ListenUrl);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(options.CreateRuntimeIdentity());
if (options.CoordinationMode == AdapterHostCoordinationMode.LocalFile)
{
    builder.Services.AddSingleton<IAdapterCheckpointStore>(new FileAdapterCheckpointStore(
        options.CheckpointFilePath!));
}
builder.Services.AddSingleton<IAdapterRuntimeMaterialProvider>(new FileAdapterRuntimeMaterialProvider(
    new FileAdapterRuntimeMaterialOptions(
        options.ConfigurationFilePath,
        options.ConfigurationContentType,
        options.SecretFilePath,
        options.SecretContentType)));
builder.Services.AddSingleton<IAdapterIngressTokenProvider, ReloadingAdapterIngressTokenProvider>();
builder.Services.AddHttpClient("adapter-ingress", client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton(provider => new AdapterHttpIngressClient(
    provider.GetRequiredService<IHttpClientFactory>().CreateClient("adapter-ingress"),
    new AdapterHttpIngressOptions(
        options.ServiceBaseAddress,
        options.TenantId,
        options.ConnectionId,
        allowInsecureLoopback: options.AllowInsecureLoopback),
    provider.GetRequiredService<IAdapterIngressTokenProvider>()));
builder.Services.AddSingleton<IAdapterPushObservationSink>(provider =>
    provider.GetRequiredService<AdapterHttpIngressClient>());
builder.Services.AddSingleton<IAdapterRemoteControlClient>(provider =>
    provider.GetRequiredService<AdapterHttpIngressClient>());

switch (options.AdapterType)
{
    case "fake.http":
        builder.Services.AddFakeHttpAdapter();
        break;
    case ImapReservationMailAdapterDescriptor.AdapterType:
        builder.Services.AddImapReservationMailAdapter();
        break;
    case JsonFileDropAdapterDescriptor.AdapterType:
        if (options.JsonFileDropRoot is null)
        {
            throw new InvalidOperationException(
                "AdapterHost:JsonFileDropRoot is required for json.file-drop.");
        }

        builder.Services.AddJsonFileDropAdapter(new JsonFileDropAdapterOptions(
            options.JsonFileDropRoot,
            options.JsonFileDropProcessedArchiveRetention,
            options.JsonFileDropFailedQuarantineRetention,
            options.JsonFileDropMaximumDeletesPerRun,
            options.JsonFileDropRetentionEnabled));
        break;
    default:
        throw new InvalidOperationException("The configured adapter type is not registered in this host build.");
}

builder.Services.AddSingleton<AdapterHostStatus>();
if (options.CoordinationMode == AdapterHostCoordinationMode.ServerLease)
{
    builder.Services.AddHostedService<RemoteAdapterPollingService>();
}
else
{
    builder.Services.AddHostedService<StandaloneAdapterPollingService>();
}

WebApplication app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", (AdapterHostStatus status) =>
    status.Snapshot().Ready
        ? Results.Ok(new { status = "ready" })
        : Results.Json(new { status = "starting" }, statusCode: StatusCodes.Status503ServiceUnavailable));
app.MapGet("/status", (AdapterHostStatus status) => Results.Ok(status.Snapshot()));
await app.RunAsync().ConfigureAwait(false);
