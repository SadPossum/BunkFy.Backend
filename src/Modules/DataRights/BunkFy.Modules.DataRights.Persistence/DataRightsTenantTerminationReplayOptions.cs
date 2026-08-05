namespace BunkFy.Modules.DataRights.Persistence;

public sealed class DataRightsTenantTerminationReplayOptions
{
    public const string SectionName =
        "DataRights:TenantTerminationReplay";

    public DataRightsTenantTerminationReplayProvider Provider { get; set; }
    public string? LocalFilePath { get; set; }
    public int MaximumPageSize { get; set; } = 200;
}

public enum DataRightsTenantTerminationReplayProvider
{
    Unknown = 0,
    LocalFile = 1,
    External = 2
}
