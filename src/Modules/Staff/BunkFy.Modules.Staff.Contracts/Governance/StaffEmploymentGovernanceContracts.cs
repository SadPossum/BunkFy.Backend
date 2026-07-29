namespace BunkFy.Modules.Staff.Contracts;

public static class StaffEmploymentGovernanceContract
{
    public const int CurrentVersion = 1;
}

public sealed record StaffEmploymentGovernanceAcknowledgementDto(
    string AcknowledgementId,
    int AcknowledgementVersion);

public sealed record StaffEmploymentGovernanceDto(
    int ContractVersion,
    Guid StaffMemberId,
    long SelectedStaffVersion,
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string DataRegionId,
    string TransferProfileId,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string PolicyContentSha256,
    DateTimeOffset PolicyEffectiveAtUtc,
    DateTimeOffset PolicyExpiresAtUtc,
    DateTimeOffset EvaluatedAtUtc,
    IReadOnlyCollection<StaffEmploymentGovernanceAcknowledgementDto>
        AcceptedAcknowledgements,
    string ConfiguredBy,
    DateTimeOffset ConfiguredAtUtc,
    long Version);

public sealed record StaffEmploymentGovernanceChangeReceiptDto(
    Guid ReceiptId,
    Guid IdempotencyKey,
    Guid StaffMemberId,
    int ContractVersion,
    long SelectedStaffVersion,
    long PreviousGovernanceVersion,
    long ResultingGovernanceVersion,
    string PolicyContentSha256,
    string AcknowledgementsSha256,
    string ReceiptSha256,
    string ActorId,
    DateTimeOffset CompletedAtUtc);
