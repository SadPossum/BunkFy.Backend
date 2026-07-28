namespace BunkFy.Host.ServiceDefaults.Production;

public sealed class BunkFyDeploymentOptions
{
    public const string SectionName = "BunkFy:Deployment";

    public BunkFyDeploymentProfile Profile { get; set; }
    public BunkFyApiTopology ApiTopology { get; set; }
    public BunkFyEdgeMode EdgeMode { get; set; }
    public BunkFyRuntimeKind Runtime { get; set; }
    public string? SourceCommitSha { get; set; }
    public string? ContainerImageDigest { get; set; }
    public BunkFyKeyProtectionKind DataProtectionKeyProtection { get; set; }
    public BunkFyStorageCredentialProfile ObjectStorageCredentialProfile { get; set; }
}

public enum BunkFyDeploymentProfile
{
    Unspecified = 0,
    SelfHosted = 1,
    Hosted = 2,
    Preview = 3
}

public enum BunkFyApiTopology
{
    Unspecified = 0,
    SingleReplica = 1,
    MultiReplica = 2
}

public enum BunkFyEdgeMode
{
    Unspecified = 0,
    DirectHttps = 1,
    TrustedReverseProxy = 2
}

public enum BunkFyRuntimeKind
{
    Unspecified = 0,
    Process = 1,
    Container = 2
}

public enum BunkFyKeyProtectionKind
{
    Unspecified = 0,
    EncryptedVolume = 1,
    Certificate = 2,
    Kms = 3,
    Hsm = 4,
    PlatformManaged = 5
}

public enum BunkFyStorageCredentialProfile
{
    Unspecified = 0,
    DedicatedServiceAccount = 1
}

public enum BunkFyDeploymentSurface
{
    PublicApi = 1,
    AdminApi = 2
}
