namespace BunkFy.AdapterHost;

using Microsoft.Extensions.Configuration;

public enum AdapterHostProductionApprovalState
{
    Pending = 0,
    Approved = 1
}

public enum AdapterHostDeploymentProfile
{
    Unspecified = 0,
    SelfHosted = 1,
    Hosted = 2
}

public enum AdapterHostRuntimeKind
{
    Unspecified = 0,
    Process = 1,
    Container = 2
}

public enum AdapterHostStatusEndpointExposure
{
    Unspecified = 0,
    Disabled = 1,
    LoopbackOnly = 2
}

public sealed record AdapterHostProductionAdmissionOptions
{
    public const string SectionName = "AdapterHost:ProductionAdmission";

    private AdapterHostProductionAdmissionOptions() { }

    public AdapterHostProductionApprovalState ApprovalState { get; init; }
    public string? ApprovalReference { get; init; }
    public AdapterHostDeploymentProfile DeploymentProfile { get; init; }
    public AdapterHostRuntimeKind Runtime { get; init; }
    public string? SourceCommitSha { get; init; }
    public string? ContainerImageDigest { get; init; }
    public string? ApprovedAdapterType { get; init; }
    public string? ApprovedServiceBaseAddress { get; init; }
    public AdapterHostStatusEndpointExposure StatusEndpointExposure { get; init; }

    public static AdapterHostProductionAdmissionOptions FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        IConfigurationSection section = configuration.GetSection(SectionName);
        return new AdapterHostProductionAdmissionOptions
        {
            ApprovalState = Parse(
                section["ApprovalState"],
                AdapterHostProductionApprovalState.Pending,
                "AdapterHost:ProductionAdmission:ApprovalState"),
            ApprovalReference = Optional(section["ApprovalReference"]),
            DeploymentProfile = Parse(
                section["DeploymentProfile"],
                AdapterHostDeploymentProfile.Unspecified,
                "AdapterHost:ProductionAdmission:DeploymentProfile"),
            Runtime = Parse(
                section["Runtime"],
                AdapterHostRuntimeKind.Unspecified,
                "AdapterHost:ProductionAdmission:Runtime"),
            SourceCommitSha = Optional(section["SourceCommitSha"]),
            ContainerImageDigest = Optional(section["ContainerImageDigest"]),
            ApprovedAdapterType = Optional(section["ApprovedAdapterType"])
                ?.ToLowerInvariant(),
            ApprovedServiceBaseAddress = Optional(
                section["ApprovedServiceBaseAddress"]),
            StatusEndpointExposure = Parse(
                section["StatusEndpointExposure"],
                AdapterHostStatusEndpointExposure.Unspecified,
                "AdapterHost:ProductionAdmission:StatusEndpointExposure")
        };
    }

    private static T Parse<T>(string? value, T fallback, string name)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!Enum.TryParse(value.Trim(), ignoreCase: true, out T parsed) ||
            !Enum.IsDefined(parsed))
        {
            throw new InvalidOperationException($"{name} is invalid.");
        }

        return parsed;
    }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
