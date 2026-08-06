namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class BeginDataRightsDecisionCommandHandler(
    DataRightsCaseMutationCoordinator mutations,
    ISystemClock clock) : ICommandHandler<BeginDataRightsDecisionCommand, DataRightsCaseDto>
{
    public Task<Result<DataRightsCaseDto>> HandleAsync(
        BeginDataRightsDecisionCommand command,
        CancellationToken cancellationToken) => DataRightsCaseCommandExecution.ApplyAsync(
        mutations,
        command.Scope,
        command.CaseId,
        dataRightsCase => dataRightsCase.BeginDecision(
            command.ExpectedVersion,
            command.ActorId,
            clock.UtcNow),
        cancellationToken);
}
