namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

internal sealed class SelectDataRightsSubjectCommandValidator
    : ICommandValidator<SelectDataRightsSubjectCommand>
{
    public IEnumerable<string> Validate(SelectDataRightsSubjectCommand command)
    {
        foreach (string error in DataRightsCaseValidation.Mutation(
            command.PropertyId,
            command.CaseId,
            command.ExpectedVersion,
            command.ActorId))
        {
            yield return error;
        }

        if (!IsValid(command.Coordinate))
        {
            yield return "A valid subject coordinate is required.";
        }
    }

    private static bool IsValid(DataRightsSubjectCoordinate? coordinate) =>
        coordinate is not null &&
        coordinate.RecordId != Guid.Empty &&
        coordinate.RecordVersion > 0 &&
        !string.IsNullOrWhiteSpace(coordinate.OwnerKey) &&
        coordinate.OwnerKey.Trim().Length <= DataRightsSubjectDiscoveryLimits.OwnerKeyMaxLength &&
        !string.IsNullOrWhiteSpace(coordinate.RecordType) &&
        coordinate.RecordType.Trim().Length <= DataRightsSubjectDiscoveryLimits.RecordTypeMaxLength;
}
