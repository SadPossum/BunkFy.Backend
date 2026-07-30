namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Staff.Domain.Aggregates;

internal interface IStaffRetentionCandidateRepository
{
    Task<StaffRetentionScanPage> ScanAsync(
        long afterProjectionOrdinal,
        int limit,
        CancellationToken cancellationToken);

    Task<StaffRetentionCandidateSnapshot?> LoadAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken);
}

internal sealed record StaffRetentionScanPage(
    IReadOnlyList<StaffRetentionCandidateSnapshot> Candidates,
    bool ReachedEnd);

internal sealed record StaffRetentionCandidateSnapshot(
    Guid StaffMemberId,
    long StaffVersion,
    long ProjectionOrdinal,
    StaffMemberState Status,
    DateTimeOffset? DepartedAtUtc,
    DateOnly? DepartureEffectiveOn,
    bool HasCurrentAssignments,
    int ActiveHoldCount,
    DateTimeOffset? EarliestHoldPlacedAtUtc,
    int? ProcessingRestrictionContractVersion,
    long? ProcessingRestrictionRevision,
    long? OperationLockRevision,
    StaffRetentionGovernanceSnapshot? Governance);

internal sealed record StaffRetentionGovernanceSnapshot(
    long GovernanceVersion,
    long SelectedStaffVersion,
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string DataRegionId,
    string TransferProfileId,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string ContentSha256,
    DateTimeOffset PolicyEffectiveAtUtc,
    DateTimeOffset PolicyExpiresAtUtc,
    DateTimeOffset EvaluatedAtUtc,
    DateTimeOffset ConfiguredAtUtc,
    IReadOnlyList<StaffRetentionAcknowledgementSnapshot>
        Acknowledgements);

internal sealed record StaffRetentionAcknowledgementSnapshot(
    string AcknowledgementId,
    int AcknowledgementVersion);
