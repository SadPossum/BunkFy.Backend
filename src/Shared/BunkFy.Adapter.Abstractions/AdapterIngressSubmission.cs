namespace BunkFy.Adapter.Abstractions;

public static class AdapterIngressContractLimits
{
    public const long MaximumHttpRequestBodyBytes = 24L * 1024 * 1024;
    public const long MaximumResponseBodyBytes = 256L * 1024;
}

public sealed record AdapterIngressObservationRequest(
    Guid OperationId,
    string RecordType,
    string ExternalRecordId,
    string? SourceRevision,
    DateTimeOffset? SourceUpdatedAtUtc,
    DateTimeOffset ObservedAtUtc,
    string ContentType,
    byte[] Payload,
    string ContentSha256)
{
    public static AdapterIngressObservationRequest FromRecord(AdapterObservedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new AdapterIngressObservationRequest(
            record.OperationId,
            record.RecordType,
            record.ExternalRecordId,
            record.SourceRevision,
            record.SourceUpdatedAtUtc,
            record.ObservedAtUtc,
            record.ContentType,
            record.Payload.ToArray(),
            record.ContentSha256);
    }
}

public sealed record AdapterIngressSubmissionRequest(
    IReadOnlyCollection<AdapterIngressObservationRequest> Records);

public sealed record AdapterIngressSubmissionResponse(
    IReadOnlyCollection<AdapterObservationResult> Results);
