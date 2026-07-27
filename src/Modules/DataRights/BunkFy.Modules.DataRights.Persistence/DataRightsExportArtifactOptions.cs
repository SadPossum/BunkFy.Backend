namespace BunkFy.Modules.DataRights.Persistence;

public sealed class DataRightsExportArtifactOptions
{
    public const string SectionName = "DataRights:ExportArtifacts";
    internal const string DevelopmentKeyBase64 =
        "YnVua2Z5LWRhdGEtcmlnaHRzLWV4cG9ydC1kZXZrZXk=";
    internal const int MaximumEncryptionOverheadBytes = 512 * 1024;

    public int ActiveKeyVersion { get; set; }
    public Dictionary<int, string> Keys { get; set; } = [];
    public int MaximumPlaintextBytes { get; set; } = 32 * 1024 * 1024;
    public int ChunkSizeBytes { get; set; } = 64 * 1024;
    public TimeSpan ArtifactLifetime { get; set; } = TimeSpan.FromHours(24);
}
