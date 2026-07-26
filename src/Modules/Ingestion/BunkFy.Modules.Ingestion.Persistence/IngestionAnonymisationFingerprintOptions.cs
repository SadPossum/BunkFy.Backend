namespace BunkFy.Modules.Ingestion.Persistence;

public sealed class IngestionAnonymisationFingerprintOptions
{
    public const string SectionName =
        "Ingestion:AnonymisationFingerprints";

    internal const string DevelopmentKeyBase64 =
        "YnVua2Z5LWluZ2VzdGlvbi1maW5nZXJwcmludC12MSE=";

    public int ActiveKeyVersion { get; set; }
    public Dictionary<int, string> Keys { get; set; } = [];
}
