namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class UnselectDataRightsSubjectCommandHandler(
    IDataRightsCaseRepository cases,
    ISystemClock clock) : ICommandHandler<UnselectDataRightsSubjectCommand, DataRightsCaseDto>
{
    public Task<Result<DataRightsCaseDto>> HandleAsync(
        UnselectDataRightsSubjectCommand command,
        CancellationToken cancellationToken)
    {
        DataRightsSubjectCoordinateKey? coordinate = command.Coordinate;
        if (coordinate is null)
        {
            return Task.FromResult(Result.Failure<DataRightsCaseDto>(
                DataRightsApplicationErrors.SubjectCoordinateInvalid));
        }

        return DataRightsCaseCommandExecution.ApplyAsync(
            cases,
            command.PropertyId,
            command.CaseId,
            dataRightsCase => dataRightsCase.UnselectSubject(
                coordinate.OwnerKey,
                coordinate.RecordType,
                coordinate.RecordId,
                command.ExpectedVersion,
                command.ActorId,
                clock.UtcNow),
            cancellationToken);
    }
}
