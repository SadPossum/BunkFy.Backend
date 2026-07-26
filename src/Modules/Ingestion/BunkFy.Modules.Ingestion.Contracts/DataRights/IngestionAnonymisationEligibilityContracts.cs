namespace BunkFy.Modules.Ingestion.Contracts;

public static class IngestionAnonymisationEligibilityContract
{
    public const int CurrentVersion = 1;
    public const int Sha256Length = 64;
    public const int MaximumGraphRecords = 1_000;
}

public interface IIngestionAnonymisationEligibilityEvaluator
{
    Task<IngestionAnonymisationEligibilityResult> EvaluateAsync(
        IngestionAnonymisationEligibilityRequest request,
        CancellationToken cancellationToken);
}

public sealed record IngestionAnonymisationEligibilityRequest(
    int ContractVersion,
    string TenantId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid PropertyId,
    Guid SourceLinkId,
    long SelectedSourceLinkVersion,
    IngestionAnonymisationRoutingPolicyEvidence RoutingPolicy);

public sealed record IngestionAnonymisationRoutingPolicyEvidence(
    long PropertyPolicySourceVersion,
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string ContentSha256,
    string PurposeCode,
    string Surface,
    string SourceProvenance,
    DateTimeOffset EvaluatedAtUtc);

public sealed record IngestionAnonymisationEligibilityResult(
    int ContractVersion,
    IngestionAnonymisationEligibilityStatus Status,
    IngestionAnonymisationBlockerCode BlockerCode,
    long SourceLinkVersion,
    long RetentionFenceVersion,
    int ActiveLegalHoldCount,
    int GraphRecordCount,
    string? PolicyEvidenceSha256,
    string? OperationFenceSha256,
    DateTimeOffset EvaluatedAtUtc);

public enum IngestionAnonymisationEligibilityStatus
{
    Unknown = 0,
    Eligible = 1,
    Blocked = 2
}

public enum IngestionAnonymisationBlockerCode
{
    None = 0,
    ContractUnsupported = 1,
    RequestInvalid = 2,
    TenantMismatch = 3,
    SourceLinkNotFound = 4,
    SourceLinkVersionChanged = 5,
    OwnerGraphUnavailable = 6,
    OwnerGraphTooLarge = 7,
    PropertyProjectionMissing = 8,
    PropertyPolicyUnavailable = 9,
    PropertyPolicyNotAllowed = 10,
    PropertyRetentionPolicyNotAllowed = 11,
    PropertyPolicyNotEffective = 12,
    PropertyPolicyExpired = 13,
    RoutingPolicyProjectionChanged = 14,
    RoutingPolicyChanged = 15,
    RoutingRetentionPolicyChanged = 16,
    RoutingPolicyDigestChanged = 17,
    RoutingPurposeChanged = 18,
    RoutingSurfaceChanged = 19,
    RoutingSourceProvenanceChanged = 20,
    RoutingPolicyEvaluationInvalid = 21,
    ActiveLegalHold = 22,
    RawPayloadPurgeInProgress = 23,
    ReprocessingReservationActive = 24,
    ReprocessingAttemptActive = 25,
    ObservationProcessingInProgress = 26,
    ChangeProposalInProgress = 27,
    ReservationDispatchInProgress = 28,
    ProviderReconciliationRequired = 29,
    SourceLinkStateUnsupported = 30,
    OperationFenceUnavailable = 31
}
