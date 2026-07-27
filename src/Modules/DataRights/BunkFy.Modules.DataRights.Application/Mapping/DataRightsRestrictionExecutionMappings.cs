namespace BunkFy.Modules.DataRights.Application.Mapping;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.ValueObjects;

internal static class DataRightsRestrictionExecutionMappings
{
    public static DataRightsRestrictionExecutionProofDto ToDto(
        this DataRightsRestrictionExecutionProof proof) =>
        new(
            (DataRightsRestrictionDirective)proof.Directive,
            proof.ReceiptId,
            proof.ResultingOwnerRevision,
            proof.ResultingProjectionRevision,
            proof.EffectiveRestricted,
            proof.CompletedAtUtc);
}
