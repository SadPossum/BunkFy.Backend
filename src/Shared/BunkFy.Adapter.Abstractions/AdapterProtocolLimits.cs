namespace BunkFy.Adapter.Abstractions;

public static class AdapterProtocolLimits
{
    public const int AdapterTypeMaxLength = 100;
    public const int RecordTypeMaxLength = 100;
    public const int ExternalRecordIdMaxLength = 300;
    public const int SourceRevisionMaxLength = 300;
    public const int CheckpointMaxLength = 2048;
    public const int ContentTypeMaxLength = 100;
    public const int ErrorCodeMaxLength = 200;
    public const int ErrorMessageMaxLength = 2000;
    public const int MaximumRecordsPerSubmission = 100;
    public const int MaximumInlinePayloadBytes = 4 * 1024 * 1024;
    public const int MaximumSubmissionPayloadBytes = 16 * 1024 * 1024;
    public const int MaximumConfigurationMaterialBytes = 256 * 1024;
    public const int MaximumSecretMaterialBytes = 64 * 1024;
    public const int Sha256Length = 64;
}
