namespace BunkFy.Modules.Guests.Contracts;

public static class GuestAnonymisationEligibilityContract
{
    public const int CurrentVersion = 1;
    public const int MaximumAffectedProperties = 256;
    public const int Sha256Length = 64;
}

public interface IGuestAnonymisationEligibilityEvaluator
{
    Task<GuestAnonymisationEligibilityResult> EvaluateAsync(
        GuestAnonymisationEligibilityRequest request,
        CancellationToken cancellationToken);
}

public sealed record GuestAnonymisationEligibilityRequest(
    int ContractVersion,
    string TenantId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid RoutingPropertyId,
    Guid GuestId,
    long SelectedGuestVersion,
    GuestAnonymisationRoutingPolicyEvidence RoutingPolicy);

public sealed record GuestAnonymisationRoutingPolicyEvidence(
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

public sealed record GuestAnonymisationEligibilityResult(
    int ContractVersion,
    GuestAnonymisationEligibilityStatus Status,
    GuestAnonymisationBlockerCode BlockerCode,
    long GuestVersion,
    int AffectedPropertyCount,
    string? AffectedPropertySetSha256,
    string? PolicySetSha256,
    DateTimeOffset EvaluatedAtUtc);

public enum GuestAnonymisationEligibilityStatus
{
    Unknown = 0,
    Eligible = 1,
    Blocked = 2
}

public enum GuestAnonymisationBlockerCode
{
    None = 0,
    ContractUnsupported = 1,
    RequestInvalid = 2,
    TenantMismatch = 3,
    GuestNotFound = 4,
    GuestVersionChanged = 5,
    GuestNotActive = 6,
    RoutingPropertyNotAssociated = 7,
    AffectedPropertySetTooLarge = 8,
    ActiveDataHold = 9,
    StayProjectionUnsupported = 10,
    ActiveOrFutureStay = 11,
    PropertyProjectionMissing = 12,
    PropertyInactive = 13,
    PropertyPolicyUnavailable = 14,
    PropertyPolicyNotAllowed = 15,
    PropertyRetentionPolicyNotAllowed = 16,
    PropertyPolicyNotEffective = 17,
    PropertyPolicyExpired = 18,
    RoutingPolicyProjectionChanged = 19,
    RoutingPolicyChanged = 20,
    RoutingRetentionPolicyChanged = 21,
    RoutingPolicyDigestChanged = 22,
    RoutingPurposeChanged = 23,
    RoutingSurfaceChanged = 24,
    RoutingSourceProvenanceChanged = 25,
    RoutingPolicyEvaluationInvalid = 26,
    DestructiveOperationInProgress = 27
}
