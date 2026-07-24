namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class RecordDataRightsDecisionCommandHandler(
    IDataRightsCaseRepository cases,
    ISystemClock clock) : ICommandHandler<RecordDataRightsDecisionCommand, DataRightsCaseDto>
{
    public Task<Result<DataRightsCaseDto>> HandleAsync(
        RecordDataRightsDecisionCommand command,
        CancellationToken cancellationToken) => DataRightsCaseCommandExecution.ApplyAsync(
        cases,
        command.PropertyId,
        command.CaseId,
        dataRightsCase => dataRightsCase.RecordDecision(
            (DataRightsCaseDecision)command.Decision,
            (DataRightsCaseDecisionReason)command.Reason,
            command.ExpectedVersion,
            command.ActorId,
            clock.UtcNow),
        cancellationToken);
}
