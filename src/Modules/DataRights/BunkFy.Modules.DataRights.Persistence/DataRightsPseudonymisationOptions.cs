namespace BunkFy.Modules.DataRights.Persistence;

public sealed class DataRightsPseudonymisationOptions
{
    public const string SectionName = "DataRights:Pseudonymisation";
    internal const string DevelopmentKeyBase64 =
        "YnVua2Z5LWRhdGEtcmlnaHRzLWRldi1rZXktdjEhISE=";

    public int ActiveKeyVersion { get; set; }
    public Dictionary<int, string> Keys { get; set; } = [];
}
