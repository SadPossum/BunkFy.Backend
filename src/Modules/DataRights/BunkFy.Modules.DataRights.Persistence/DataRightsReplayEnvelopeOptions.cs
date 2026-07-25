namespace BunkFy.Modules.DataRights.Persistence;

public sealed class DataRightsReplayEnvelopeOptions
{
    public const string SectionName = "DataRights:ReplayEnvelope";
    internal const string DevelopmentKeyBase64 =
        "YnVua2Z5LXJlcGxheS1lbnZlbG9wZS1kZXYta2V5LTE=";

    public int ActiveKeyVersion { get; set; }
    public Dictionary<int, string> Keys { get; set; } = [];
}
