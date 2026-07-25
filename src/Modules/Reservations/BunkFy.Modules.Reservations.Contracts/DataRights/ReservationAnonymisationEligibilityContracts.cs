namespace BunkFy.Modules.Reservations.Contracts;

public static class ReservationAnonymisationEligibilityContract
{
    public const int CurrentVersion = 1;
    public const int Sha256Length = 64;
}

public interface IReservationAnonymisationEligibilityEvaluator
{
    Task<ReservationAnonymisationEligibilityResult> EvaluateAsync(
        ReservationAnonymisationEligibilityRequest request,
        CancellationToken cancellationToken);
}

public sealed record ReservationAnonymisationEligibilityRequest(
    int ContractVersion,
    string TenantId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid PropertyId,
    Guid ReservationId,
    long SelectedReservationVersion,
    long SelectedDetailsRevision,
    ReservationAnonymisationRoutingPolicyEvidence RoutingPolicy);

public sealed record ReservationAnonymisationRoutingPolicyEvidence(
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

public sealed record ReservationAnonymisationEligibilityResult(
    int ContractVersion,
    ReservationAnonymisationEligibilityStatus Status,
    ReservationAnonymisationBlockerCode BlockerCode,
    long ReservationVersion,
    long DetailsRevision,
    int ActiveHoldCount,
    string? PolicyEvidenceSha256,
    DateTimeOffset EvaluatedAtUtc);

public enum ReservationAnonymisationEligibilityStatus
{
    Unknown = 0,
    Eligible = 1,
    Blocked = 2
}

public enum ReservationAnonymisationBlockerCode
{
    None = 0,
    ContractUnsupported = 1,
    RequestInvalid = 2,
    TenantMismatch = 3,
    ReservationNotFound = 4,
    ReservationVersionChanged = 5,
    DetailsRevisionChanged = 6,
    ReservationStateUnsupported = 7,
    AllocationOrReleasePending = 8,
    ActiveOrFutureStay = 9,
    ActiveDataHold = 10,
    ProcessingRestrictionStateUnavailable = 11,
    PropertyProjectionMissing = 12,
    PropertyPolicyUnavailable = 13,
    PropertyPolicyNotAllowed = 14,
    PropertyRetentionPolicyNotAllowed = 15,
    PropertyPolicyNotEffective = 16,
    PropertyPolicyExpired = 17,
    RoutingPolicyProjectionChanged = 18,
    RoutingPolicyChanged = 19,
    RoutingRetentionPolicyChanged = 20,
    RoutingPolicyDigestChanged = 21,
    RoutingPurposeChanged = 22,
    RoutingSurfaceChanged = 23,
    RoutingSourceProvenanceChanged = 24,
    RoutingPolicyEvaluationInvalid = 25,
    ProviderReferenceRequired = 26,
    DestructiveOperationInProgress = 27,
    AlreadyRedacted = 28
}
