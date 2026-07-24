namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class BeginDataRightsDecisionCommandHandler(
    IDataRightsCaseRepository cases,
    ISystemClock clock) : ICommandHandler<BeginDataRightsDecisionCommand, DataRightsCaseDto>
{
    public Task<Result<DataRightsCaseDto>> HandleAsync(
        BeginDataRightsDecisionCommand command,
        CancellationToken cancellationToken) => DataRightsCaseCommandExecution.ApplyAsync(
        cases,
        command.PropertyId,
        command.CaseId,
        dataRightsCase => dataRightsCase.BeginDecision(
            command.ExpectedVersion,
            command.ActorId,
            clock.UtcNow),
        cancellationToken);
}
