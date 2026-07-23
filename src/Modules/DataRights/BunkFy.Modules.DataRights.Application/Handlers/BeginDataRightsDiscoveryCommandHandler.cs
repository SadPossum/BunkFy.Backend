namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class BeginDataRightsDiscoveryCommandHandler(
    IDataRightsCaseRepository cases,
    ISystemClock clock) : ICommandHandler<BeginDataRightsDiscoveryCommand, DataRightsCaseDto>
{
    public Task<Result<DataRightsCaseDto>> HandleAsync(
        BeginDataRightsDiscoveryCommand command,
        CancellationToken cancellationToken) => DataRightsCaseCommandExecution.ApplyAsync(
        cases,
        command.PropertyId,
        command.CaseId,
        dataRightsCase => dataRightsCase.BeginDiscovery(
            command.ExpectedVersion,
            command.ActorId,
            clock.UtcNow),
        cancellationToken);
}
