namespace BunkFy.Modules.Ingestion.Application.Contributors;

internal static class IngestionRetentionCoordinates
{
    public const string OwnerKey = "ingestion";
    public const string RawPayloadDataClass = "raw-source-evidence";
    public const string SensitiveHistoryDataClass =
        "sensitive-reservation-history";
    public const int ExecutionPolicyVersion = 1;

    public static bool IsSupported(string dataClassKey) =>
        string.Equals(
            dataClassKey,
            RawPayloadDataClass,
            StringComparison.Ordinal) ||
        string.Equals(
            dataClassKey,
            SensitiveHistoryDataClass,
            StringComparison.Ordinal);
}
