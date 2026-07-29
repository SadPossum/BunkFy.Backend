namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

public sealed record ConfigureStaffEmploymentGovernanceCommand(
    Guid IdempotencyKey,
    Guid StaffMemberId,
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
        AcceptedAcknowledgements,
    string ActorId)
    : ITransactionalCommand<StaffEmploymentGovernanceChangeReceiptDto>;
