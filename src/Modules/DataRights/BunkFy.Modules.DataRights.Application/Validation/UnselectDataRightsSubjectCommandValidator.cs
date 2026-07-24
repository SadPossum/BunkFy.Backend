namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

internal sealed class UnselectDataRightsSubjectCommandValidator
    : ICommandValidator<UnselectDataRightsSubjectCommand>
{
    public IEnumerable<string> Validate(UnselectDataRightsSubjectCommand command)
    {
        foreach (string error in DataRightsCaseValidation.Mutation(
            command.PropertyId,
            command.CaseId,
            command.ExpectedVersion,
            command.ActorId))
        {
            yield return error;
        }

        DataRightsSubjectCoordinateKey? coordinate = command.Coordinate;
        if (coordinate is null ||
            coordinate.RecordId == Guid.Empty ||
            string.IsNullOrWhiteSpace(coordinate.OwnerKey) ||
            coordinate.OwnerKey.Trim().Length > DataRightsSubjectDiscoveryLimits.OwnerKeyMaxLength ||
            string.IsNullOrWhiteSpace(coordinate.RecordType) ||
            coordinate.RecordType.Trim().Length > DataRightsSubjectDiscoveryLimits.RecordTypeMaxLength)
        {
            yield return "A valid subject coordinate key is required.";
        }
    }
}
