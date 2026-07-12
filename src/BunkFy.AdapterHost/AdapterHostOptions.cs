namespace BunkFy.AdapterHost;

using BunkFy.Adapter.Abstractions;
using BunkFy.Adapter.Runtime;
using BunkFy.Adapters.Http;
using BunkFy.Adapters.JsonFileDrop;
using Microsoft.Extensions.Configuration;

public enum AdapterHostCoordinationMode
{
    LocalFile = 1,
    ServerLease = 2
}

public sealed record AdapterHostOptions
{
    public const string SectionName = "AdapterHost";

    private AdapterHostOptions() { }

    public required string AdapterType { get; init; }
    public required string TenantId { get; init; }
    public required Guid PropertyId { get; init; }
    public required Guid ConnectionId { get; init; }
    public required AdapterHostCoordinationMode CoordinationMode { get; init; }
    public Guid? WorkerId { get; init; }
    public required Uri ServiceBaseAddress { get; init; }
    public string? CheckpointFilePath { get; init; }
    public required string ConfigurationFilePath { get; init; }
    public string ConfigurationContentType { get; init; } = "application/json";
    public string? SecretFilePath { get; init; }
    public string? SecretContentType { get; init; }
    public string? IngressTokenEnvironmentVariable { get; init; }
    public string? IngressTokenFilePath { get; init; }
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan MaximumRunDuration { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan RemoteLeaseDuration { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan RetryMaxDelay { get; init; } = TimeSpan.FromMinutes(5);
    public bool RunOnStart { get; init; } = true;
    public bool AllowInsecureLoopback { get; init; }
    public string ListenUrl { get; init; } = "http://127.0.0.1:8088";
    public string? JsonFileDropRoot { get; init; }
    public bool JsonFileDropRetentionEnabled { get; init; } = true;
    public TimeSpan JsonFileDropProcessedArchiveRetention { get; init; } =
        JsonFileDropAdapterOptions.DefaultProcessedArchiveRetention;
    public TimeSpan JsonFileDropFailedQuarantineRetention { get; init; } =
        JsonFileDropAdapterOptions.DefaultFailedQuarantineRetention;
    public int JsonFileDropMaximumDeletesPerRun { get; init; } =
        JsonFileDropAdapterOptions.DefaultMaximumDeletesPerRun;

    public static AdapterHostOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        IConfigurationSection section = configuration.GetRequiredSection(SectionName);
        string adapterType = Required(section["AdapterType"], "AdapterHost:AdapterType")
            .ToLowerInvariant();
        string tenantId = Required(section["TenantId"], "AdapterHost:TenantId");
        if (tenantId.Length > 128 || tenantId.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new InvalidOperationException("AdapterHost:TenantId is invalid.");
        }

        Guid propertyId = RequiredGuid(section["PropertyId"], "AdapterHost:PropertyId");
        Guid connectionId = RequiredGuid(section["ConnectionId"], "AdapterHost:ConnectionId");
        AdapterHostCoordinationMode coordinationMode = section["CoordinationMode"]?.Trim().ToLowerInvariant() switch
        {
            "local-file" => AdapterHostCoordinationMode.LocalFile,
            "server-lease" => AdapterHostCoordinationMode.ServerLease,
            _ => throw new InvalidOperationException(
                "AdapterHost:CoordinationMode must be local-file or server-lease.")
        };
        Guid? workerId = coordinationMode == AdapterHostCoordinationMode.ServerLease
            ? RequiredGuid(section["WorkerId"], "AdapterHost:WorkerId")
            : null;
        Uri serviceBaseAddress = RequiredUri(section["ServiceBaseAddress"], "AdapterHost:ServiceBaseAddress");
        string? checkpointPath = coordinationMode == AdapterHostCoordinationMode.LocalFile
            ? RequiredPath(section["CheckpointFilePath"], "AdapterHost:CheckpointFilePath")
            : OptionalPath(section["CheckpointFilePath"]);
        string configurationPath = RequiredPath(
            section["ConfigurationFilePath"],
            "AdapterHost:ConfigurationFilePath");
        string? secretPath = OptionalPath(section["SecretFilePath"]);
        string? tokenFilePath = OptionalPath(section["IngressTokenFilePath"]);
        string? tokenEnvironmentVariable = Optional(section["IngressTokenEnvironmentVariable"]);
        if ((tokenFilePath is null) == (tokenEnvironmentVariable is null))
        {
            throw new InvalidOperationException(
                "Exactly one AdapterHost ingress token file or environment variable must be configured.");
        }

        if (tokenEnvironmentVariable is not null &&
            (tokenEnvironmentVariable.Length > 128 || tokenEnvironmentVariable.Any(character =>
                !char.IsLetterOrDigit(character) && character != '_')))
        {
            throw new InvalidOperationException(
                "AdapterHost:IngressTokenEnvironmentVariable is invalid.");
        }

        TimeSpan pollInterval = section.GetValue("PollInterval", TimeSpan.FromMinutes(5));
        TimeSpan maximumRunDuration = section.GetValue("MaximumRunDuration", TimeSpan.FromMinutes(5));
        TimeSpan remoteLeaseDuration = section.GetValue("RemoteLeaseDuration", TimeSpan.FromMinutes(2));
        TimeSpan retryBaseDelay = section.GetValue("RetryBaseDelay", TimeSpan.FromSeconds(10));
        TimeSpan retryMaxDelay = section.GetValue("RetryMaxDelay", TimeSpan.FromMinutes(5));
        if (pollInterval < TimeSpan.FromSeconds(1) || pollInterval > TimeSpan.FromDays(30) ||
            maximumRunDuration < TimeSpan.FromSeconds(30) || maximumRunDuration > TimeSpan.FromHours(23) ||
            retryBaseDelay < TimeSpan.FromSeconds(1) || retryBaseDelay > TimeSpan.FromHours(1) ||
            retryMaxDelay < retryBaseDelay || retryMaxDelay > TimeSpan.FromHours(1))
        {
            throw new InvalidOperationException("AdapterHost timing configuration is invalid.");
        }

        if (coordinationMode == AdapterHostCoordinationMode.ServerLease &&
            (remoteLeaseDuration < TimeSpan.FromSeconds(AdapterRemoteLeaseContractLimits.MinimumLeaseSeconds) ||
             remoteLeaseDuration > TimeSpan.FromSeconds(AdapterRemoteLeaseContractLimits.MaximumLeaseSeconds) ||
             remoteLeaseDuration.TotalSeconds != Math.Truncate(remoteLeaseDuration.TotalSeconds)))
        {
            throw new InvalidOperationException("AdapterHost:RemoteLeaseDuration is invalid.");
        }

        string listenUrl = Required(section["ListenUrl"] ?? "http://127.0.0.1:8088", "AdapterHost:ListenUrl");
        if (!Uri.TryCreate(listenUrl, UriKind.Absolute, out Uri? listenUri) ||
            listenUri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(listenUri.UserInfo) ||
            !string.IsNullOrEmpty(listenUri.Query) ||
            !string.IsNullOrEmpty(listenUri.Fragment))
        {
            throw new InvalidOperationException("AdapterHost:ListenUrl is invalid.");
        }

        string configurationContentType = Required(
            section["ConfigurationContentType"] ?? "application/json",
            "AdapterHost:ConfigurationContentType").ToLowerInvariant();
        string? secretContentType = Optional(section["SecretContentType"])?.ToLowerInvariant();
        if (secretPath is not null && secretContentType is null)
        {
            throw new InvalidOperationException(
                "AdapterHost:SecretContentType is required with a secret file.");
        }

        bool allowInsecureLoopback = section.GetValue("AllowInsecureLoopback", false);
        _ = new AdapterHttpIngressOptions(
            serviceBaseAddress,
            tenantId,
            connectionId,
            allowInsecureLoopback: allowInsecureLoopback);

        string? jsonFileDropRoot = OptionalPath(section["JsonFileDropRoot"]);
        bool jsonFileDropRetentionEnabled = section.GetValue("JsonFileDropRetentionEnabled", true);
        TimeSpan jsonFileDropProcessedRetention = section.GetValue(
            "JsonFileDropProcessedArchiveRetention",
            JsonFileDropAdapterOptions.DefaultProcessedArchiveRetention);
        TimeSpan jsonFileDropFailedRetention = section.GetValue(
            "JsonFileDropFailedQuarantineRetention",
            JsonFileDropAdapterOptions.DefaultFailedQuarantineRetention);
        int jsonFileDropMaximumDeletes = section.GetValue(
            "JsonFileDropMaximumDeletesPerRun",
            JsonFileDropAdapterOptions.DefaultMaximumDeletesPerRun);
        if (string.Equals(adapterType, JsonFileDropAdapterDescriptor.AdapterType, StringComparison.Ordinal))
        {
            _ = new JsonFileDropAdapterOptions(
                jsonFileDropRoot ?? throw new InvalidOperationException(
                    "AdapterHost:JsonFileDropRoot is required for json.file-drop."),
                jsonFileDropProcessedRetention,
                jsonFileDropFailedRetention,
                jsonFileDropMaximumDeletes,
                jsonFileDropRetentionEnabled);
        }

        return new AdapterHostOptions
        {
            AdapterType = adapterType,
            TenantId = tenantId,
            PropertyId = propertyId,
            ConnectionId = connectionId,
            CoordinationMode = coordinationMode,
            WorkerId = workerId,
            ServiceBaseAddress = serviceBaseAddress,
            CheckpointFilePath = checkpointPath,
            ConfigurationFilePath = configurationPath,
            ConfigurationContentType = configurationContentType,
            SecretFilePath = secretPath,
            SecretContentType = secretContentType,
            IngressTokenEnvironmentVariable = tokenEnvironmentVariable,
            IngressTokenFilePath = tokenFilePath,
            PollInterval = pollInterval,
            MaximumRunDuration = maximumRunDuration,
            RemoteLeaseDuration = remoteLeaseDuration,
            RetryBaseDelay = retryBaseDelay,
            RetryMaxDelay = retryMaxDelay,
            RunOnStart = section.GetValue("RunOnStart", true),
            AllowInsecureLoopback = allowInsecureLoopback,
            ListenUrl = listenUri.AbsoluteUri,
            JsonFileDropRoot = jsonFileDropRoot,
            JsonFileDropRetentionEnabled = jsonFileDropRetentionEnabled,
            JsonFileDropProcessedArchiveRetention = jsonFileDropProcessedRetention,
            JsonFileDropFailedQuarantineRetention = jsonFileDropFailedRetention,
            JsonFileDropMaximumDeletesPerRun = jsonFileDropMaximumDeletes
        };
    }

    public AdapterRuntimeIdentity CreateRuntimeIdentity() => new(
        this.TenantId,
        this.PropertyId,
        this.ConnectionId,
        this.AdapterType,
        this.MaximumRunDuration.Add(TimeSpan.FromSeconds(30)));

    private static string Required(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"{name} is required.");

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid RequiredGuid(string? value, string name) =>
        Guid.TryParse(value, out Guid parsed) && parsed != Guid.Empty
            ? parsed
            : throw new InvalidOperationException($"{name} is invalid.");

    private static Uri RequiredUri(string? value, string name) =>
        Uri.TryCreate(Required(value, name), UriKind.Absolute, out Uri? parsed)
            ? parsed
            : throw new InvalidOperationException($"{name} is invalid.");

    private static string RequiredPath(string? value, string name) =>
        Path.GetFullPath(Required(value, name), AppContext.BaseDirectory);

    private static string? OptionalPath(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Path.GetFullPath(value.Trim(), AppContext.BaseDirectory);
}
