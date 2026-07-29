namespace BunkFy.Modules.Staff.Api.Requests;

using BunkFy.Modules.Staff.Contracts;

public sealed record ConfigureStaffEmploymentGovernanceRequest(
    Guid IdempotencyKey,
    long ExpectedStaffVersion,
    long ExpectedGovernanceVersion,
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string DataRegionId,
    string TransferProfileId,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    IReadOnlyCollection<StaffEmploymentGovernanceAcknowledgementDto>
        AcceptedAcknowledgements);
