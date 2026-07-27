namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsRestrictionExecutionDto(
    DataRightsCaseDto Case,
    DataRightsRestrictionExecutionProofDto Proof);

public sealed record DataRightsRestrictionExecutionProofDto(
    DataRightsRestrictionDirective Directive,
    Guid ReceiptId,
    long ResultingOwnerRevision,
    long ResultingProjectionRevision,
    bool EffectiveRestricted,
    DateTimeOffset CompletedAtUtc);
