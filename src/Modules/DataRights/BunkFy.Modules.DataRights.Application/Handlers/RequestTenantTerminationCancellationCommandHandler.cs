namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class RequestTenantTerminationCancellationCommandHandler(
    ITenantTerminationRepository repository,
    ITenantTerminationCoordinationSignal coordinationSignal,
    ISystemClock clock)
    : ICommandHandler<RequestTenantTerminationCancellationCommand,
        TenantTerminationProcessDto>
{
    public async Task<Result<TenantTerminationProcessDto>> HandleAsync(
        RequestTenantTerminationCancellationCommand command,
        CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await repository.GetProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (process is null)
        {
            return Result.Failure<TenantTerminationProcessDto>(
                DataRightsApplicationErrors
                    .TenantTerminationProcessNotFound);
        }

        string actor = command.ActorId?.Trim() ?? string.Empty;
        if (process.Phase == TenantTerminationProcessPhase.Restore &&
            process.Status == TenantTerminationProcessStatus.Pending &&
            string.Equals(
                process.LastChangedBy,
                actor,
                StringComparison.Ordinal))
        {
            return Result.Success(process.ToDto());
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result requested = TenantTerminationCancellationCoordinator.Request(
            process,
            command.ExpectedProcessVersion,
            actor,
            nowUtc);
        if (requested.IsFailure)
        {
            return Result.Failure<TenantTerminationProcessDto>(
                requested.Error);
        }

        if (!await coordinationSignal.EnqueueAsync(
                process,
                nowUtc,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TenantTerminationProcessDto>(
                DataRightsApplicationErrors
                    .TenantTerminationExecutionStateInvalid);
        }

        return Result.Success(process.ToDto());
    }
}
