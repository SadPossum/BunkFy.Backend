namespace BunkFy.Modules.DataRights.Persistence;

public sealed class DataRightsLedgerDeltaOptions
{
    public const string SectionName = "DataRights:LedgerDelta";
    internal const string DevelopmentIntegrityKeyBase64 =
        "YnVua2Z5LWRhdGEtcmlnaHRzLWRlbHRhLWRldi12MSE=";

    public DataRightsLedgerDeltaProvider Provider { get; set; }
    public string? LocalFilePath { get; set; }
    public int MaximumPageSize { get; set; } = 200;
    public int ActiveIntegrityKeyVersion { get; set; }
    public Dictionary<int, string> IntegrityKeys { get; set; } = [];
}

public enum DataRightsLedgerDeltaProvider
{
    Unknown = 0,
    LocalFile = 1,
    External = 2
}
